using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Out of Office > Level Editor (OUT-12): build, play and prove a room without writing code.
    //
    //   Left    the catalog: chapters and their rooms. Open, add, reorder, move between chapters, bulk-tag, CSV report.
    //   Middle  the board. Click to select, drag to move (snapped to the grid), drag the yellow handle to resize,
    //           R / Shift+R to rotate, arrows to nudge, Delete, Ctrl+D duplicate, Ctrl+C / Ctrl+V copy and paste,
    //           Ctrl+S save. Undo and redo are Unity's. Movers, spinners and pop-up walls animate when Animate is on.
    //   Right   add pieces, edit the room and the selection, live warnings (click one to select the culprit), and
    //           the LevelCheck result with its solution drawn on the board.
    public class LevelEditorWindow : EditorWindow
    {
        enum Snap { Off, Tenth, Quarter, Half, One }
        static readonly float[] SnapSize = { 0f, 0.1f, 0.25f, 0.5f, 1f };

        LevelCatalog catalog;
        LevelDef room;
        string roomId;
        bool dirty;
        LevelEditorState state;

        LevelLint.Part selPart = LevelLint.Part.Level;
        int selIndex = -1, selPoint = -1;
        readonly HashSet<string> picked = new HashSet<string>();

        Snap snap = Snap.Tenth;
        bool grid = true, animate = true;
        string search = "", bulkTag = "";
        Vector2 leftScroll, rightScroll;
        List<LevelLint.Problem> lint = new List<LevelLint.Problem>();
        LevelCheck.Report report;
        List<List<Vector2>> solution;

        // Board mapping, refreshed each repaint.
        Rect canvas;
        Vector2 worldCenter;
        float scale = 30f;

        enum Drag { None, Move, Resize, Point }
        Drag drag;
        Vector2 dragGrab;
        int dragGroup;

        const float LeftWidth = 250f, RightWidth = 330f;
        const string ClipPrefix = "ooo-piece:";

        [MenuItem("Out of Office/Level Editor %#l")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor").Show();

        void OnEnable()
        {
            minSize = new Vector2(980f, 560f);
            state = CreateInstance<LevelEditorState>();
            state.hideFlags = HideFlags.HideAndDontSave;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += Tick;
            ReloadCatalog();
            if (room == null && Levels.All.Length > 0) OpenRoom(Levels.All[0].Id);
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= Tick;
            if (state != null) { Undo.ClearUndo(state); DestroyImmediate(state); }
        }

        double lastFrame;

        // Animates movers, spinners, pop-up walls and spikes at about 30 frames a second.
        void Tick()
        {
            if (EditorApplication.timeSinceStartup - lastFrame < 1.0 / 30.0) return;
            lastFrame = EditorApplication.timeSinceStartup;
            if (animate && room != null && (room.Blocks.Exists(b => b.Moves || b.BlinkPeriod > 0f) || room.Zones.Exists(z => z.Period > 0f))) Repaint();
        }

        void OnUndoRedo()
        {
            if (state == null || string.IsNullOrEmpty(state.Json)) return;
            try { room = LevelData.ParseLevel(state.Json); } catch { return; }
            dirty = true;
            ClampSelection();
            Relint();
            Repaint();
        }

        // ---------- Catalog and files ----------

        void ReloadCatalog() => catalog = LevelFiles.ReadCatalog();

        void OpenRoom(string id)
        {
            if (id == roomId && room != null) return;
            if (!ConfirmDiscard()) return;
            var def = catalog.Rooms.TryGetValue(id, out var d) && d != null ? LevelData.Clone(d) : LevelFiles.ReadRoom(id);
            if (def == null) return;
            SetRoom(def, false);
        }

        void SetRoom(LevelDef def, bool isDirty)
        {
            room = def;
            roomId = def.Id;
            dirty = isDirty;
            report = null;
            solution = null;
            Select(LevelLint.Part.Level, -1);
            Undo.ClearUndo(state);
            state.Json = LevelData.ToJson(room);
            Relint();
            Repaint();
        }

        bool ConfirmDiscard()
        {
            if (!dirty || room == null) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved room", $"Save the changes to '{room.Id}'?", "Save", "Cancel", "Discard");
            if (choice == 1) return false;
            if (choice == 0) Save();
            return true;
        }

        void Save()
        {
            if (room == null) return;
            if (room.Id != roomId)
            {
                if (LevelFiles.ShippedIds().Contains(roomId) &&
                    !EditorUtility.DisplayDialog("Rename a shipped room?", $"'{roomId}' has shipped: players' stars are saved under it. Renaming it loses them. Rename anyway?", "Rename", "Cancel"))
                    return;
                if (File.Exists(LevelFiles.RoomPath(room.Id)) && !EditorUtility.DisplayDialog("Room exists", $"A room '{room.Id}' already exists. Overwrite it?", "Overwrite", "Cancel"))
                    return;
                foreach (var ch in catalog.Chapters)
                    for (int i = 0; i < ch.LevelIds.Count; i++)
                        if (ch.LevelIds[i] == roomId) ch.LevelIds[i] = room.Id;
                if (File.Exists(LevelFiles.RoomPath(roomId))) AssetDatabase.DeleteAsset(LevelFiles.RoomPath(roomId));
                LevelFiles.WriteCatalog(catalog.Chapters);
            }
            LevelFiles.WriteRoom(room);
            roomId = room.Id;
            dirty = false;
            LevelFiles.Refresh();
            ReloadCatalog();
            ShowNotification(new GUIContent($"Saved {room.Id}"), 1.2);
        }

        void SaveCatalog()
        {
            LevelFiles.WriteCatalog(catalog.Chapters);
            LevelFiles.Refresh();
            ReloadCatalog();
        }

        string UniqueId(string stem)
        {
            string id = stem;
            for (int n = 2; catalog.Rooms.ContainsKey(id) || File.Exists(LevelFiles.RoomPath(id)); n++) id = stem + n;
            return id;
        }

        void NewRoom(int chapter, string template, LevelDef copyOf = null)
        {
            if (!ConfirmDiscard()) return;
            LevelDef def;
            if (copyOf != null)
            {
                def = LevelData.Clone(copyOf);
                def.Id = UniqueId(copyOf.Id + "-copy");
                def.Name = (copyOf.Name + " II").Length <= LevelLint.MaxNameLength ? copyOf.Name + " II" : "New Room";
            }
            else def = LevelTemplates.Make(template, UniqueId("room"), "New Room");
            LevelFiles.WriteRoom(def);
            int at = Mathf.Clamp(chapter, 0, catalog.Chapters.Count - 1);
            var ids = catalog.Chapters[at].LevelIds;
            int after = copyOf != null ? ids.IndexOf(copyOf.Id) : -1;
            ids.Insert(after >= 0 ? after + 1 : ids.Count, def.Id);
            SaveCatalog();
            roomId = null;
            SetRoom(def, false);
        }

        void DeleteRoom(string id)
        {
            if (LevelFiles.ShippedIds().Contains(id))
            {
                EditorUtility.DisplayDialog("Shipped room", $"'{id}' has shipped, so players' saves refer to it. It cannot be deleted.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete room", $"Delete '{id}' and its file?", "Delete", "Cancel")) return;
            foreach (var ch in catalog.Chapters) ch.LevelIds.Remove(id);
            AssetDatabase.DeleteAsset(LevelFiles.RoomPath(id));
            SaveCatalog();
            if (roomId == id) { room = null; roomId = null; dirty = false; if (Levels.All.Length > 0) OpenRoom(Levels.All[0].Id); }
        }

        // ---------- Editing ----------

        void Change(string label, System.Action edit)
        {
            Undo.RecordObject(state, label);
            edit();
            Commit();
        }

        void Commit()
        {
            state.Json = LevelData.ToJson(room);
            dirty = true;
            report = null;
            solution = null;
            Relint();
        }

        void Relint() => lint = room == null ? new List<LevelLint.Problem>() : LevelLint.Check(room);

        void Select(LevelLint.Part part, int index)
        {
            selPart = part;
            selIndex = index;
            selPoint = -1;
            GUI.FocusControl(null);
        }

        void ClampSelection()
        {
            int count = selPart == LevelLint.Part.Block ? room.Blocks.Count : selPart == LevelLint.Part.Zone ? room.Zones.Count
                : selPart == LevelLint.Part.Rail ? room.Rails.Count : selPart == LevelLint.Part.Star ? room.Stars.Count : 1;
            if (selIndex >= count) Select(LevelLint.Part.Level, -1);
        }

        float Snapped(float v)
        {
            float s = SnapSize[(int)snap];
            return s <= 0f ? Mathf.Round(v * 1000f) / 1000f : Mathf.Round(v / s) * s;
        }

        Vector2 Snapped(Vector2 v) => new Vector2(Snapped(v.x), Snapped(v.y));

        Vector2 Position(LevelLint.Part part, int i)
        {
            switch (part)
            {
                case LevelLint.Part.Block: return room.Blocks[i].Center;
                case LevelLint.Part.Zone: return room.Zones[i].Center;
                case LevelLint.Part.Star: return room.Stars[i];
                case LevelLint.Part.Hole: return room.Hole;
                case LevelLint.Part.Start: return room.Start;
                case LevelLint.Part.Rail: return Centroid(room.Rails[i].Path);
            }
            return Vector2.zero;
        }

        void SetPosition(LevelLint.Part part, int i, Vector2 p)
        {
            switch (part)
            {
                case LevelLint.Part.Block: room.Blocks[i].Center = p; break;
                case LevelLint.Part.Zone: room.Zones[i].Center = p; break;
                case LevelLint.Part.Star: room.Stars[i] = p; break;
                case LevelLint.Part.Hole: room.Hole = p; break;
                case LevelLint.Part.Start: room.Start = p; break;
                case LevelLint.Part.Rail: MoveRail(room.Rails[i], p - Centroid(room.Rails[i].Path)); break;
            }
        }

        // Rails keep their source points (in unflipped space for a mirrored rail); a move shifts those.
        static void MoveRail(RailDef r, Vector2 by)
        {
            var d = r.Mirror ? new Vector2(-by.x, by.y) : by;
            if (r.IsArc) r.ArcCenter += d;
            else for (int k = 0; k < r.Points.Count; k++) r.Points[k] += d;
            r.Rebuild();
        }

        static Vector2 RailPoint(RailDef r, int k) { var p = r.Points[k]; return r.Mirror ? new Vector2(-p.x, p.y) : p; }

        static Vector2 Centroid(List<Vector2> pts)
        {
            if (pts == null || pts.Count == 0) return Vector2.zero;
            var sum = Vector2.zero;
            foreach (var p in pts) sum += p;
            return sum / pts.Count;
        }

        void AddBlock(BlockKind kind, bool post)
        {
            var at = Snapped(worldCenter);
            Change("Add " + kind, () =>
            {
                var b = new BlockDef(kind, at, post ? new Vector2(0.9f, 0.9f) : new Vector2(2.4f, 0.6f)) { Shape = post ? BlockShape.Post : BlockShape.Box };
                if (kind == BlockKind.Crate) b.Size = new Vector2(0.9f, 0.9f);
                if (kind == BlockKind.Gate) b.Group = 0;
                if (kind == BlockKind.PopWall) { b.BlinkPeriod = 2.4f; b.Size = new Vector2(4f, 0.5f); }
                room.Blocks.Add(b);
            });
            Select(LevelLint.Part.Block, room.Blocks.Count - 1);
        }

        void AddZone(ZoneKind kind)
        {
            var at = Snapped(worldCenter);
            Change("Add " + kind, () =>
            {
                bool round = kind == ZoneKind.Portal || kind == ZoneKind.Magnet || kind == ZoneKind.Switch || kind == ZoneKind.Vortex || kind == ZoneKind.Jump;
                var z = new ZoneDef { Kind = kind, Center = at, Size = round ? new Vector2(1.6f, 1.6f) : new Vector2(2.4f, 3f) };
                if (kind == ZoneKind.Portal || kind == ZoneKind.Switch) z.Size = new Vector2(1.1f, 1.1f);
                if (kind == ZoneKind.OneWay) z.Size = new Vector2(2.4f, 0.7f);
                if (kind == ZoneKind.Switch) z.Group = 0;
                if (kind == ZoneKind.Spikes) z.Period = 2.2f;
                room.Zones.Add(z);
                if (kind == ZoneKind.Portal)
                {
                    int a = room.Zones.Count - 1;
                    room.Zones.Add(new ZoneDef { Kind = kind, Center = at + new Vector2(2f, 3f), Size = z.Size, Link = a });
                    z.Link = a + 1;
                }
            });
            Select(LevelLint.Part.Zone, room.Zones.Count - 1);
        }

        void AddRail(bool arc)
        {
            var at = Snapped(worldCenter);
            Change("Add rail", () => room.Rails.Add(arc
                ? RailDef.Around(BlockKind.Wall, at, 2.4f, 20f, 160f)
                : RailDef.Through(BlockKind.Wall, 0.3f, new List<Vector2> { at + new Vector2(-1.5f, 0f), at, at + new Vector2(0f, 1.5f) })));
            Select(LevelLint.Part.Rail, room.Rails.Count - 1);
        }

        void DeleteSelection()
        {
            if (selIndex < 0) return;
            var part = selPart;
            int i = selIndex;
            Change("Delete", () =>
            {
                if (part == LevelLint.Part.Block) room.Blocks.RemoveAt(i);
                else if (part == LevelLint.Part.Rail) room.Rails.RemoveAt(i);
                else if (part == LevelLint.Part.Star) room.Stars.RemoveAt(i);
                else if (part == LevelLint.Part.Zone)
                {
                    // Removing a zone shifts the ones after it: keep portal pairs pointing at the right partner.
                    room.Zones.RemoveAt(i);
                    foreach (var z in room.Zones) { if (z.Link == i) z.Link = -1; else if (z.Link > i) z.Link--; }
                }
            });
            Select(LevelLint.Part.Level, -1);
        }

        string SelectionJson()
        {
            if (selIndex < 0) return null;
            switch (selPart)
            {
                case LevelLint.Part.Block: return "block" + LevelData.BlockJson(room.Blocks[selIndex]);
                case LevelLint.Part.Zone: return "zone" + LevelData.ZoneJson(room.Zones[selIndex]);
                case LevelLint.Part.Rail: return "rail" + LevelData.RailJson(room.Rails[selIndex]);
                case LevelLint.Part.Star: var s = room.Stars[selIndex]; return $"star{{ \"at\": [{LevelData.F(s.x)}, {LevelData.F(s.y)}] }}";
            }
            return null;
        }

        void Paste(string text, Vector2 offset)
        {
            if (text == null) return;
            try
            {
                if (text.StartsWith("block")) { var b = LevelData.ParseBlock(text.Substring(5)); b.Center += offset; Change("Paste", () => room.Blocks.Add(b)); Select(LevelLint.Part.Block, room.Blocks.Count - 1); }
                else if (text.StartsWith("zone")) { var z = LevelData.ParseZone(text.Substring(4)); z.Center += offset; z.Link = -1; Change("Paste", () => room.Zones.Add(z)); Select(LevelLint.Part.Zone, room.Zones.Count - 1); }
                else if (text.StartsWith("rail")) { var r = LevelData.ParseRail(text.Substring(4)); MoveRail(r, offset); Change("Paste", () => room.Rails.Add(r)); Select(LevelLint.Part.Rail, room.Rails.Count - 1); }
                else if (text.StartsWith("star"))
                {
                    var o = Json.Parse(text.Substring(4)) as Dictionary<string, object>;
                    var at = o["at"] as List<object>;
                    var p = new Vector2(((Json.Number)at[0]).Float, ((Json.Number)at[1]).Float) + offset;
                    Change("Paste", () => room.Stars.Add(p));
                    Select(LevelLint.Part.Star, room.Stars.Count - 1);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Level Editor: nothing to paste (" + e.Message + ")"); }
        }

        // ---------- Play and prove ----------

        void PlayRoom()
        {
            if (room == null) return;
            if (dirty) Save();
            if (Levels.IndexOf(room.Id) < 0) { EditorUtility.DisplayDialog("Not in a chapter", "Add the room to a chapter before playing it.", "OK"); return; }
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(OutOfOfficeGame.PlaytestKey, room.Id);
            EditorApplication.isPlaying = true;
        }

        void ProveRoom()
        {
            if (room == null) return;
            if (EditorApplication.isPlaying) { ShowNotification(new GUIContent("Stop Play mode first")); return; }
            EditorUtility.DisplayProgressBar("LevelCheck", $"Solving {room.Id}...", 0.5f);
            try
            {
                report = LevelCheck.Check(room);
                solution = report.Solved ? LevelCheck.Trace(room, report) : null;
                Debug.Log($"HSLEVEL {room.Id} {report.Line}");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Difficulty report", "", "level-difficulty.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var all = Levels.All;
            var csv = new StringBuilder("index,chapter,id,name,difficulty,boss,teaches,tags,par,limit,min shots,suggested par,one-shot win %,hazard %,coffees reached,coffees,ok,seconds\n");
            try
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var d = all[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Difficulty report", $"{i + 1}/{all.Length} {d.Id}", i / (float)all.Length)) break;
                    var r = LevelCheck.Check(d);
                    string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
                    csv.Append($"{i + 1},{Q(Levels.Chapters[Themes.ChapterOf(i)].Key)},{Q(d.Id)},{Q(d.Name)},{d.Difficulty},{d.Boss},{Q(d.Teaches)},{Q(string.Join(" ", d.Tags))},")
                       .Append($"{d.Par},{d.MaxShots},{r.MinShots},{r.SuggestedPar},{r.OneShotWin:0.0},{r.Hazard:0.0},{r.StarsReached},{d.Stars.Count},{r.Ok},{r.Seconds:0.0}\n");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            File.WriteAllText(path, csv.ToString());
            EditorUtility.RevealInFinder(path);
        }

        // ---------- GUI ----------

        void OnGUI()
        {
            if (catalog == null) ReloadCatalog();
            Keys();
            Toolbar();
            var body = new Rect(0f, EditorGUIUtility.singleLineHeight + 6f, position.width, position.height - EditorGUIUtility.singleLineHeight - 6f);
            GUILayout.BeginArea(new Rect(body.x, body.y, LeftWidth, body.height));
            LeftPanel();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(body.xMax - RightWidth, body.y, RightWidth, body.height));
            RightPanel();
            GUILayout.EndArea();
            canvas = new Rect(LeftWidth + 4f, body.y + 4f, body.width - LeftWidth - RightWidth - 8f, body.height - 8f);
            Board();
        }

        void Toolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(room == null ? "No room" : $"{room.Id}{(dirty ? " *" : "")}", EditorStyles.boldLabel, GUILayout.Width(170f));
            using (new EditorGUI.DisabledScope(room == null))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50f))) Save();
                using (new EditorGUI.DisabledScope(!dirty))
                    if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(55f))) { string id = roomId; dirty = false; roomId = null; ReloadCatalog(); OpenRoom(id); }
                if (GUILayout.Button("▶ Play this room", EditorStyles.toolbarButton, GUILayout.Width(110f))) PlayRoom();
                if (GUILayout.Button("Run LevelCheck", EditorStyles.toolbarButton, GUILayout.Width(105f))) ProveRoom();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Snap", GUILayout.Width(34f));
            snap = (Snap)EditorGUILayout.Popup((int)snap, new[] { "off", "0.1", "0.25", "0.5", "1" }, EditorStyles.toolbarPopup, GUILayout.Width(55f));
            grid = GUILayout.Toggle(grid, "Grid", EditorStyles.toolbarButton, GUILayout.Width(40f));
            animate = GUILayout.Toggle(animate, "Animate", EditorStyles.toolbarButton, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
        }

        void LeftPanel()
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            leftScroll = GUILayout.BeginScrollView(leftScroll);
            int number = 0;
            for (int c = 0; c < catalog.Chapters.Count; c++)
            {
                var ch = catalog.Chapters[c];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{c + 1}. {ch.Key}", EditorStyles.boldLabel);
                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f))) NewRoomMenu(c);
                if (GUILayout.Button("⋯", EditorStyles.miniButton, GUILayout.Width(22f))) ChapterMenu(c);
                GUILayout.EndHorizontal();
                for (int i = 0; i < ch.LevelIds.Count; i++)
                {
                    number++;
                    string id = ch.LevelIds[i];
                    catalog.Rooms.TryGetValue(id, out var def);
                    string label = $"{number,3}  {def?.Name ?? id}";
                    if (search.Length > 0 && label.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0 && id.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    GUILayout.BeginHorizontal();
                    bool on = picked.Contains(id);
                    if (GUILayout.Toggle(on, GUIContent.none, GUILayout.Width(14f)) != on) { if (on) picked.Remove(id); else picked.Add(id); }
                    var style = new GUIStyle(EditorStyles.label) { fontStyle = id == roomId ? FontStyle.Bold : FontStyle.Normal };
                    if (def != null && def.Difficulty != Difficulty.Normal) label += def.Difficulty == Difficulty.Hard ? "  [H]" : "  [SH]";
                    if (def != null && def.Boss) label += "  [BOSS]";
                    if (GUILayout.Button(label, style)) OpenRoom(id);
                    if (GUILayout.Button("⋯", EditorStyles.miniButton, GUILayout.Width(22f))) RoomMenu(c, i);
                    GUILayout.EndHorizontal();
                }
            }
            if (GUILayout.Button("Add chapter")) AddChapter();
            GUILayout.EndScrollView();

            GUILayout.Label($"Batch ({picked.Count} picked)", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            bulkTag = EditorGUILayout.TextField(bulkTag);
            using (new EditorGUI.DisabledScope(picked.Count == 0 || bulkTag.Trim().Length == 0))
            {
                if (GUILayout.Button("Tag", GUILayout.Width(40f))) BulkTag(bulkTag.Trim(), true);
                if (GUILayout.Button("Untag", GUILayout.Width(48f))) BulkTag(bulkTag.Trim(), false);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Difficulty report (CSV)...")) ExportCsv();
        }

        void NewRoomMenu(int chapter)
        {
            var menu = new GenericMenu();
            foreach (var t in LevelTemplates.Names) { string tt = t; menu.AddItem(new GUIContent("New room/" + t), false, () => NewRoom(chapter, tt)); }
            if (room != null) menu.AddItem(new GUIContent($"Duplicate '{room.Id}'"), false, () => NewRoom(chapter, null, room));
            menu.ShowAsContext();
        }

        void RoomMenu(int c, int i)
        {
            var ids = catalog.Chapters[c].LevelIds;
            string id = ids[i];
            var menu = new GenericMenu();
            if (i > 0) menu.AddItem(new GUIContent("Move up"), false, () => { (ids[i - 1], ids[i]) = (ids[i], ids[i - 1]); SaveCatalog(); });
            if (i + 1 < ids.Count) menu.AddItem(new GUIContent("Move down"), false, () => { (ids[i + 1], ids[i]) = (ids[i], ids[i + 1]); SaveCatalog(); });
            for (int k = 0; k < catalog.Chapters.Count; k++)
            {
                if (k == c) continue;
                int kk = k;
                menu.AddItem(new GUIContent($"Move to chapter/{k + 1}. {catalog.Chapters[k].Key}"), false, () =>
                {
                    ids.RemoveAt(i);
                    catalog.Chapters[kk].LevelIds.Add(id);
                    SaveCatalog();
                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete room..."), false, () => DeleteRoom(id));
            menu.ShowAsContext();
        }

        void ChapterMenu(int c)
        {
            var chapters = catalog.Chapters;
            var menu = new GenericMenu();
            if (c > 0) menu.AddItem(new GUIContent("Move chapter up"), false, () => { (chapters[c - 1], chapters[c]) = (chapters[c], chapters[c - 1]); SaveCatalog(); });
            if (c + 1 < chapters.Count) menu.AddItem(new GUIContent("Move chapter down"), false, () => { (chapters[c + 1], chapters[c]) = (chapters[c], chapters[c + 1]); SaveCatalog(); });
            foreach (var t in Themes.All)
            {
                string key = t.Key;
                menu.AddItem(new GUIContent("Theme/" + t.Name), chapters[c].ThemeKey == key, () => { chapters[c].ThemeKey = key; SaveCatalog(); });
            }
            if (chapters[c].LevelIds.Count == 0) menu.AddItem(new GUIContent("Remove empty chapter"), false, () => { chapters.RemoveAt(c); SaveCatalog(); });
            menu.ShowAsContext();
        }

        void AddChapter()
        {
            string key = "chapter-" + (catalog.Chapters.Count + 1);
            catalog.Chapters.Add(new ChapterDef { Key = key, ThemeKey = Themes.All[0].Key, Theme = Themes.All[0] });
            SaveCatalog();
        }

        void BulkTag(string tag, bool add)
        {
            if (dirty && picked.Contains(roomId)) Save();
            int changed = 0;
            foreach (var id in picked)
            {
                var def = LevelFiles.ReadRoom(id);
                if (def == null) continue;
                bool has = def.Tags.Contains(tag);
                if (add == has) continue;
                if (add) def.Tags.Add(tag); else def.Tags.Remove(tag);
                LevelFiles.WriteRoom(def);
                changed++;
            }
            LevelFiles.Refresh();
            ReloadCatalog();
            if (roomId != null && picked.Contains(roomId)) { string id = roomId; roomId = null; OpenRoom(id); }
            ShowNotification(new GUIContent($"{(add ? "Tagged" : "Untagged")} {changed} rooms"));
        }

        void RightPanel()
        {
            if (room == null) { GUILayout.Label("Open a room on the left."); return; }
            rightScroll = GUILayout.BeginScrollView(rightScroll);

            GUILayout.Label("Add", EditorStyles.boldLabel);
            Palette();

            GUILayout.Space(6f);
            GUILayout.Label(selPart == LevelLint.Part.Level ? "Room" : $"Selected {selPart.ToString().ToLowerInvariant()} {(selIndex >= 0 ? (selIndex + 1).ToString() : "")}", EditorStyles.boldLabel);
            if (selPart == LevelLint.Part.Block && selIndex >= 0) BlockFields(room.Blocks[selIndex]);
            else if (selPart == LevelLint.Part.Zone && selIndex >= 0) ZoneFields(room.Zones[selIndex], selIndex);
            else if (selPart == LevelLint.Part.Rail && selIndex >= 0) RailFields(room.Rails[selIndex]);
            else if (selPart == LevelLint.Part.Star && selIndex >= 0) { var p = room.Stars[selIndex]; Vec("At", p, v => room.Stars[selIndex] = v); }
            else if (selPart == LevelLint.Part.Hole) Vec("Bed", room.Hole, v => room.Hole = v);
            else if (selPart == LevelLint.Part.Start) Vec("Start", room.Start, v => room.Start = v);
            if (selPart != LevelLint.Part.Level && GUILayout.Button("Back to the room")) Select(LevelLint.Part.Level, -1);
            if (selPart == LevelLint.Part.Level) RoomFields();

            GUILayout.Space(6f);
            int errors = lint.FindAll(p => p.Error).Count;
            GUILayout.Label(lint.Count == 0 ? "No warnings" : $"Warnings ({errors} errors)", EditorStyles.boldLabel);
            foreach (var p in lint)
            {
                var style = new GUIStyle(EditorStyles.helpBox) { richText = true, wordWrap = true };
                if (GUILayout.Button((p.Error ? "<color=#ff6b6b><b>ERROR</b></color>  " : "<color=#e8c25a>warning</color>  ") + p.Text, style))
                    Select(p.Part, p.Index);
            }

            GUILayout.Space(6f);
            GUILayout.Label("LevelCheck", EditorStyles.boldLabel);
            if (report == null) GUILayout.Label("Run LevelCheck to prove this room can be finished.", EditorStyles.wordWrappedMiniLabel);
            else
            {
                EditorGUILayout.HelpBox(report.Solved
                    ? $"{(report.Ok ? "OK" : "PROBLEM")}  in {report.Seconds:0.0}s\nMin shots {report.MinShots}   par {room.Par}   suggested par {report.SuggestedPar}\n" +
                      $"One-shot win {report.OneShotWin:0.0}%   hazard {report.Hazard:0.0}%\nCoffees reached {report.StarsReached}/{room.Stars.Count}   shot limit {room.MaxShots}"
                    : "UNSOLVED: no route to the bed was found within the shot limit.", report.Ok ? MessageType.Info : MessageType.Error);
                GUILayout.Label(report.Line, EditorStyles.wordWrappedMiniLabel);
                if (report.Solved && (room.Par < report.MinShots || room.Par > report.MinShots + 1) && GUILayout.Button($"Use suggested par {report.SuggestedPar}"))
                {
                    int par = report.SuggestedPar;
                    Change("Par", () => room.Par = par);
                }
            }
            GUILayout.EndScrollView();
        }

        void Palette()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Desk")) AddBlock(BlockKind.Wall, false);
            if (GUILayout.Button("Pillar")) AddBlock(BlockKind.Wall, true);
            if (GUILayout.Button("Chair")) AddBlock(BlockKind.Bouncer, true);
            if (GUILayout.Button("Cactus")) AddBlock(BlockKind.Hazard, true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Block...")) { var m = new GenericMenu(); foreach (BlockKind k in System.Enum.GetValues(typeof(BlockKind))) { var kk = k; m.AddItem(new GUIContent("Box/" + k), false, () => AddBlock(kk, false)); m.AddItem(new GUIContent("Post/" + k), false, () => AddBlock(kk, true)); } m.ShowAsContext(); }
            if (GUILayout.Button("Zone...")) { var m = new GenericMenu(); foreach (ZoneKind k in System.Enum.GetValues(typeof(ZoneKind))) { var kk = k; m.AddItem(new GUIContent(k.ToString()), false, () => AddZone(kk)); } m.ShowAsContext(); }
            if (GUILayout.Button("Rail")) AddRail(false);
            if (GUILayout.Button("Arc")) AddRail(true);
            if (GUILayout.Button("Coffee")) { var at = Snapped(worldCenter); Change("Add coffee", () => room.Stars.Add(at)); Select(LevelLint.Part.Star, room.Stars.Count - 1); }
            GUILayout.EndHorizontal();
        }

        void RoomFields()
        {
            Text("Id", room.Id, v => room.Id = v.Trim().ToLowerInvariant());
            if (room.Id != roomId) EditorGUILayout.HelpBox($"Saving renames '{roomId}'. Shipped rooms keep their ids: players' saves use them.", MessageType.Warning);
            Text("Name", room.Name, v => room.Name = v);
            Text("Hint", room.Hint, v => room.Hint = v.ToUpperInvariant());
            Int("Par", room.Par, v => room.Par = Mathf.Max(1, v));
            Int("Shot limit (0 = par + 3)", room.MaxShotsOverride, v => room.WithMaxShots(Mathf.Max(0, v)));
            EditorGUI.BeginChangeCheck();
            var diff = (Difficulty)EditorGUILayout.EnumPopup("Difficulty", room.Difficulty);
            bool boss = EditorGUILayout.Toggle("Boss room", room.Boss);
            if (EditorGUI.EndChangeCheck()) Change("Difficulty", () => { room.Difficulty = diff; room.Boss = boss; });
            var keys = new List<string> { "(none)" };
            foreach (var g in Gadgets.All) keys.Add(g.Key);
            int teach = Mathf.Max(0, keys.IndexOf(room.Teaches));
            EditorGUI.BeginChangeCheck();
            teach = EditorGUILayout.Popup("Teaches", teach, keys.ToArray());
            if (EditorGUI.EndChangeCheck()) { string t = teach == 0 ? "" : keys[teach]; Change("Teaches", () => room.Teaches = t); }
            Text("Tags (space separated)", string.Join(" ", room.Tags), v => { room.Tags.Clear(); foreach (var t in v.Split(' ')) if (t.Length > 0) room.Tags.Add(t); });

            GUILayout.Space(4f);
            GUILayout.Label("Board", EditorStyles.miniBoldLabel);
            var shape = room.Shape;
            int kind = System.Array.IndexOf(ShapeDef.Kinds, shape.Kind);
            EditorGUI.BeginChangeCheck();
            kind = EditorGUILayout.Popup("Outline", Mathf.Max(0, kind), ShapeDef.Kinds);
            string args = EditorGUILayout.DelayedTextField("Numbers", string.Join(" ", System.Array.ConvertAll(shape.Args, LevelData.F)));
            var shift = EditorGUILayout.Vector2Field("Shift", shape.Offset);
            bool flip = EditorGUILayout.Toggle("Mirrored", shape.Flipped);
            if (EditorGUI.EndChangeCheck())
            {
                string k = ShapeDef.Kinds[kind];
                var nums = new List<float>();
                foreach (var part in args.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                    if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) nums.Add(f);
                // Switching kind starts from that kind's defaults.
                var a = k == shape.Kind ? nums.ToArray() : new float[0];
                Change("Outline", () => room.Shape = ShapeDef.Make(k, a, shift, flip));
            }
            EditorGUILayout.LabelField(" ", ShapeHelp(shape.Kind), EditorStyles.wordWrappedMiniLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bed")) Select(LevelLint.Part.Hole, -1);
            if (GUILayout.Button("Start")) Select(LevelLint.Part.Start, -1);
            GUILayout.EndHorizontal();
        }

        static string ShapeHelp(string kind)
        {
            switch (kind)
            {
                case ShapeDef.RectKind: return "width height corner";
                case ShapeDef.StadiumKind: return "width height";
                case ShapeDef.ChamferedKind: return "width height cut corner";
                case ShapeDef.CircleKind: return "radius";
                case ShapeDef.FigureEightKind: return "radius half-spacing waist";
                case ShapeDef.PolygonKind: return "corner, then x y of each corner point";
                default: return "x y of each point";
            }
        }

        void BlockFields(BlockDef b)
        {
            EditorGUI.BeginChangeCheck();
            var kind = (BlockKind)EditorGUILayout.EnumPopup("Kind", b.Kind);
            bool post = EditorGUILayout.Toggle("Round post", b.Shape == BlockShape.Post);
            if (EditorGUI.EndChangeCheck()) Change("Kind", () => { b.Kind = kind; b.Shape = post ? BlockShape.Post : BlockShape.Box; if (post) b.Size = new Vector2(b.Size.x, b.Size.x); });
            Vec("At", b.Center, v => b.Center = v);
            if (b.Shape == BlockShape.Post) Float("Diameter", b.Size.x, v => b.Size = new Vector2(Mathf.Max(0.1f, v), Mathf.Max(0.1f, v)));
            else
            {
                Vec("Size", b.Size, v => b.Size = new Vector2(Mathf.Max(0.1f, v.x), Mathf.Max(0.1f, v.y)));
                Float("Angle", b.Angle, v => b.Angle = v);
                Float("Corner (-1 auto)", b.Corner, v => b.Corner = v);
            }
            GUILayout.Label("Motion", EditorStyles.miniBoldLabel);
            Vec("Swing offset", b.MoveOffset, v => b.MoveOffset = v);
            Float("Swing period (s)", b.Period, v => b.Period = Mathf.Max(0f, v));
            Float("Swing phase (rad)", b.Phase, v => b.Phase = v);
            Float("Spin (deg/s)", b.Spin, v => b.Spin = v);
            if (b.Kind == BlockKind.Gate) Int("Group", b.Group, v => b.Group = v);
            if (b.Kind == BlockKind.PopWall)
            {
                Float("Pop period (s)", b.BlinkPeriod, v => b.BlinkPeriod = Mathf.Max(0f, v));
                Float("Pop phase (0-1)", b.BlinkPhase, v => b.BlinkPhase = v);
            }
            if (b.Reception != 0) EditorGUILayout.HelpBox("The receptionist's desk: decoration in a top corner. One per chapter.", MessageType.None);
        }

        void ZoneFields(ZoneDef z, int index)
        {
            EditorGUI.BeginChangeCheck();
            var kind = (ZoneKind)EditorGUILayout.EnumPopup("Kind", z.Kind);
            if (EditorGUI.EndChangeCheck()) Change("Kind", () => z.Kind = kind);
            Vec("At", z.Center, v => z.Center = v);
            Vec("Size (across, along)", z.Size, v => z.Size = new Vector2(Mathf.Max(0.1f, v.x), Mathf.Max(0.1f, v.y)));
            Float("Angle (0 = up)", z.Angle, v => z.Angle = v);
            if (z.Kind == ZoneKind.Portal)
            {
                Int("Paired with zone", z.Link + 1, v => z.Link = v - 1);
                if (z.Link >= 0 && z.Link < room.Zones.Count && GUILayout.Button("Select its pair")) Select(LevelLint.Part.Zone, z.Link);
            }
            if (z.Kind == ZoneKind.Switch) Int("Group", z.Group, v => z.Group = v);
            if (z.Kind == ZoneKind.Vortex)
            {
                EditorGUI.BeginChangeCheck();
                bool cw = EditorGUILayout.Toggle("Clockwise", z.Turn < 0f);
                if (EditorGUI.EndChangeCheck()) Change("Turn", () => z.Turn = cw ? -1f : 1f);
            }
            if (z.Kind == ZoneKind.Spikes)
            {
                Float("Period (s)", z.Period, v => z.Period = Mathf.Max(0f, v));
                Float("Phase (0-1)", z.Phase, v => z.Phase = v);
            }
        }

        void RailFields(RailDef r)
        {
            EditorGUI.BeginChangeCheck();
            var kind = (BlockKind)EditorGUILayout.EnumPopup("Kind", r.Kind);
            float width = EditorGUILayout.FloatField("Width", r.Width);
            bool mirror = EditorGUILayout.Toggle("Mirrored", r.Mirror);
            if (EditorGUI.EndChangeCheck()) Change("Rail", () => { r.Kind = kind; r.Width = Mathf.Max(0.1f, width); r.Mirror = mirror; r.Rebuild(); });
            if (r.IsArc)
            {
                Vec("Center", r.ArcCenter, v => { r.ArcCenter = v; r.Rebuild(); });
                Float("Radius", r.ArcRadius, v => { r.ArcRadius = Mathf.Max(0.2f, v); r.Rebuild(); });
                Float("From (deg)", r.ArcFrom, v => { r.ArcFrom = v; r.Rebuild(); });
                Float("To (deg)", r.ArcTo, v => { r.ArcTo = v; r.Rebuild(); });
            }
            else
            {
                if (r.Mirror) EditorGUILayout.HelpBox("Mirrored: the points below are before the flip.", MessageType.None);
                Float("Bend", r.Bend, v => { r.Bend = Mathf.Max(0f, v); r.Rebuild(); });
                for (int k = 0; k < r.Points.Count; k++)
                {
                    int kk = k;
                    GUILayout.BeginHorizontal();
                    Vec($"Point {k + 1}", r.Points[k], v => { r.Points[kk] = v; r.Rebuild(); });
                    if (r.Points.Count > 2 && GUILayout.Button("x", GUILayout.Width(20f))) { Change("Remove point", () => { r.Points.RemoveAt(kk); r.Rebuild(); }); GUILayout.EndHorizontal(); break; }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Add point")) Change("Add point", () => { var last = r.Points[r.Points.Count - 1]; r.Points.Add(last + new Vector2(0f, 1f)); r.Rebuild(); });
            }
        }

        void Vec(string label, Vector2 value, System.Action<Vector2> set)
        {
            EditorGUI.BeginChangeCheck();
            var v = EditorGUILayout.Vector2Field(label, value);
            if (EditorGUI.EndChangeCheck()) Change(label, () => set(v));
        }

        void Float(string label, float value, System.Action<float> set)
        {
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.FloatField(label, value);
            if (EditorGUI.EndChangeCheck()) Change(label, () => set(v));
        }

        void Int(string label, int value, System.Action<int> set)
        {
            EditorGUI.BeginChangeCheck();
            int v = EditorGUILayout.IntField(label, value);
            if (EditorGUI.EndChangeCheck()) Change(label, () => set(v));
        }

        void Text(string label, string value, System.Action<string> set)
        {
            EditorGUI.BeginChangeCheck();
            string v = EditorGUILayout.DelayedTextField(label, value ?? "");
            if (EditorGUI.EndChangeCheck()) Change(label, () => set(v));
        }

        // ---------- Keyboard ----------

        void Keys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || room == null) return;
            bool ctrl = e.control || e.command;
            if (ctrl && e.keyCode == KeyCode.S) { Save(); e.Use(); return; }
            if (GUIUtility.keyboardControl != 0) return;  // typing in a field
            float step = Mathf.Max(0.05f, SnapSize[(int)snap]);
            Vector2 nudge = e.keyCode == KeyCode.LeftArrow ? Vector2.left : e.keyCode == KeyCode.RightArrow ? Vector2.right
                : e.keyCode == KeyCode.UpArrow ? Vector2.up : e.keyCode == KeyCode.DownArrow ? Vector2.down : Vector2.zero;
            if (nudge != Vector2.zero && selPart != LevelLint.Part.Level)
            {
                var part = selPart; int i = selIndex;
                Change("Nudge", () => SetPosition(part, i, Position(part, i) + nudge * step));
                e.Use();
            }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && selIndex >= 0) { DeleteSelection(); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.D && selIndex >= 0) { Paste(SelectionJson(), new Vector2(0.5f, 0.5f)); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.C && selIndex >= 0) { EditorGUIUtility.systemCopyBuffer = ClipPrefix + SelectionJson(); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.V)
            {
                string clip = EditorGUIUtility.systemCopyBuffer;
                if (clip != null && clip.StartsWith(ClipPrefix)) Paste(clip.Substring(ClipPrefix.Length), new Vector2(0.5f, 0.5f));
                e.Use();
            }
            else if (e.keyCode == KeyCode.R && !ctrl && (selPart == LevelLint.Part.Block || selPart == LevelLint.Part.Zone) && selIndex >= 0)
            {
                float by = e.shift ? -15f : 15f;
                var part = selPart; int i = selIndex;
                Change("Rotate", () =>
                {
                    if (part == LevelLint.Part.Block) room.Blocks[i].Angle = Mathf.Repeat(room.Blocks[i].Angle + by + 180f, 360f) - 180f;
                    else room.Zones[i].Angle = Mathf.Repeat(room.Zones[i].Angle + by + 180f, 360f) - 180f;
                });
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape) { Select(LevelLint.Part.Level, -1); e.Use(); }
            if (e.type == EventType.Used) Repaint();
        }

        // ---------- The board ----------

        Vector2 ToScreen(Vector2 w) => new Vector2(canvas.center.x + (w.x - worldCenter.x) * scale, canvas.center.y - (w.y - worldCenter.y) * scale);
        Vector2 ToWorld(Vector2 s) => new Vector2(worldCenter.x + (s.x - canvas.center.x) / scale, worldCenter.y - (s.y - canvas.center.y) / scale);
        Vector3 S(Vector2 w) => ToScreen(w);

        static readonly Color Floor = new Color(0.93f, 0.9f, 0.84f), Ink = new Color(0.16f, 0.18f, 0.24f), Highlight = new Color(1f, 0.84f, 0.1f);

        static Color KindColor(BlockKind k)
        {
            switch (k)
            {
                case BlockKind.Hazard: return new Color(0.32f, 0.68f, 0.3f);
                case BlockKind.Bouncer: return new Color(0.95f, 0.45f, 0.35f);
                case BlockKind.Sticky: return new Color(0.75f, 0.45f, 0.85f);
                case BlockKind.Gate: return new Color(0.95f, 0.75f, 0.2f);
                case BlockKind.Crate: return new Color(0.78f, 0.58f, 0.36f);
                case BlockKind.Breakable: return new Color(0.6f, 0.85f, 0.95f);
                case BlockKind.Kicker: return new Color(0.85f, 0.15f, 0.2f);
                case BlockKind.PopWall: return new Color(0.45f, 0.5f, 0.7f);
                default: return new Color(0.42f, 0.36f, 0.3f);
            }
        }

        static Color ZoneColor(ZoneKind k)
        {
            switch (k)
            {
                case ZoneKind.Boost: return new Color(1f, 0.6f, 0.1f, 0.45f);
                case ZoneKind.Mud: return new Color(0.45f, 0.3f, 0.15f, 0.45f);
                case ZoneKind.Ice: return new Color(0.6f, 0.9f, 1f, 0.5f);
                case ZoneKind.Gap: return new Color(0.05f, 0.05f, 0.1f, 0.65f);
                case ZoneKind.Portal: return new Color(0.55f, 0.35f, 1f, 0.55f);
                case ZoneKind.Magnet: return new Color(0.9f, 0.2f, 0.3f, 0.25f);
                case ZoneKind.Switch: return new Color(0.95f, 0.75f, 0.2f, 0.7f);
                case ZoneKind.Spikes: return new Color(0.5f, 0.5f, 0.55f, 0.6f);
                case ZoneKind.Jump: return new Color(0.2f, 0.7f, 1f, 0.5f);
                case ZoneKind.Vortex: return new Color(0.3f, 0.8f, 0.7f, 0.4f);
                case ZoneKind.Wind: return new Color(0.8f, 0.9f, 1f, 0.45f);
                case ZoneKind.OneWay: return new Color(0.3f, 0.9f, 0.4f, 0.55f);
                default: return new Color(0.5f, 0.6f, 0.9f, 0.4f);
            }
        }

        static bool Round(ZoneKind k) => k == ZoneKind.Portal || k == ZoneKind.Magnet || k == ZoneKind.Switch || k == ZoneKind.Vortex || k == ZoneKind.Jump;
        static bool Directed(ZoneKind k) => k == ZoneKind.Boost || k == ZoneKind.Conveyor || k == ZoneKind.Wind || k == ZoneKind.Ramp || k == ZoneKind.OneWay;

        void Board()
        {
            EditorGUI.DrawRect(canvas, new Color(0.2f, 0.22f, 0.27f));
            if (room == null) return;
            var bounds = Geometry.Bounds(room.Shape.Outline);
            worldCenter = bounds.center;
            scale = Mathf.Min((canvas.width - 40f) / bounds.width, (canvas.height - 40f) / bounds.height);
            float t = animate ? (float)EditorApplication.timeSinceStartup : 0f;

            var e = Event.current;
            if (canvas.Contains(e.mousePosition)) Mouse(e);
            if (e.type != EventType.Repaint) return;

            GUI.BeginClip(canvas);
            var offset = canvas.position;
            canvas.position = Vector2.zero;
            try
            {
                var outline = room.Shape.Outline;
                var pts = new Vector3[outline.Count + 1];
                for (int i = 0; i < outline.Count; i++) pts[i] = S(outline[i]);
                pts[outline.Count] = pts[0];
                FillPolygon(outline, Floor);
                if (grid) Grid(bounds);
                Handles.color = Ink;
                Handles.DrawAAPolyLine(4f, pts);

                foreach (var br in room.Bridges) Quad(br.center, br.size, 0f, new Color(0.7f, 0.55f, 0.35f, 0.8f));
                for (int i = 0; i < room.Zones.Count; i++) DrawZone(room.Zones[i], i, t);
                for (int i = 0; i < room.Rails.Count; i++) DrawRail(room.Rails[i], i);
                for (int i = 0; i < room.Blocks.Count; i++) DrawBlock(room.Blocks[i], i, t);

                Disc(room.Hole, Rules.HoleRadius, new Color(0.12f, 0.1f, 0.14f), selPart == LevelLint.Part.Hole);
                Label(room.Hole, "BED", Color.white);
                for (int i = 0; i < room.Stars.Count; i++) Disc(room.Stars[i], 0.3f, new Color(0.55f, 0.32f, 0.16f), selPart == LevelLint.Part.Star && selIndex == i);
                Disc(room.Start, Rules.PuckRadius, Color.white, selPart == LevelLint.Part.Start);
                Ring(room.Start, Rules.PuckRadius, Ink, 2f);

                if (solution != null)
                    for (int s = 0; s < solution.Count; s++)
                    {
                        var path = solution[s];
                        var line = new Vector3[path.Count];
                        for (int k = 0; k < path.Count; k++) line[k] = S(path[k]);
                        Handles.color = Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(0.9f, 0.1f, 0.6f), solution.Count > 1 ? s / (solution.Count - 1f) : 0f);
                        Handles.DrawAAPolyLine(3f, line);
                        Label(path[0] + new Vector2(0.35f, 0.35f), (s + 1).ToString(), Handles.color);
                    }

                foreach (var p in lint)
                    if (p.Error && p.Part != LevelLint.Part.Level && (p.Index >= 0 || p.Part == LevelLint.Part.Hole || p.Part == LevelLint.Part.Start))
                        Ring(SafePosition(p.Part, p.Index), 0.55f, new Color(1f, 0.25f, 0.25f), 3f);

                SelectionHandles();
            }
            finally
            {
                canvas.position = offset;
                GUI.EndClip();
            }
        }

        Vector2 SafePosition(LevelLint.Part part, int i)
        {
            try { return Position(part, i); } catch { return room.Hole; }
        }

        void Grid(Rect b)
        {
            for (float x = Mathf.Floor(b.xMin); x <= b.xMax; x += 0.5f)
            {
                Handles.color = new Color(0f, 0f, 0f, Mathf.Approximately(x, Mathf.Round(x)) ? (x == 0f ? 0.25f : 0.12f) : 0.05f);
                Handles.DrawLine(S(new Vector2(x, b.yMin)), S(new Vector2(x, b.yMax)));
            }
            for (float y = Mathf.Floor(b.yMin); y <= b.yMax; y += 0.5f)
            {
                Handles.color = new Color(0f, 0f, 0f, Mathf.Approximately(y, Mathf.Round(y)) ? (y == 0f ? 0.25f : 0.12f) : 0.05f);
                Handles.DrawLine(S(new Vector2(b.xMin, y)), S(new Vector2(b.xMax, y)));
            }
        }

        List<Vector2> fillFor, fillVerts;
        static Material fillMaterial;

        static void UseFill()
        {
            if (fillMaterial == null)
            {
                fillMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                fillMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                fillMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            fillMaterial.SetPass(0);
        }
        readonly List<int> fillTris = new List<int>();

        void FillPolygon(List<Vector2> outline, Color c)
        {
            if (fillFor != outline)
            {
                fillTris.Clear();
                fillVerts = Geometry.Triangulate(outline, null, fillTris);
                fillFor = outline;
            }
            var tris = fillTris;
            var verts = fillVerts;
            // Plain triangles: anti-aliased ones would leave hairline seams along every shared edge.
            UseFill();
            GL.Begin(GL.TRIANGLES);
            GL.Color(c);
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                GL.Vertex(S(verts[tris[i]]));
                GL.Vertex(S(verts[tris[i + 1]]));
                GL.Vertex(S(verts[tris[i + 2]]));
            }
            GL.End();
        }

        void DrawBlock(BlockDef b, int i, float t)
        {
            Vector2 at = b.Center;
            float angle = b.Angle;
            if (b.Period > 0f)
            {
                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawDottedLine(S(b.Center - b.MoveOffset), S(b.Center + b.MoveOffset), 3f);
                at += b.MoveOffset * Mathf.Sin(t * Mathf.PI * 2f / b.Period + b.Phase);
            }
            angle += b.Spin * t;
            var c = KindColor(b.Kind);
            if (b.Kind == BlockKind.PopWall && b.BlinkPeriod > 0f && Mathf.Repeat(t / b.BlinkPeriod + b.BlinkPhase, 1f) >= 0.5f) c.a = 0.3f;
            if (b.Reception != 0) c = new Color(0.55f, 0.5f, 0.62f);
            bool on = selPart == LevelLint.Part.Block && selIndex == i;
            if (b.Shape == BlockShape.Post) Disc(at, b.Size.x * 0.5f, c, on);
            else Quad(at, b.Size, angle, c, on);
            if (b.Kind == BlockKind.Gate || (b.Kind == BlockKind.Crate)) Label(at, b.Kind == BlockKind.Gate ? "G" + b.Group : "", Color.black);
        }

        void DrawZone(ZoneDef z, int i, float t)
        {
            var c = ZoneColor(z.Kind);
            if (z.Kind == ZoneKind.Spikes && z.Period > 0f && Mathf.Repeat(t / z.Period + z.Phase, 1f) >= Rules.SpikeDuty) c.a *= 0.35f;
            bool on = selPart == LevelLint.Part.Zone && selIndex == i;
            if (Round(z.Kind)) Disc(z.Center, z.Size.x * 0.5f, c, on);
            else Quad(z.Center, z.Size, z.Angle, c, on);
            if (Directed(z.Kind) || z.Kind == ZoneKind.Portal)
            {
                Vector2 dir = z.Direction * Mathf.Max(0.5f, (Round(z.Kind) ? z.Size.x : z.Size.y) * 0.35f);
                Handles.color = new Color(0f, 0f, 0f, 0.6f);
                Handles.DrawAAPolyLine(3f, S(z.Center - dir), S(z.Center + dir));
                Vector2 side = new Vector2(-dir.y, dir.x) * 0.35f;
                Handles.DrawAAPolyLine(3f, S(z.Center + dir - dir * 0.35f + side), S(z.Center + dir), S(z.Center + dir - dir * 0.35f - side));
            }
            if (z.Kind == ZoneKind.Portal && z.Link > i && z.Link < room.Zones.Count)
            {
                Handles.color = new Color(0.55f, 0.35f, 1f, 0.8f);
                Handles.DrawDottedLine(S(z.Center), S(room.Zones[z.Link].Center), 4f);
            }
            if (z.Kind == ZoneKind.Switch) Label(z.Center, "S" + z.Group, Color.black);
            if (z.Kind == ZoneKind.Vortex) Label(z.Center, z.Turn < 0f ? "↻" : "↺", Color.black);
        }

        void DrawRail(RailDef r, int i)
        {
            if (r.Path == null || r.Path.Count < 2) return;
            bool on = selPart == LevelLint.Part.Rail && selIndex == i;
            if (on) Rod(r.Path, r.Width * 0.5f + 4f / scale, Highlight);
            Rod(r.Path, r.Width * 0.5f, KindColor(r.Kind));
        }

        // A solid rod of the given half width along a path: a quad per segment and a disc at every joint, so it reads
        // crisp at any zoom (a wide anti-aliased line goes soft).
        void Rod(List<Vector2> path, float half, Color c)
        {
            UseFill();
            GL.Begin(GL.QUADS);
            GL.Color(c);
            for (int k = 1; k < path.Count; k++)
            {
                Vector2 a = path[k - 1], b = path[k], d = b - a;
                if (d.sqrMagnitude < 1e-8f) continue;
                Vector2 n = new Vector2(-d.y, d.x).normalized * half;
                GL.Vertex(S(a + n)); GL.Vertex(S(b + n)); GL.Vertex(S(b - n)); GL.Vertex(S(a - n));
            }
            GL.End();
            Handles.color = c;
            foreach (var p in path) Handles.DrawSolidDisc(S(p), Vector3.forward, half * scale);
        }

        void Quad(Vector2 at, Vector2 size, float angle, Color c, bool on = false)
        {
            var rot = Quaternion.Euler(0f, 0f, angle);
            Vector2 h = size * 0.5f;
            var corners = new[]
            {
                S(at + (Vector2)(rot * new Vector2(-h.x, -h.y))), S(at + (Vector2)(rot * new Vector2(h.x, -h.y))),
                S(at + (Vector2)(rot * new Vector2(h.x, h.y))), S(at + (Vector2)(rot * new Vector2(-h.x, h.y))),
            };
            Handles.color = c;
            Handles.DrawAAConvexPolygon(corners);
            Handles.color = on ? Highlight : new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawAAPolyLine(on ? 4f : 1.5f, corners[0], corners[1], corners[2], corners[3], corners[0]);
        }

        void Disc(Vector2 at, float radius, Color c, bool on = false)
        {
            Handles.color = c;
            Handles.DrawSolidDisc(S(at), Vector3.forward, radius * scale);
            if (on) Ring(at, radius, Highlight, 4f);
        }

        void Ring(Vector2 at, float radius, Color c, float width)
        {
            Handles.color = c;
            Handles.DrawWireDisc(S(at), Vector3.forward, radius * scale, width);
        }

        void Label(Vector2 at, string text, Color c)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = c } };
            var p = ToScreen(at);
            GUI.Label(new Rect(p.x - 30f, p.y - 9f, 60f, 18f), text, style);
        }

        // The handle that resizes the selection, in world space; null when it has none.
        Vector2? ResizeHandle()
        {
            if (selIndex < 0) return null;
            if (selPart == LevelLint.Part.Block)
            {
                var b = room.Blocks[selIndex];
                if (b.Shape == BlockShape.Post) return b.Center + new Vector2(b.Size.x * 0.5f, 0f);
                return b.Center + (Vector2)(Quaternion.Euler(0f, 0f, b.Angle) * (b.Size * 0.5f));
            }
            if (selPart == LevelLint.Part.Zone)
            {
                var z = room.Zones[selIndex];
                if (Round(z.Kind)) return z.Center + new Vector2(z.Size.x * 0.5f, 0f);
                return z.Center + (Vector2)(Quaternion.Euler(0f, 0f, z.Angle) * (z.Size * 0.5f));
            }
            if (selPart == LevelLint.Part.Rail && room.Rails[selIndex].IsArc)
            {
                var r = room.Rails[selIndex];
                var c = r.Mirror ? new Vector2(-r.ArcCenter.x, r.ArcCenter.y) : r.ArcCenter;
                return c + new Vector2(r.ArcRadius * (r.Mirror ? -1f : 1f), 0f);
            }
            return null;
        }

        void SelectionHandles()
        {
            var h = ResizeHandle();
            if (h.HasValue)
            {
                var p = ToScreen(h.Value);
                EditorGUI.DrawRect(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Highlight);
            }
            if (selPart == LevelLint.Part.Rail && selIndex >= 0 && !room.Rails[selIndex].IsArc)
            {
                var r = room.Rails[selIndex];
                for (int k = 0; k < r.Points.Count; k++)
                {
                    var p = ToScreen(RailPoint(r, k));
                    EditorGUI.DrawRect(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), k == selPoint ? Color.white : Highlight);
                }
            }
        }

        void Mouse(Event e)
        {
            Vector2 w = ToWorld(e.mousePosition);
            float grab = 8f / scale;  // eight pixels, in world units
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    GUI.FocusControl(null);
                    dragGroup = Undo.GetCurrentGroup();
                    var h = ResizeHandle();
                    if (h.HasValue && (w - h.Value).magnitude < grab * 1.5f) { drag = Drag.Resize; e.Use(); return; }
                    if (selPart == LevelLint.Part.Rail && selIndex >= 0 && !room.Rails[selIndex].IsArc)
                    {
                        var r = room.Rails[selIndex];
                        for (int k = 0; k < r.Points.Count; k++)
                            if ((RailPoint(r, k) - w).magnitude < grab * 1.5f) { selPoint = k; drag = Drag.Point; e.Use(); return; }
                    }
                    Pick(w, grab);
                    drag = selPart == LevelLint.Part.Level ? Drag.None : Drag.Move;
                    if (drag == Drag.Move) dragGrab = Position(selPart, selIndex) - w;
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDrag when e.button == 0 && drag != Drag.None:
                {
                    var part = selPart; int i = selIndex; var mode = drag; int point = selPoint;
                    Change("Drag", () =>
                    {
                        if (mode == Drag.Move) SetPosition(part, i, Snapped(w + dragGrab));
                        else if (mode == Drag.Point)
                        {
                            var r = room.Rails[i];
                            var p = Snapped(w);
                            r.Points[point] = r.Mirror ? new Vector2(-p.x, p.y) : p;
                            r.Rebuild();
                        }
                        else Resize(part, i, w);
                    });
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseUp when e.button == 0:
                    if (drag != Drag.None) Undo.CollapseUndoOperations(dragGroup);
                    drag = Drag.None;
                    e.Use();
                    break;
            }
        }

        void Resize(LevelLint.Part part, int i, Vector2 w)
        {
            if (part == LevelLint.Part.Block)
            {
                var b = room.Blocks[i];
                if (b.Shape == BlockShape.Post) { float d = Mathf.Max(0.2f, Snapped(2f * (w - b.Center).magnitude)); b.Size = new Vector2(d, d); return; }
                Vector2 local = Quaternion.Euler(0f, 0f, -b.Angle) * (w - b.Center);
                b.Size = new Vector2(Mathf.Max(0.2f, Snapped(2f * Mathf.Abs(local.x))), Mathf.Max(0.2f, Snapped(2f * Mathf.Abs(local.y))));
            }
            else if (part == LevelLint.Part.Zone)
            {
                var z = room.Zones[i];
                if (Round(z.Kind)) { float d = Mathf.Max(0.3f, Snapped(2f * (w - z.Center).magnitude)); z.Size = new Vector2(d, d); return; }
                Vector2 local = Quaternion.Euler(0f, 0f, -z.Angle) * (w - z.Center);
                z.Size = new Vector2(Mathf.Max(0.3f, Snapped(2f * Mathf.Abs(local.x))), Mathf.Max(0.3f, Snapped(2f * Mathf.Abs(local.y))));
            }
            else if (part == LevelLint.Part.Rail)
            {
                var r = room.Rails[i];
                var c = r.Mirror ? new Vector2(-r.ArcCenter.x, r.ArcCenter.y) : r.ArcCenter;
                r.ArcRadius = Mathf.Max(0.3f, Snapped((w - c).magnitude));
                r.Rebuild();
            }
        }

        // Top-most thing under the cursor: the puck and the bed, coffees, then blocks, rails and zones.
        void Pick(Vector2 w, float grab)
        {
            if ((w - room.Start).magnitude < Rules.PuckRadius + grab) { Select(LevelLint.Part.Start, -1); return; }
            for (int i = room.Stars.Count - 1; i >= 0; i--) if ((w - room.Stars[i]).magnitude < 0.3f + grab) { Select(LevelLint.Part.Star, i); return; }
            if ((w - room.Hole).magnitude < Rules.HoleRadius) { Select(LevelLint.Part.Hole, -1); return; }
            for (int i = room.Blocks.Count - 1; i >= 0; i--) if (LevelLint.Covers(room.Blocks[i], w, grab * 0.5f)) { Select(LevelLint.Part.Block, i); return; }
            for (int i = room.Rails.Count - 1; i >= 0; i--)
            {
                var path = room.Rails[i].Path;
                for (int k = 1; k < path.Count; k++)
                    if (LevelLint.DistanceToSegment(w, path[k - 1], path[k]) < room.Rails[i].Width * 0.5f + grab) { Select(LevelLint.Part.Rail, i); return; }
            }
            for (int i = room.Zones.Count - 1; i >= 0; i--)
            {
                var z = room.Zones[i];
                bool hit = Round(z.Kind) ? (w - z.Center).magnitude < z.Size.x * 0.5f
                    : LevelLint.Covers(new BlockDef(BlockKind.Wall, z.Center, z.Size) { Angle = z.Angle }, w, 0f);
                if (hit) { Select(LevelLint.Part.Zone, i); return; }
            }
            Select(LevelLint.Part.Level, -1);
        }

        // ---------- Self-test ----------

        // A designer's whole loop, driven through this window (OUT-12): a new room from a template, a piece added and
        // undone, a save, LevelCheck with its solution replayed into the bed, then the room removed again. The catalog
        // and room files are put back exactly as they were. Part of Run All Checks.
        public static int SelfTest()
        {
            int failures = 0;
            void Report(string name, bool ok, string detail)
            {
                if (!ok) failures++;
                Debug.Log($"HSEDITOR {name} {(ok ? "PASS" : "FAIL")} {detail}");
            }

            string catalogBefore = File.ReadAllText(LevelFiles.CatalogFile);
            var w = CreateInstance<LevelEditorWindow>();
            string id = null;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                int chapter = w.catalog.Chapters.Count - 1;
                w.NewRoom(chapter, "blank");
                id = w.room.Id;
                Report("new-room", File.Exists(LevelFiles.RoomPath(id)) && Levels.IndexOf(id) == Levels.All.Length - 1 && !w.dirty,
                    $"'{id}' from the blank template is the last room of chapter {chapter + 1}");

                int blocks = w.room.Blocks.Count;
                w.AddBlock(BlockKind.Bouncer, true);
                bool added = w.room.Blocks.Count == blocks + 1 && w.dirty && w.selPart == LevelLint.Part.Block;
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Report("undo", added && w.room.Blocks.Count == blocks, $"add a chair: {blocks} -> {blocks + 1} blocks, undo -> {w.room.Blocks.Count}");

                w.SetPosition(LevelLint.Part.Star, 0, new Vector2(-3.4f, -1f));
                w.Commit();
                w.Save();
                var reread = LevelFiles.ReadRoom(id);
                Report("save", !w.dirty && reread != null && LevelFiles.Diff(reread, w.room) == null && reread.Stars[0] == new Vector2(-3.4f, -1f),
                    "the moved coffee is in the saved file, bit for bit");

                w.ProveRoom();
                var r = w.report;
                var last = w.solution != null && w.solution.Count > 0 ? w.solution[w.solution.Count - 1] : null;
                bool sunk = last != null && last.Count > 1 && (last[last.Count - 1] - w.room.Hole).magnitude < Rules.HoleRadius + 0.3f;
                Report("levelcheck", r != null && r.Solved && sunk && r.Seconds < 10.0,
                    r == null ? "no report" : $"min shots {r.MinShots}, suggested par {r.SuggestedPar}, {r.Solution.Count} shot solution ends in the bed: {sunk}, {r.Seconds:0.0}s");
            }
            catch (System.Exception e)
            {
                Report("loop", false, e.ToString());
            }
            finally
            {
                if (id != null && File.Exists(LevelFiles.RoomPath(id))) AssetDatabase.DeleteAsset(LevelFiles.RoomPath(id));
                File.WriteAllText(LevelFiles.CatalogFile, catalogBefore);
                w.dirty = false;
                DestroyImmediate(w);
                LevelFiles.Refresh();
            }
            Report("cleanup", File.ReadAllText(LevelFiles.CatalogFile) == catalogBefore && (id == null || !File.Exists(LevelFiles.RoomPath(id))) && Levels.IndexOf(id) < 0,
                $"catalog and rooms restored; whole loop {clock.Elapsed.TotalSeconds:0.0}s");
            Debug.Log($"HSEDITOR done, failures: {failures}");
            return failures;
        }
    }
}
