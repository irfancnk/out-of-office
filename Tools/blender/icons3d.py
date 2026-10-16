# Renders the game's 3D UI icons (OUT-30): stylised, softly lit props with cartoon outlines, on transparent
# backgrounds, into Assets/OutOfOffice/Resources/Icons3D/<name>.png.
#
# Run headless from the repository root:
#   blender --background --factory-startup --python Tools/blender/icons3d.py -- [name ...]
# With no names, every icon is rendered. Re-run after editing a prop; the game picks the PNGs up on the next build.

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "OutOfOffice", "Resources", "Icons3D")
SIZE = 512
INK = (0.12, 0.07, 0.10)


def hexc(h, a=1.0):
    v = int(h.lstrip("#"), 16)
    srgb = [((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (lin[0], lin[1], lin[2], a)


# ---------------------------------------------------------------- scene

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.cycles.device = "CPU"
    scene.render.resolution_x = SIZE
    scene.render.resolution_y = SIZE
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"  # keep the UI palette saturated, no filmic wash
    scene.view_settings.look = "None"
    # Cartoon outlines that match the game's inked look.
    scene.render.use_freestyle = True
    scene.render.line_thickness_mode = "ABSOLUTE"
    scene.render.line_thickness = 3.2
    ls = scene.view_layers[0].freestyle_settings.linesets.new("Ink")
    ls.select_by_visibility = True
    ls.select_by_edge_types = True
    ls.select_silhouette = True
    ls.select_border = True
    ls.select_crease = False
    ls.linestyle.color = INK[:3]
    ls.linestyle.thickness = 3.2

    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.9, 0.92, 1.0, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.55

    def area(name, loc, energy, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        direction = Vector((0, 0, 0)) - Vector(loc)
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    area("Key", (3.5, -4.0, 6.0), 900, 4.0, (1.0, 0.97, 0.92))
    area("Fill", (-5.0, -2.0, 3.0), 280, 5.0, (0.85, 0.9, 1.0))
    area("Rim", (0.0, 5.0, 4.0), 500, 3.0, (1.0, 0.95, 1.0))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 3.0
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # A friendly three-quarter view from slightly above, like a toy on a shelf.
    cam.location = (3.6, -6.2, 4.4)
    cam.rotation_euler = (Vector((0, 0, 0.35)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    return scene, cam


def material(name, color, rough=0.38, metal=0.0, emit=0.0, coat=0.25):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
    if emit > 0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emit
    return m


def finish(obj, mat, bevel=0.0, segments=4, smooth=True):
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
        if hasattr(obj.data, "set_sharp_from_angle"):
            obj.data.set_sharp_from_angle(angle=math.radians(40))
    return obj


def box(size, loc, mat, bevel=0.06, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, mat, bevel)


def cyl(radius, depth, loc, mat, rot=(0, 0, 0), verts=48, bevel=0.03, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, location=loc, rotation=rot, vertices=verts)
    else:
        bpy.ops.mesh.primitive_cone_add(radius1=radius, radius2=r2, depth=depth, location=loc, rotation=rot, vertices=verts)
    return finish(bpy.context.active_object, mat, bevel)


def sphere(radius, loc, mat, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=loc, segments=40, ring_count=20)
    o = bpy.context.active_object
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, mat)


def torus(major, minor, loc, mat, rot=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot, major_segments=48, minor_segments=16)
    o = bpy.context.active_object
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, mat)


def frame(cam, objects, margin=1.22):
    # Fit the orthographic camera to everything built, with a little room for the outline.
    pts = []
    for o in objects:
        if o.type != "MESH":
            continue
        if o.hide_render:
            continue
        for v in o.data.vertices:  # real geometry, not the (stale) bounding box
            pts.append(o.matrix_world @ v.co)
    inv = cam.matrix_world.inverted()
    local = [inv @ p for p in pts]
    xs = [p.x for p in local]
    ys = [p.y for p in local]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), max(ys) - min(ys)) * margin
    cam.data.ortho_scale = span
    cam.location = cam.matrix_world @ Vector((cx, cy, 0))


# ---------------------------------------------------------------- props

def chest():
    wood = material("Wood", hexc("#C8743A"), 0.5)
    wood_dark = material("WoodDark", hexc("#8A4320"), 0.55)
    gold = material("Gold", hexc("#FFC63A"), 0.25, metal=0.85)
    ink = material("Keyhole", hexc("#2A1810"), 0.6)
    box((1.9, 1.25, 0.95), (0, 0, 0.475), wood, 0.07)
    box((1.96, 1.31, 0.12), (0, 0, 0.9), wood_dark, 0.04)               # lip under the lid
    for z in (0.3, 0.6):                                               # plank lines on the front and sides
        box((1.91, 1.26, 0.025), (0, 0, z), wood_dark, 0.008)
    lid = cyl(0.66, 1.9, (0, 0, 0.95), wood, rot=(0, math.pi / 2, 0), bevel=0.05)
    lid.scale = (1, 0.95, 0.62)
    bpy.ops.object.transform_apply(scale=True)
    for x in (-0.62, 0.62):                                            # gold straps over body and lid
        box((0.2, 1.33, 0.98), (x, 0, 0.49), gold, 0.03)
        strap = cyl(0.69, 0.2, (x, 0, 0.95), gold, rot=(0, math.pi / 2, 0), bevel=0.02)
        strap.scale = (1, 0.97, 0.64)
        bpy.ops.object.transform_apply(scale=True)
    plate = box((0.42, 0.12, 0.5), (0, -0.66, 0.78), gold, 0.05)        # lock plate
    cyl(0.07, 0.06, (0, -0.73, 0.84), ink, rot=(math.pi / 2, 0, 0), bevel=0.0)
    box((0.05, 0.06, 0.13), (0, -0.73, 0.74), ink, 0.0)
    for x in (-0.92, 0.92):                                            # corner studs
        for z in (0.08, 0.86):
            sphere(0.07, (x, -0.6, z), gold)


def gift():
    paper = material("Paper", hexc("#FF6FA8"), 0.42)
    paper_dark = material("PaperDark", hexc("#E24C8C"), 0.45)
    ribbon = material("Ribbon", hexc("#FFD447"), 0.3, metal=0.2)
    box((1.5, 1.5, 1.1), (0, 0, 0.55), paper, 0.06)
    box((1.62, 1.62, 0.32), (0, 0, 1.2), paper_dark, 0.06)             # lid
    box((0.26, 1.66, 1.46), (0, 0, 0.74), ribbon, 0.03)
    box((1.66, 0.26, 1.46), (0, 0, 0.74), ribbon, 0.03)
    for a in (-1, 1):                                                  # bow loops
        loop = torus(0.3, 0.09, (a * 0.3, 0, 1.52), ribbon, rot=(math.pi / 2, a * 0.5, 0), scale=(1, 1, 0.7))
    sphere(0.14, (0, 0, 1.45), ribbon)


def shirt():
    cloth = material("Shirt", hexc("#F7F5EF"), 0.6, coat=0.0)
    tie = material("Tie", hexc("#3D7BF2"), 0.35)
    tie_dark = material("TieDark", hexc("#2A5BC4"), 0.35)
    body = box((1.25, 0.42, 1.5), (0, 0, 0.75), cloth, 0.16)
    for s in (-1, 1):                                                  # sleeves angled out
        box((0.62, 0.38, 0.4), (s * 0.78, 0, 1.25), cloth, 0.14, rot=(0, s * -0.55, 0))
        box((0.42, 0.06, 0.26), (s * 0.24, -0.2, 1.48), cloth, 0.05, rot=(0, s * 0.5, 0))   # collar flaps
    box((0.2, 0.08, 0.16), (0, -0.24, 1.38), tie_dark, 0.04)             # knot
    blade = cyl(0.2, 0.95, (0, -0.24, 0.8), tie, rot=(0, 0, 0), verts=4, bevel=0.04, r2=0.1)
    blade.rotation_euler = (math.pi, 0, math.pi / 4)
    blade.scale = (1, 0.22, 1)
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    for z in (0.95, 0.6, 0.25):                                        # buttons
        sphere(0.035, (0.3, -0.22, z), tie_dark)


def door():
    frame_m = material("Frame", hexc("#6B4A86"), 0.45)
    panel = material("Door", hexc("#B07A4A"), 0.5)
    glass = material("Glass", hexc("#9EE4FF"), 0.08, coat=0.6)
    brass = material("Brass", hexc("#FFC63A"), 0.25, metal=0.85)
    sign = material("Sign", hexc("#FF5E7E"), 0.4)
    box((1.4, 0.32, 2.1), (0, 0, 1.05), frame_m, 0.06)
    box((1.12, 0.36, 1.92), (0, -0.04, 1.0), panel, 0.04)
    box((0.7, 0.38, 0.6), (0, -0.05, 1.48), glass, 0.03)                 # window
    sphere(0.09, (0.4, -0.27, 0.95), brass)                             # knob
    box((0.6, 0.1, 0.22), (0, -0.25, 0.62), sign, 0.04)                 # "do not disturb" plate


def gear():
    steel = material("Steel", hexc("#7FA4D9"), 0.3, metal=0.6)
    steel_dark = material("SteelDark", hexc("#4F6FA8"), 0.35, metal=0.6)
    ring = cyl(0.78, 0.36, (0, 0, 0), steel, rot=(math.pi / 2, 0, 0), bevel=0.05)
    for i in range(8):
        a = i * math.pi / 4
        box((0.34, 0.36, 0.34), (math.cos(a) * 0.9, 0, math.sin(a) * 0.9), steel, 0.06, rot=(0, -a, 0))
    hole = cyl(0.3, 0.6, (0, 0, 0), steel_dark, rot=(math.pi / 2, 0, 0), bevel=0.0)
    mod = ring.modifiers.new("Hole", "BOOLEAN")
    mod.object = hole
    mod.operation = "DIFFERENCE"
    bpy.context.view_layer.objects.active = ring
    bpy.ops.object.modifier_apply(modifier="Hole")
    hole.hide_render = True
    torus(0.31, 0.05, (0, -0.18, 0), steel_dark, rot=(math.pi / 2, 0, 0))


def home():
    wall = material("Wall", hexc("#FFF1DC"), 0.55)
    roof = material("Roof", hexc("#FF5E5E"), 0.4)
    wood = material("Door", hexc("#A8683A"), 0.5)
    glass = material("Window", hexc("#9EE4FF"), 0.1, coat=0.6)
    brick = material("Chimney", hexc("#C9583E"), 0.6)
    box((1.6, 1.3, 1.05), (0, 0, 0.525), wall, 0.05)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.32, radius2=0.0, depth=0.95, location=(0, 0, 1.5), rotation=(0, 0, math.pi / 4))
    r = bpy.context.active_object
    r.scale = (1.0, 0.85, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    finish(r, roof, 0.05, smooth=False)
    box((0.22, 0.22, 0.5), (0.48, 0.2, 1.7), brick, 0.03)
    box((0.42, 0.06, 0.66), (0, -0.66, 0.33), wood, 0.04)
    for x in (-0.5, 0.5):
        box((0.3, 0.06, 0.3), (x, -0.66, 0.68), glass, 0.03)


def coffee():
    paper = material("Paper", hexc("#E8C9A0"), 0.55)  # kraft paper
    sleeve = material("Sleeve", hexc("#FF6B5E"), 0.45)
    lid = material("Lid", hexc("#FFFFFF"), 0.3)
    brown = material("Coffee", hexc("#5A3322"), 0.4)
    cyl(0.5, 1.5, (0, 0, 0.75), paper, verts=64, bevel=0.03, r2=0.62)
    cyl(0.59, 0.55, (0, 0, 0.8), sleeve, verts=64, bevel=0.02, r2=0.64)
    cyl(0.68, 0.14, (0, 0, 1.56), lid, verts=64, bevel=0.05)
    dome = sphere(0.62, (0, 0, 1.6), lid, scale=(1, 1, 0.22))
    box((0.2, 0.08, 0.04), (0.28, -0.1, 1.74), brown, 0.02)             # sip hole


def gem():
    # A brilliant-cut gem: a faceted crown over a deep pavilion, glossy violet with a bright table.
    stone = material("Gem", hexc("#9B5CFF"), 0.08, coat=0.9)
    table = material("Table", hexc("#B990FF"), 0.05, coat=1.0)
    cyl(0.9, 0.42, (0, 0, 0.21), stone, verts=10, bevel=0.0, r2=0.55)          # crown
    cyl(0.9, 1.1, (0, 0, -0.55), stone, verts=10, bevel=0.0, r2=0.0).rotation_euler = (math.pi, 0, 0)
    cyl(0.55, 0.04, (0, 0, 0.43), table, verts=10, bevel=0.0)                  # the flat top catches the light
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            for p in o.data.polygons:
                p.use_smooth = False                                           # facets, not a blob


def arc_arrow(a0, a1, head_at_start, radius=0.9, tube=0.2):
    # A chunky curved arrow (OUT-39): a round tube along an arc in the picture plane, ending in a fat arrowhead. The arc
    # leaves a wide gap and tucks under the head, so the arrow never reads as a closed ring. Angles in degrees,
    # counterclockwise as the camera sees it; the head sits at a0 pointing clockwise, or at a1 pointing onward.
    body = material("Arrow", hexc("#D3DBEC"), 0.45, coat=0.15)  # off-white: pure white clips to a flat shape
    head_len, head_w = 0.8, 0.52
    # Pull the tube's end back under the head.
    start, end = (a0 + 8, a1) if head_at_start else (a0, a1 - 8)
    data = bpy.data.curves.new("Arc", "CURVE")
    data.dimensions = "3D"
    data.bevel_depth = tube
    data.bevel_resolution = 8
    data.use_fill_caps = True
    spline = data.splines.new("POLY")
    steps = 64
    spline.points.add(steps)
    for i in range(steps + 1):
        a = math.radians(start + (end - start) * i / steps)
        spline.points[i].co = (radius * math.cos(a), 0.0, radius * math.sin(a), 1.0)
    arc = bpy.data.objects.new("Arc", data)
    bpy.context.scene.collection.objects.link(arc)
    bpy.context.view_layer.objects.active = arc
    arc.select_set(True)
    bpy.ops.object.convert(target="MESH")
    finish(arc, body)

    at = math.radians(a0 if head_at_start else a1)
    point = Vector((radius * math.cos(at), 0.0, radius * math.sin(at)))
    radial = point.normalized()
    # Tangent along the arrow's travel: clockwise at the start, counterclockwise at the end.
    tangent = Vector((math.sin(at), 0.0, -math.cos(at))) if head_at_start else Vector((-math.sin(at), 0.0, math.cos(at)))
    # A thick flat arrowhead in the arc's plane, its base across the tube's end.
    base = point - tangent * 0.06
    tri = [base + radial * head_w, base - radial * head_w, base + tangent * head_len]
    depth = Vector((0.0, tube * 1.05, 0.0))
    verts = [p - depth for p in tri] + [p + depth for p in tri]
    faces = [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]
    mesh = bpy.data.meshes.new("Head")
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)  # outward, whatever order the corners came in
    bm.to_mesh(mesh)
    bm.free()
    head = bpy.data.objects.new("Head", mesh)
    bpy.context.scene.collection.objects.link(head)
    finish(head, body, bevel=0.07, segments=5)

    # Softer light than the other props: a near-white object otherwise clips to a flat cut-out.
    for light in bpy.data.lights:
        light.energy *= 0.5

    # Face the camera, turned a little so it still reads as a solid thing.
    pivot = bpy.data.objects.new("Pivot", None)
    bpy.context.scene.collection.objects.link(pivot)
    for o in (arc, head):
        o.parent = pivot
    pivot.rotation_euler = (math.radians(-22), 0, math.radians(26))
    pivot.location = (0, 0, 0.35)


def restart():
    arc_arrow(100, 370, True)      # ↻: three quarters of a turn, the head on top pointing right, the gap at the top right


def undo():
    arc_arrow(-25, 185, False)     # ↶: over the top, the head at the left pointing down


PROPS = {"gem": gem, "chest": chest, "gift": gift, "shirt": shirt, "rooms": door, "gear": gear, "home": home, "coffee": coffee,
         "restart": restart, "undo": undo}


def render(name):
    scene, cam = reset()
    PROPS[name]()
    bpy.context.view_layer.update()  # world matrices of anything rotated after creation
    frame(cam, list(scene.objects))
    os.makedirs(OUT, exist_ok=True)
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("ICON3D rendered", name)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for n in (argv or list(PROPS)):
        render(n)
