using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Every gadget the rooms use, for the "NEW IN THE OFFICE" intro cards and the field guide (OUT-11): its name, one
    // line on what it does, how to spot it in a room, and a tiny demo room it is shown in.
    public class Gadget
    {
        public string Key, Name, Text;
        public Func<LevelDef, bool> In;   // does this room use it?
        public Func<LevelDef> Demo;        // a small room with just this gadget, for its preview
    }

    public static class Gadgets
    {
        static bool Block(LevelDef d, BlockKind k) => d.Blocks.Exists(b => b.Kind == k && b.Reception == 0) || d.Rails.Exists(r => r.Kind == k);
        static bool Zone(LevelDef d, ZoneKind k) => d.Zones.Exists(z => z.Kind == k);

        // A small room, the start low and the bed high, both outside the preview's frame: only the gadget shows.
        static LevelDef Room(string key) => new LevelDef("demo-" + key, key, 0f, 3.1f).WithShape(ShapeDef.Rect(6.4f, 7.6f, 1.2f)).StartAt(0f, -3.2f);

        public static readonly Gadget[] All =
        {
            new Gadget { Key = "bouncer", Name = "BOUNCY CHAIR", Text = "Springs you off at speed. Bank shots welcome.",
                In = d => Block(d, BlockKind.Bouncer), Demo = () => Room("bouncer").Post(BlockKind.Bouncer, -0.9f, 0.2f, 0.55f).Post(BlockKind.Bouncer, 1f, -0.3f, 0.45f) },
            new Gadget { Key = "hazard", Name = "CACTUS", Text = "One touch and you're back where the shot began. The shot still counts.",
                In = d => Block(d, BlockKind.Hazard), Demo = () => Room("hazard").SpikeBall(0f, 0f, 0.5f).Wall(1.6f, -0.4f, 0.6f, 1.6f) },
            new Gadget { Key = "sticky", Name = "STICKY DESK", Text = "Kills your bounce dead. You stop where you hit it.",
                In = d => Block(d, BlockKind.Sticky), Demo = () => Room("sticky").Sticky(0f, 0f, 3.2f, 0.7f) },
            new Gadget { Key = "gate", Name = "BADGE READER", Text = "Roll over the reader and its gate opens.",
                In = d => Zone(d, ZoneKind.Switch) || Block(d, BlockKind.Gate), Demo = () => Room("gate").Switch(0, -1.2f, -0.7f).Gate(0, 0.9f, 0.6f, 2.6f, 0.45f) },
            new Gadget { Key = "crate", Name = "PARCELS", Text = "Shove them aside. They're heavy, so hit them hard.",
                In = d => Block(d, BlockKind.Crate), Demo = () => Room("crate").Crate(-0.5f, 0f, 0.9f).Crate(0.7f, 0.4f, 0.9f) },
            new Gadget { Key = "breakable", Name = "GLASS WALL", Text = "Hit it hard enough and it shatters.",
                In = d => Block(d, BlockKind.Breakable), Demo = () => Room("breakable").Breakable(0f, 0f, 3.6f, 0.6f) },
            new Gadget { Key = "kicker", Name = "BOSS CHAIR", Text = "Launches you away at full speed. Aim off it on purpose.",
                In = d => Block(d, BlockKind.Kicker), Demo = () => Room("kicker").Kicker(0f, 0f, 0.6f) },
            new Gadget { Key = "popwall", Name = "POP-UP WALL", Text = "Rises and sinks on a beat. Time your shot.",
                In = d => Block(d, BlockKind.PopWall), Demo = () => Room("popwall").PopWall(0f, 0f, 4f, 0.5f, 2.4f, 0f) },
            new Gadget { Key = "mover", Name = "MOVING DESK", Text = "Slides back and forth. Watch it once, then go.",
                In = d => d.Blocks.Exists(b => b.Period > 0f && b.Spin == 0f), Demo = () => Room("mover").Wall(0f, 0f, 1.8f, 0.6f).Moving(1.2f, 0f, 2.6f) },
            new Gadget { Key = "spinner", Name = "TURNSTILE", Text = "Spins nonstop and swats you aside.",
                In = d => d.Blocks.Exists(b => b.Spin != 0f), Demo = () => Room("spinner").Wall(0f, 0f, 3.2f, 0.4f).Spinning(90f) },
            new Gadget { Key = "boost", Name = "SPEED STRIP", Text = "Shoots you forward, faster than you came in.",
                In = d => Zone(d, ZoneKind.Boost), Demo = () => Room("boost").Boost(0f, 0f, 1.4f, 3f, 0f) },
            new Gadget { Key = "mud", Name = "SHAG RUG", Text = "Thick and slow. You lose speed fast on it.",
                In = d => Zone(d, ZoneKind.Mud), Demo = () => Room("mud").Mud(0f, 0f, 3.4f, 2.4f) },
            new Gadget { Key = "ice", Name = "WET FLOOR", Text = "Barely any friction. You slide a long way.",
                In = d => Zone(d, ZoneKind.Ice), Demo = () => Room("ice").Ice(0f, 0f, 3.6f, 2.6f) },
            new Gadget { Key = "gap", Name = "HOLE IN THE FLOOR", Text = "Fall in and you're back where the shot began.",
                In = d => Zone(d, ZoneKind.Gap), Demo = () => Room("gap").Gap(0f, 0f, 3f, 1.6f) },
            new Gadget { Key = "conveyor", Name = "CONVEYOR BELT", Text = "Carries you along its arrows. Go with it, not against it.",
                In = d => Zone(d, ZoneKind.Conveyor), Demo = () => Room("conveyor").Conveyor(0f, 0f, 1.6f, 4f, -90f) },
            new Gadget { Key = "wind", Name = "AIR VENT", Text = "Blows you along its stream. Curve your shots with it.",
                In = d => Zone(d, ZoneKind.Wind), Demo = () => Room("wind").Wind(0f, 0f, 2f, 4f, 90f) },
            new Gadget { Key = "spin", Name = "SPIN PLATFORM", Text = "Bends your shot the way it spins, then flings you off the edge.",
                In = d => Zone(d, ZoneKind.Vortex), Demo = () => Room("spin").Vortex(0f, 0f, 1.6f) },  // was the whirlpool; a new key, so everyone meets it again (OUT-49)
            new Gadget { Key = "magnet", Name = "COFFEE MACHINE", Text = "Pulls you in from anywhere in its ring. Roll past close and it swings you round.",
                In = d => Zone(d, ZoneKind.Magnet), Demo = () => Room("magnet").Magnet(0f, 0f, 2.2f) },
            new Gadget { Key = "portal", Name = "PORTAL DOORS", Text = "In one door, out the other, at the same speed.",
                In = d => Zone(d, ZoneKind.Portal), Demo = () => Room("portal").Portal(-1.4f, -0.2f, 1.4f, 0.2f) },
            new Gadget { Key = "spikes", Name = "STAPLE STRIP", Text = "Staples pop up and down. Cross while they're down.",
                In = d => Zone(d, ZoneKind.Spikes), Demo = () => Room("spikes").Spikes(0f, 0f, 4.4f, 1.1f, 2.4f, 0f) },
            new Gadget { Key = "ramp", Name = "RAMP", Text = "Hops you up and over what's ahead.",
                In = d => Zone(d, ZoneKind.Ramp), Demo = () => Room("ramp").Ramp(0f, 0f, 1.6f, 1.8f, 0f) },
            new Gadget { Key = "jump", Name = "JUMP PAD", Text = "Throws you into the air. Land it on the other side.",
                In = d => Zone(d, ZoneKind.Jump), Demo = () => Room("jump").Jump(0f, 0f, 0.8f) },
            new Gadget { Key = "oneway", Name = "ONE-WAY DOOR", Text = "Lets you through one way only. The arrows say which.",
                In = d => Zone(d, ZoneKind.OneWay), Demo = () => Room("oneway").OneWay(0f, 0f, 3f) },
        };

        public static Gadget Find(string key) => Array.Find(All, g => g.Key == key);

        // The gadgets a room uses, in field-guide order.
        public static List<Gadget> In(LevelDef def)
        {
            var list = new List<Gadget>();
            if (def == null) return list;
            foreach (var g in All) if (g.In(def)) list.Add(g);
            return list;
        }
    }
}
