using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // The level editor (OUT-12): Out of Office > Level Editor. Build, play and validate a room without touching C#.
    //
    //   Left:   the rooms by chapter. Search, new (from a template), duplicate, mirror, delete, reorder, bulk tags,
    //           difficulty report.
    //   Middle: the board, to scale, on a snapping grid. Click to select, drag to move; the square handle resizes,
    //           the round one rotates. Right-click to add anything. Polygon floors and bent rails show their points.
    //           Live warnings sit on the board; Check draws the solver's solution over it.
    //   Right:  the room's settings, the selection's settings, problems, and the last LevelCheck result.
    //
    // Keys: Delete, Ctrl+D duplicate, Ctrl+C / Ctrl+V, Ctrl+Z / Ctrl+Y, Ctrl+S save, arrows nudge by the snap.
    // Mouse wheel zooms, middle or Alt drag pans. Everything is saved as the same level files the game loads.
    public class LevelEditorWindow : EditorWindow
    {
        [MenuItem("Out of Office/Level Editor %#l")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor").Show();

        // ---------- State ----------

        enum Pick { None, Block, Zone, Rail, Star, Hole, Start, RailPoint, ShapePoint }

        struct Sel
        {
            public Pick Kind;
            public int Index, Sub;
            public static readonly Sel None = new Sel { Kind = Pick.None, Index = -1, Sub = -1 };
            public bool Is(Pick k, int i) => Kind == k && Index == i;
        }

        LevelData.Manifest manifest;
        readonly Dictionary<string, LevelDef> rooms = new Dictionary<string, LevelDef>();
        readonly HashSet<string> dirty = new HashSet<string>();
        bool manifestDirty;
        HashSet<string> shipped = new HashSet<string>();

        LevelDef room;
        Sel sel = Sel.None;
        readonly List<string> undo = new List<string>(), redo = new List<string>();
        string committed;   // the room's JSON after the last change; what an undo goes back to

        List<LevelLint.Problem> problems = new List<LevelLint.Problem>();
        LevelCheck.Result check;
        string checkedJson;  // the room as it was when check ran: the result is stale once the room changes

        // View
        float snap = 0.1f, angleSnap = 5f, zoom = 1f;
        Vector2 pan;
        bool animate, showSolution = true, showGrid = true;
        string search = "", bulkTag = "";
        Vector2 listScroll, inspectorScroll;
        readonly HashSet<int> collapsed = new HashSet<int>();

        // Drag
        enum DragMode { None, Move, Resize, Rotate, Pan }
        DragMode drag;
        Vector2 dragStartMouse, dragStartValue, dragStartSize;
        float dragStartAngle;
        bool dragChanged;

        static readonly float[] Snaps = { 0f, 0.05f, 0.1f, 0.25f, 0.5f, 1f };
        static readonly string[] SnapNames = { "Off", "0.05", "0.1", "0.25", "0.5", "1" };

        const string Clip = "OUTOFOFFICE-LEVEL:";
        const float ListWidth = 230f, InspectorWidth = 320f;

        void OnEnable()
        {
            wantsMouseMove = true;
            Reload();
            EditorApplication.update += Tick;
        }

        void OnDisable() => EditorApplication.update -= Tick;

        void Tick()
        {
            if (animate) Repaint();
        }

        void Reload()
        {
            rooms.Clear();
            errorCache.Clear();
            dirty.Clear();
            manifestDirty = false;
            try { manifest = LevelFiles.ReadManifest(); }
            catch (LevelData.FormatException e)
            {
                Debug.LogError("Level Editor: " + e.Message);
                manifest = new LevelData.Manifest();
            }
            foreach (string id in LevelFiles.AllIds())
            {
                try { rooms[id] = LevelData.FromJson(LevelFiles.Read(LevelFiles.PathOf(id))); }
                catch (LevelData.FormatException e) { Debug.LogError($"Level Editor: {id}.json: {e.Message}"); }
            }
            shipped = new HashSet<string>(LevelFiles.ShippedIds());
            string keep = room?.Id;
            room = null;
            Select(keep != null && rooms.ContainsKey(keep) ? keep : FirstId());
        }

        string FirstId()
        {
            foreach (var c in manifest.Chapters) if (c.Levels.Count > 0) return c.Levels[0];
            foreach (var id in rooms.Keys) return id;
            return null;
        }

        void Select(string id)
        {
            room = id != null && rooms.TryGetValue(id, out var def) ? def : null;
            sel = Sel.None;
            undo.Clear();
            redo.Clear();
            committed = room == null ? null : LevelData.ToJson(room);
            check = null;
            problems = room == null ? new List<LevelLint.Problem>() : LevelLint.Check(room);
            pan = Vector2.zero;
            zoom = 1f;
            if (Event.current != null) GUI.FocusControl(null);  // not from the self-test, which has no GUI
            Repaint();
        }

        // Call after every change to the room: records the undo step, marks it unsaved, re-runs the layout rules.
        void Commit()
        {
            if (room == null) return;
            string now = LevelData.ToJson(room);
            if (now == committed) return;
            undo.Add(committed);
            if (undo.Count > 200) undo.RemoveAt(0);
            redo.Clear();
            committed = now;
            dirty.Add(room.Id);
            errorCache.Remove(room.Id);
            problems = LevelLint.Check(room);
            Repaint();
        }

        void Restore(string json)
        {
            room = LevelData.FromJson(json);
            rooms[room.Id] = room;
            committed = json;
            dirty.Add(room.Id);
            sel = Sel.None;
            problems = LevelLint.Check(room);
            Repaint();
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(committed);
            string back = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Restore(back);
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(committed);
            string fwd = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Restore(fwd);
        }

        bool SaveAll()
        {
            foreach (string id in dirty)
                if (rooms.TryGetValue(id, out var def)) LevelFiles.Save(def);
            if (manifestDirty) LevelFiles.SaveManifest(manifest);
            bool any = dirty.Count > 0 || manifestDirty;
            dirty.Clear();
            manifestDirty = false;
            if (any) LevelFiles.Refresh();
            return any;
        }

        // ---------- Layout ----------

        void OnGUI()
        {
            if (manifest == null) Reload();
            HandleShortcuts();
            Toolbar();
            float top = EditorStyles.toolbar.fixedHeight;
            var listRect = new Rect(0f, top, ListWidth, position.height - top);
            var inspRect = new Rect(position.width - InspectorWidth, top, InspectorWidth, position.height - top);
            var boardRect = new Rect(ListWidth, top, position.width - ListWidth - InspectorWidth, position.height - top);

            GUILayout.BeginArea(listRect, EditorStyles.helpBox);
            RoomList();
            GUILayout.EndArea();

            GUILayout.BeginArea(inspRect, EditorStyles.helpBox);
            Inspector();
            GUILayout.EndArea();

            Board(boardRect);
        }

        void Toolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            int unsaved = dirty.Count + (manifestDirty ? 1 : 0);
            if (GUILayout.Button(unsaved > 0 ? $"Save ({unsaved})" : "Saved", EditorStyles.toolbarButton, GUILayout.Width(70))) SaveAll();
            if (GUILayout.Button("Revert", EditorStyles.toolbarButton) && (unsaved == 0 || EditorUtility.DisplayDialog("Revert", "Throw away unsaved changes?", "Revert", "Keep"))) Reload();
            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(undo.Count == 0)) if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) Undo();
            using (new EditorGUI.DisabledScope(redo.Count == 0)) if (GUILayout.Button("Redo", EditorStyles.toolbarButton)) Redo();
            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(room == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("▶ Play this room", EditorStyles.toolbarButton)) PlayRoom();
                if (GUILayout.Button("Check this room", EditorStyles.toolbarButton)) CheckRoom();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Snap", EditorStyles.miniLabel);
            int si = Mathf.Max(0, System.Array.IndexOf(Snaps, snap));
            snap = Snaps[EditorGUILayout.Popup(si, SnapNames, EditorStyles.toolbarPopup, GUILayout.Width(50))];
            animate = GUILayout.Toggle(animate, "Animate", EditorStyles.toolbarButton);
            showSolution = GUILayout.Toggle(showSolution, "Solution", EditorStyles.toolbarButton);
            showGrid = GUILayout.Toggle(showGrid, "Grid", EditorStyles.toolbarButton);
            if (GUILayout.Button("Frame", EditorStyles.toolbarButton)) { zoom = 1f; pan = Vector2.zero; }
            GUILayout.EndHorizontal();
        }

        // ---------- Room list ----------

        void RoomList()
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            listScroll = GUILayout.BeginScrollView(listScroll);
            int number = 0;
            for (int c = 0; c < manifest.Chapters.Count; c++)
            {
                var chapter = manifest.Chapters[c];
                bool open = !collapsed.Contains(c);
                bool now = EditorGUILayout.Foldout(open, $"{c + 1}. {chapter.Theme}  ({chapter.Levels.Count})", true, EditorStyles.foldoutHeader);
                if (now != open) { if (now) collapsed.Remove(c); else collapsed.Add(c); }
                for (int i = 0; i < chapter.Levels.Count; i++)
                {
                    number++;
                    string id = chapter.Levels[i];
                    if (!now || !Matches(id)) continue;
                    RoomRow(id, number.ToString());
                }
            }
            if (!string.IsNullOrEmpty(manifest.Tutorial) && Matches(manifest.Tutorial))
            {
                EditorGUILayout.LabelField("Tutorial", EditorStyles.boldLabel);
                RoomRow(manifest.Tutorial, "T");
            }
            var loose = new List<string>();
            foreach (var id in rooms.Keys) if (ChapterOf(id) < 0 && id != manifest.Tutorial && Matches(id)) loose.Add(id);
            if (loose.Count > 0)
            {
                EditorGUILayout.LabelField("Not in a chapter", EditorStyles.boldLabel);
                foreach (var id in loose) RoomRow(id, "-");
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New…")) NewRoomMenu();
            using (new EditorGUI.DisabledScope(room == null))
            {
                if (GUILayout.Button("Duplicate")) Duplicate(false);
                if (GUILayout.Button("Mirror")) Duplicate(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(room == null))
            {
                if (GUILayout.Button("▲")) MoveRoom(-1);
                if (GUILayout.Button("▼")) MoveRoom(1);
                using (new EditorGUI.DisabledScope(room == null || shipped.Contains(room.Id)))
                    if (GUILayout.Button(new GUIContent("Delete", "Shipped rooms can't be deleted: saves key on their ids"))) DeleteRoom();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            bulkTag = EditorGUILayout.TextField(bulkTag);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(bulkTag)))
            {
                if (GUILayout.Button(new GUIContent("+Tag", "Add this tag to every room the search shows"), GUILayout.Width(44))) BulkTag(true);
                if (GUILayout.Button(new GUIContent("-Tag", "Remove this tag from every room the search shows"), GUILayout.Width(44))) BulkTag(false);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Difficulty report (CSV)…")) { SaveAll(); LevelReport.WriteWithDialog(); }
        }

        void RoomRow(string id, string number)
        {
            rooms.TryGetValue(id, out var def);
            int errors = def == null ? 1 : def == room ? LevelLint.Errors(problems) : ErrorsOf(def);
            string label = $"{number,3}  {(def == null ? id + " (missing)" : def.Name)}{(dirty.Contains(id) ? " *" : "")}";
            var style = new GUIStyle(EditorStyles.label) { richText = true };
            if (errors > 0) label = "<color=#ff6b5e>" + label + "</color>";
            bool selected = room != null && room.Id == id;
            var r = GUILayoutUtility.GetRect(new GUIContent(label), style);
            if (selected) EditorGUI.DrawRect(r, new Color(0.24f, 0.48f, 0.9f, 0.45f));
            GUI.Label(r, new GUIContent(label, id), style);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Select(id);
                Event.current.Use();
            }
        }

        // Layout errors per room for the list, worked out once (rooms other than the open one only change on a reload).
        readonly Dictionary<string, int> errorCache = new Dictionary<string, int>();

        int ErrorsOf(LevelDef def)
        {
            if (!errorCache.TryGetValue(def.Id, out int n)) errorCache[def.Id] = n = LevelLint.Errors(LevelLint.Check(def));
            return n;
        }

        bool Matches(string id)
        {
            if (string.IsNullOrEmpty(search)) return true;
            string s = search.ToLowerInvariant();
            if (id.Contains(s)) return true;
            if (!rooms.TryGetValue(id, out var def)) return false;
            if (def.Name.ToLowerInvariant().Contains(s) || (def.Teaches ?? "").Contains(s)) return true;
            foreach (var t in def.Tags) if (t.ToLowerInvariant().Contains(s)) return true;
            return false;
        }

        int ChapterOf(string id)
        {
            for (int c = 0; c < manifest.Chapters.Count; c++) if (manifest.Chapters[c].Levels.Contains(id)) return c;
            return -1;
        }

        void NewRoomMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Standard 9 x 16"), false, () => NewRoom(ShapeDef.Rect(9f, 16f)));
            menu.AddItem(new GUIContent("Tall 9 x 22 (scrolls)"), false, () => NewRoom(ShapeDef.Rect(9f, 22f).Shift(0f, -3f)));
            menu.AddItem(new GUIContent("Round"), false, () => NewRoom(ShapeDef.Circle(7.2f)));
            menu.AddItem(new GUIContent("Octagon"), false, () => NewRoom(ShapeDef.Chamfered(9f, 16f, 1.6f)));
            menu.AddItem(new GUIContent("Stadium"), false, () => NewRoom(ShapeDef.Stadium(9f, 16f)));
            menu.AddItem(new GUIContent("Figure eight"), false, () => NewRoom(ShapeDef.FigureEight(4.4f, 3.6f, 3f)));
            menu.ShowAsContext();
        }

        string FreeId(string stem)
        {
            stem = stem.Length > 20 ? stem.Substring(0, 20) : stem;
            if (!rooms.ContainsKey(stem) && LevelFiles.ValidId(stem)) return stem;
            for (int n = 2; ; n++) if (!rooms.ContainsKey(stem + n)) return stem + n;
        }

        void AddToChapter(LevelDef def, int afterChapter, string afterId)
        {
            rooms[def.Id] = def;
            dirty.Add(def.Id);
            int c = afterChapter >= 0 ? afterChapter : Mathf.Max(0, manifest.Chapters.Count - 1);
            if (manifest.Chapters.Count == 0) manifest.Chapters.Add(new LevelData.ChapterEntry { Theme = Themes.All[0].Name });
            var list = manifest.Chapters[c].Levels;
            int at = afterId != null ? list.IndexOf(afterId) + 1 : list.Count;
            list.Insert(at <= 0 ? list.Count : at, def.Id);
            manifestDirty = true;
            Select(def.Id);
            dirty.Add(def.Id);
        }

        void NewRoom(ShapeDef shape)
        {
            Rect b = Geometry.Bounds(shape.Outline);
            var def = new LevelDef(FreeId("room"), "Untitled Room", 0f, b.yMax - 1.8f) { Shape = shape, Start = new Vector2(0f, b.yMin + 2f), Par = 2 };
            int c = room != null ? Mathf.Max(0, ChapterOf(room.Id)) : -1;
            AddToChapter(def, c, room?.Id);
        }

        void Duplicate(bool mirror)
        {
            var copy = LevelData.FromFields((Dictionary<string, object>)JsonLite.Parse(committed));
            string id = FreeId(room.Id + (mirror ? "-m" : "-copy"));
            LevelDef def;
            if (mirror)
            {
                def = LevelDef.Mirror(copy, id, copy.Name + " (Mirrored)", copy.Hint);
                def.Tags.Add("remix:" + room.Id);
            }
            else
            {
                var text = LevelData.ToJson(copy).Replace($"\"id\": \"{room.Id}\"", $"\"id\": \"{id}\"");
                def = LevelData.FromJson(text);
                def.Name = copy.Name + " (Copy)";
            }
            AddToChapter(def, ChapterOf(room.Id), room.Id);
        }

        void MoveRoom(int dir)
        {
            int c = ChapterOf(room.Id);
            if (c < 0) return;
            var list = manifest.Chapters[c].Levels;
            int i = list.IndexOf(room.Id), j = i + dir;
            if (j >= 0 && j < list.Count)
            {
                list[i] = list[j];
                list[j] = room.Id;
            }
            else if (c + dir >= 0 && c + dir < manifest.Chapters.Count)
            {
                // Past the end of a chapter: into the next (or previous) one.
                list.RemoveAt(i);
                var other = manifest.Chapters[c + dir].Levels;
                if (dir > 0) other.Insert(0, room.Id); else other.Add(room.Id);
            }
            manifestDirty = true;
            Repaint();
        }

        void DeleteRoom()
        {
            if (!EditorUtility.DisplayDialog("Delete room", $"Delete {room.Name} ({room.Id}.json)? This can't be undone here (use git).", "Delete", "Cancel")) return;
            string id = room.Id;
            foreach (var c in manifest.Chapters) c.Levels.Remove(id);
            manifestDirty = true;
            rooms.Remove(id);
            dirty.Remove(id);
            LevelFiles.Delete(id);
            SaveAll();
            Select(FirstId());
        }

        void BulkTag(bool add)
        {
            string tag = bulkTag.Trim();
            int n = 0;
            foreach (var kv in rooms)
            {
                if (!Matches(kv.Key)) continue;
                bool has = kv.Value.Tags.Contains(tag);
                if (add && !has) { kv.Value.Tags.Add(tag); dirty.Add(kv.Key); n++; }
                if (!add && has) { kv.Value.Tags.Remove(tag); dirty.Add(kv.Key); n++; }
            }
            if (room != null) committed = LevelData.ToJson(room);
            ShowNotification(new GUIContent($"{(add ? "Tagged" : "Untagged")} {n} rooms"));
        }

        // ---------- Play and check ----------

        void PlayRoom()
        {
            SaveAll();
            Levels.Reload();
            if (Levels.IndexOf(room.Id) < 0 && room.Id != Levels.Tutorial.Id)
            {
                EditorUtility.DisplayDialog("Play this room", "Put the room in a chapter (and fix any load errors) to play it.", "OK");
                return;
            }
            SessionState.SetString(OutOfOfficeGame.PlaytestKey, room.Id);
            EditorApplication.EnterPlaymode();
        }

        void CheckRoom()
        {
            try
            {
                EditorUtility.DisplayProgressBar("LevelCheck", $"Sweeping {room.Name}…", 0.5f);
                check = LevelCheck.CheckOne(room);
                checkedJson = committed;
            }
            finally { EditorUtility.ClearProgressBar(); }
            Repaint();
        }

        // ---------- Inspector ----------

        void Inspector()
        {
            if (room == null) { GUILayout.Label("No room selected."); return; }
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            EditorGUI.BeginChangeCheck();

            Header("Room");
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField(new GUIContent("Id", "The save key. Fixed once a room has shipped."), room.Id);
            if (!shipped.Contains(room.Id) && GUILayout.Button("Rename id…", EditorStyles.miniButton)) RenameIdDialog();
            room.Name = EditorGUILayout.TextField("Name", room.Name);
            string hint = EditorGUILayout.TextField(new GUIContent("Hint", "Shown in capitals"), room.Hint);
            if (hint != room.Hint) room.Hint = hint.ToUpperInvariant();
            room.Par = Mathf.Max(1, EditorGUILayout.IntField("Par", room.Par));
            int over = EditorGUILayout.IntField(new GUIContent("Shot limit", $"0 = par + {LevelDef.ShotSlack} (now {room.MaxShots})"), room.MaxShotsOverride);
            if (over != room.MaxShotsOverride) room.WithMaxShots(Mathf.Max(0, over));
            room.Difficulty = EditorGUILayout.IntSlider(new GUIContent("Difficulty", "0 = not rated, 1 gentle to 5 brutal"), room.Difficulty, 0, 5);
            room.IsBoss = EditorGUILayout.Toggle("Boss room", room.IsBoss);
            var keys = new List<string> { "(none)" };
            foreach (var g in Gadgets.All) keys.Add(g.Key);
            int ti = Mathf.Max(0, keys.IndexOf(room.Teaches));
            ti = EditorGUILayout.Popup(new GUIContent("Teaches", "The gadget this room introduces"), ti, keys.ToArray());
            room.Teaches = ti == 0 ? "" : keys[ti];
            string tags = EditorGUILayout.TextField(new GUIContent("Tags", "Comma separated"), string.Join(", ", room.Tags));
            if (tags != string.Join(", ", room.Tags))
            {
                room.Tags.Clear();
                foreach (var t in tags.Split(',')) if (t.Trim().Length > 0) room.Tags.Add(t.Trim());
            }
            int chapter = ChapterOf(room.Id);
            var chapterNames = new List<string>();
            for (int c = 0; c < manifest.Chapters.Count; c++) chapterNames.Add($"{c + 1}. {manifest.Chapters[c].Theme}");
            if (chapter >= 0)
            {
                int moved = EditorGUILayout.Popup("Chapter", chapter, chapterNames.ToArray());
                if (moved != chapter)
                {
                    manifest.Chapters[chapter].Levels.Remove(room.Id);
                    manifest.Chapters[moved].Levels.Add(room.Id);
                    manifestDirty = true;
                }
            }
            else if (room.Id != manifest.Tutorial)
            {
                int put = EditorGUILayout.Popup("Chapter", -1, chapterNames.ToArray());
                if (put >= 0) { manifest.Chapters[put].Levels.Add(room.Id); manifestDirty = true; }
            }

            Header("Board");
            room.Hole = EditorGUILayout.Vector2Field("Bed", room.Hole);
            room.Start = EditorGUILayout.Vector2Field("Start", room.Start);
            ShapeFields();
            if (EditorGUI.EndChangeCheck()) Commit();

            EditorGUI.BeginChangeCheck();
            SelectionFields();
            if (EditorGUI.EndChangeCheck()) Commit();

            Header($"Problems ({LevelLint.Errors(problems)} errors)");
            if (problems.Count == 0) GUILayout.Label("None.", EditorStyles.miniLabel);
            foreach (var p in problems)
                EditorGUILayout.HelpBox(p.Text, p.Error ? MessageType.Error : MessageType.Warning);

            Header("LevelCheck");
            if (check == null) GUILayout.Label("Press Check this room to sweep it with the real physics.", EditorStyles.wordWrappedMiniLabel);
            else
            {
                if (checkedJson != committed) EditorGUILayout.HelpBox("The room changed since this check.", MessageType.Info);
                EditorGUILayout.LabelField("Verdict", check.Ok ? "OK" : check.Solved ? "BAD (see the report line)" : "UNSOLVED");
                EditorGUILayout.LabelField("Fewest shots", check.Solved ? check.MinShots.ToString() : "-");
                EditorGUILayout.LabelField("One-shot win", $"{check.OneShotWin:0.0}%");
                EditorGUILayout.LabelField("Hazard rate", $"{check.HazardRate:0.0}%");
                EditorGUILayout.LabelField("Coffees reached", $"{check.StarsReached}/{check.StarsTotal}");
                if (check.Solved)
                {
                    EditorGUILayout.LabelField("Suggested par", $"{check.MinShots} to {check.MinShots + 1}");
                    bool parOk = room.Par >= check.MinShots && room.Par <= check.MinShots + 1;
                    using (new EditorGUI.DisabledScope(parOk))
                        if (GUILayout.Button(parOk ? "Par is in range" : $"Set par to {Mathf.Clamp(room.Par, check.MinShots, check.MinShots + 1)}"))
                        {
                            room.Par = Mathf.Clamp(room.Par, check.MinShots, check.MinShots + 1);
                            Commit();
                        }
                }
                EditorGUILayout.SelectableLabel(check.Line, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(64));
            }
            GUILayout.EndScrollView();
        }

        static void Header(string text)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        void RenameIdDialog()
        {
            var popup = CreateInstance<RenamePopup>();
            popup.Value = room.Id;
            popup.Done = newId =>
            {
                if (newId == room.Id) return;
                if (!LevelFiles.ValidId(newId) || rooms.ContainsKey(newId))
                {
                    EditorUtility.DisplayDialog("Rename id", $"\"{newId}\" is taken or not a valid id (lower case letters, digits, dashes).", "OK");
                    return;
                }
                string old = room.Id;
                var def = LevelData.FromJson(committed.Replace($"\"id\": \"{old}\"", $"\"id\": \"{newId}\""));
                foreach (var c in manifest.Chapters) { int i = c.Levels.IndexOf(old); if (i >= 0) c.Levels[i] = newId; }
                if (manifest.Tutorial == old) manifest.Tutorial = newId;
                manifestDirty = true;
                rooms.Remove(old);
                dirty.Remove(old);
                LevelFiles.Delete(old);
                rooms[newId] = def;
                dirty.Add(newId);
                SaveAll();
                Select(newId);
            };
            popup.ShowUtility();
        }

        void ShapeFields()
        {
            var s = room.Shape;
            var kind = (ShapeKind)EditorGUILayout.EnumPopup("Floor", s.Kind);
            float[] args = (float[])s.Args.Clone();
            if (kind != s.Kind)
            {
                Rect b = Geometry.Bounds(s.Outline);
                args = kind == ShapeKind.Rect ? new[] { b.width, b.height, 0.7f }
                    : kind == ShapeKind.Chamfered ? new[] { b.width, b.height, 1.6f, 0.5f }
                    : kind == ShapeKind.Circle ? new[] { Mathf.Min(b.width, b.height) * 0.5f }
                    : kind == ShapeKind.FigureEight ? new[] { b.width * 0.5f, b.height * 0.25f, b.width * 0.35f }
                    : new[] { 0.7f, b.xMin, b.yMin, b.xMax, b.yMin, b.xMax, b.yMax, b.xMin, b.yMax };
                room.Shape = ShapeDef.Make(kind, args, false, s.Offset);
                return;
            }
            string[] names = kind == ShapeKind.Rect ? new[] { "Width", "Height", "Corner" }
                : kind == ShapeKind.Chamfered ? new[] { "Width", "Height", "Cut", "Corner" }
                : kind == ShapeKind.Circle ? new[] { "Radius" }
                : kind == ShapeKind.FigureEight ? new[] { "Radius", "Half spacing", "Waist" }
                : new[] { "Corner" };
            for (int i = 0; i < names.Length; i++) args[i] = Mathf.Max(0.05f, EditorGUILayout.FloatField(names[i], args[i]));
            if (kind == ShapeKind.Polygon) EditorGUILayout.LabelField("Corners", $"{(args.Length - 1) / 2}  (drag on the board; right-click a corner to add or remove)", EditorStyles.wordWrappedMiniLabel);
            bool flipped = EditorGUILayout.Toggle("Flipped", s.Flipped);
            Vector2 offset = EditorGUILayout.Vector2Field("Offset", s.Offset);
            bool same = flipped == s.Flipped && offset.x == s.Offset.x && offset.y == s.Offset.y;
            for (int i = 0; i < args.Length && same; i++) same = args[i] == s.Args[i];
            if (!same) room.Shape = ShapeDef.Make(kind, args, flipped, offset);
        }

        void SelectionFields()
        {
            switch (sel.Kind)
            {
                case Pick.Block when sel.Index < room.Blocks.Count:
                {
                    var b = room.Blocks[sel.Index];
                    Header($"{b.Kind} (block {sel.Index + 1})");
                    b.Kind = (BlockKind)EditorGUILayout.EnumPopup("Kind", b.Kind);
                    b.Shape = (BlockShape)EditorGUILayout.EnumPopup("Shape", b.Shape);
                    b.Center = EditorGUILayout.Vector2Field("At", b.Center);
                    if (b.Shape == BlockShape.Post)
                    {
                        float r = Mathf.Max(0.05f, EditorGUILayout.FloatField("Radius", b.Size.x * 0.5f));
                        b.Size = new Vector2(r * 2f, r * 2f);
                    }
                    else
                    {
                        b.Size = Vector2.Max(new Vector2(0.05f, 0.05f), EditorGUILayout.Vector2Field("Size", b.Size));
                        b.Corner = EditorGUILayout.FloatField(new GUIContent("Corner", "-1 = automatic"), b.Corner);
                        b.Angle = EditorGUILayout.FloatField("Angle", b.Angle);
                    }
                    b.MoveOffset = EditorGUILayout.Vector2Field(new GUIContent("Swing", "Moves between at - swing and at + swing"), b.MoveOffset);
                    b.Period = Mathf.Max(0f, EditorGUILayout.FloatField("Swing period (s)", b.Period));
                    b.Phase = EditorGUILayout.FloatField(new GUIContent("Swing phase", "Radians"), b.Phase);
                    b.Spin = EditorGUILayout.FloatField(new GUIContent("Spin (deg/s)", "0 = still"), b.Spin);
                    if (b.Kind == BlockKind.Gate) b.Group = EditorGUILayout.IntField("Switch group", b.Group);
                    if (b.Kind == BlockKind.PopWall)
                    {
                        b.BlinkPeriod = Mathf.Max(0f, EditorGUILayout.FloatField("Pop period (s)", b.BlinkPeriod));
                        b.BlinkPhase = EditorGUILayout.Slider("Pop phase", b.BlinkPhase, 0f, 1f);
                    }
                    if (b.Reception != 0) EditorGUILayout.HelpBox("The receptionist's desk: keep it clear of every route (LevelCheck measures it).", MessageType.None);
                    break;
                }
                case Pick.Zone when sel.Index < room.Zones.Count:
                {
                    var z = room.Zones[sel.Index];
                    Header($"{z.Kind} (zone {sel.Index + 1})");
                    z.Kind = (ZoneKind)EditorGUILayout.EnumPopup("Kind", z.Kind);
                    z.Center = EditorGUILayout.Vector2Field("At", z.Center);
                    if (RoundZone(z.Kind))
                    {
                        float r = Mathf.Max(0.05f, EditorGUILayout.FloatField(z.Kind == ZoneKind.Vortex || z.Kind == ZoneKind.Magnet ? "Reach" : "Radius", z.Size.x * 0.5f));
                        z.Size = new Vector2(r * 2f, r * 2f);
                    }
                    else z.Size = Vector2.Max(new Vector2(0.05f, 0.05f), EditorGUILayout.Vector2Field(new GUIContent("Size", "Across, along the direction"), z.Size));
                    if (Directional(z.Kind) || z.Kind == ZoneKind.Portal) z.Angle = EditorGUILayout.FloatField(new GUIContent("Angle", "0 = up, 90 = left"), z.Angle);
                    if (z.Kind == ZoneKind.Portal) z.Link = EditorGUILayout.IntField(new GUIContent("Linked zone", "Index of the partner portal, from 1"), z.Link + 1) - 1;
                    if (z.Kind == ZoneKind.Switch) z.Group = EditorGUILayout.IntField("Opens gate group", z.Group);
                    if (z.Kind == ZoneKind.Vortex) z.Turn = EditorGUILayout.Toggle("Clockwise", z.Turn < 0f) ? -1f : 1f;
                    if (z.Kind == ZoneKind.Spikes)
                    {
                        z.Period = Mathf.Max(0f, EditorGUILayout.FloatField("Period (s)", z.Period));
                        z.Phase = EditorGUILayout.Slider("Phase", z.Phase, 0f, 1f);
                    }
                    break;
                }
                case Pick.Rail when sel.Index < room.Rails.Count:
                case Pick.RailPoint when sel.Index < room.Rails.Count:
                {
                    var r = room.Rails[sel.Index];
                    Header($"Rail {sel.Index + 1}");
                    r.Kind = (BlockKind)EditorGUILayout.EnumPopup("Kind", r.Kind);
                    r.Width = Mathf.Max(0.1f, EditorGUILayout.FloatField("Width", r.Width));
                    if (r.IsArc)
                    {
                        var a = r.ArcArgs;
                        var c = EditorGUILayout.Vector2Field("Center", new Vector2(a[0], a[1]));
                        float rad = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius", a[2]));
                        float from = EditorGUILayout.FloatField("From (deg)", a[3]), to = EditorGUILayout.FloatField("To (deg)", a[4]);
                        r.ArcArgs = new[] { c.x, c.y, rad, from, to };
                    }
                    else
                    {
                        r.Bend = Mathf.Max(0f, EditorGUILayout.FloatField("Bend", r.Bend));
                        EditorGUILayout.LabelField("Points", $"{r.Points.Count}  (drag on the board; right-click a point to add or remove)", EditorStyles.wordWrappedMiniLabel);
                    }
                    r.Flipped = EditorGUILayout.Toggle("Flipped", r.Flipped);
                    r.Rebuild();
                    break;
                }
                case Pick.Star when sel.Index < room.Stars.Count:
                    Header($"Coffee {sel.Index + 1}");
                    room.Stars[sel.Index] = EditorGUILayout.Vector2Field("At", room.Stars[sel.Index]);
                    break;
            }
        }

        // ---------- Board ----------

        Rect board;
        Vector2 viewCenter;
        float scale;

        Vector2 ToScreen(Vector2 w) => board.center + new Vector2(w.x - viewCenter.x, -(w.y - viewCenter.y)) * scale;
        Vector2 ToWorld(Vector2 s) => viewCenter + new Vector2(s.x - board.center.x, -(s.y - board.center.y)) / scale;

        void Board(Rect rect)
        {
            board = rect;
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.14f, 0.16f));
            if (room == null) return;

            Rect bounds = Geometry.Bounds(room.Shape.Outline);
            float fit = Mathf.Min((rect.width - 40f) / bounds.width, (rect.height - 40f) / bounds.height);
            scale = Mathf.Max(4f, fit * zoom);
            viewCenter = bounds.center + pan;

            GUI.BeginClip(rect);
            board = new Rect(0f, 0f, rect.width, rect.height);
            var e = Event.current;
            BoardInput(e);
            if (e.type == EventType.Repaint) DrawBoard();
            GUI.EndClip();
            board = rect;
        }

        void DrawBoard()
        {
            var outline = room.Shape.Outline;
            Handles.color = new Color(0.86f, 0.84f, 0.8f);
            var tris = new List<int>();
            var verts = Geometry.Triangulate(outline, null, tris);
            for (int i = 0; i + 2 < tris.Count; i += 3)
                Handles.DrawAAConvexPolygon(ToScreen(verts[tris[i]]), ToScreen(verts[tris[i + 1]]), ToScreen(verts[tris[i + 2]]));

            if (showGrid) DrawGrid();

            Handles.color = new Color(0.25f, 0.27f, 0.32f);
            DrawLoop(outline, 3f);

            foreach (var br in room.Bridges)
                FillPoly(Box(br.center, br.size, 0f), new Color(0.62f, 0.45f, 0.28f, 0.6f));

            float t = (float)EditorApplication.timeSinceStartup;
            for (int i = 0; i < room.Zones.Count; i++) DrawZone(room.Zones[i], i, sel.Is(Pick.Zone, i), t);
            for (int i = 0; i < room.Rails.Count; i++) DrawRail(room.Rails[i], i, sel.Kind is Pick.Rail or Pick.RailPoint && sel.Index == i);
            for (int i = 0; i < room.Blocks.Count; i++) DrawBlock(room.Blocks[i], i, sel.Is(Pick.Block, i), t);

            // Bed, start, coffees
            Disc(room.Hole, Rules.HoleRadius, new Color(0.35f, 0.42f, 0.75f));
            Label(room.Hole, "BED", Color.white);
            if (sel.Kind == Pick.Hole) Ring(room.Hole, Rules.HoleRadius + 0.12f, Color.cyan);
            Disc(room.Start, Rules.PuckRadius, new Color(1f, 0.62f, 0.25f));
            if (sel.Kind == Pick.Start) Ring(room.Start, Rules.PuckRadius + 0.12f, Color.cyan);
            for (int i = 0; i < room.Stars.Count; i++)
            {
                Disc(room.Stars[i], 0.26f, new Color(0.55f, 0.33f, 0.18f));
                Label(room.Stars[i], (i + 1).ToString(), Color.white);
                if (sel.Is(Pick.Star, i)) Ring(room.Stars[i], 0.38f, Color.cyan);
            }

            if (room.Shape.Kind == ShapeKind.Polygon) DrawShapePoints();
            if (showSolution && check != null && checkedJson == committed) DrawSolution();

            foreach (var p in problems)
            {
                var s = ToScreen(p.At);
                Handles.color = p.Error ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.8f, 0.2f);
                Handles.DrawAAPolyLine(3f, s + new Vector2(-6, -6), s + new Vector2(6, 6));
                Handles.DrawAAPolyLine(3f, s + new Vector2(-6, 6), s + new Vector2(6, -6));
            }

            var mouse = ToWorld(Event.current.mousePosition);
            GUI.Label(new Rect(8, board.height - 22, 400, 20), $"{room.Name}   x {mouse.x:0.00}  y {mouse.y:0.00}   zoom {zoom:0.0}x", EditorStyles.whiteMiniLabel);
        }

        void DrawGrid()
        {
            Rect b = Geometry.Bounds(room.Shape.Outline);
            for (float x = Mathf.Floor(b.xMin); x <= b.xMax; x += 0.5f)
            {
                Handles.color = Mathf.Approximately(x % 1f, 0f) ? new Color(0f, 0f, 0f, 0.12f) : new Color(0f, 0f, 0f, 0.05f);
                if (Mathf.Approximately(x, 0f)) Handles.color = new Color(0.2f, 0.4f, 0.9f, 0.25f);
                Handles.DrawLine(ToScreen(new Vector2(x, b.yMin)), ToScreen(new Vector2(x, b.yMax)));
            }
            for (float y = Mathf.Floor(b.yMin); y <= b.yMax; y += 0.5f)
            {
                Handles.color = Mathf.Approximately(y % 1f, 0f) ? new Color(0f, 0f, 0f, 0.12f) : new Color(0f, 0f, 0f, 0.05f);
                if (Mathf.Approximately(y, 0f)) Handles.color = new Color(0.2f, 0.4f, 0.9f, 0.25f);
                Handles.DrawLine(ToScreen(new Vector2(b.xMin, y)), ToScreen(new Vector2(b.xMax, y)));
            }
        }

        static Color BlockColor(BlockKind k)
        {
            switch (k)
            {
                case BlockKind.Hazard: return new Color(0.36f, 0.7f, 0.32f);
                case BlockKind.Bouncer: return new Color(0.93f, 0.36f, 0.42f);
                case BlockKind.Sticky: return new Color(0.75f, 0.45f, 0.85f);
                case BlockKind.Gate: return new Color(0.95f, 0.75f, 0.2f);
                case BlockKind.Crate: return new Color(0.72f, 0.52f, 0.3f);
                case BlockKind.Breakable: return new Color(0.6f, 0.85f, 0.95f);
                case BlockKind.Kicker: return new Color(0.98f, 0.55f, 0.1f);
                case BlockKind.PopWall: return new Color(0.5f, 0.55f, 0.75f);
                default: return new Color(0.42f, 0.55f, 0.72f);
            }
        }

        static Color ZoneColor(ZoneKind k)
        {
            switch (k)
            {
                case ZoneKind.Boost: return new Color(1f, 0.85f, 0.2f, 0.45f);
                case ZoneKind.Mud: return new Color(0.45f, 0.32f, 0.2f, 0.5f);
                case ZoneKind.Ramp: return new Color(0.8f, 0.8f, 0.85f, 0.5f);
                case ZoneKind.Portal: return new Color(0.6f, 0.3f, 0.95f, 0.55f);
                case ZoneKind.Ice: return new Color(0.7f, 0.9f, 1f, 0.55f);
                case ZoneKind.Magnet: return new Color(0.9f, 0.3f, 0.3f, 0.25f);
                case ZoneKind.Gap: return new Color(0.05f, 0.05f, 0.08f, 0.8f);
                case ZoneKind.Switch: return new Color(0.95f, 0.75f, 0.2f, 0.7f);
                case ZoneKind.Conveyor: return new Color(0.35f, 0.35f, 0.4f, 0.55f);
                case ZoneKind.Wind: return new Color(0.75f, 0.95f, 0.95f, 0.4f);
                case ZoneKind.Vortex: return new Color(0.25f, 0.6f, 0.9f, 0.35f);
                case ZoneKind.Spikes: return new Color(0.6f, 0.6f, 0.65f, 0.7f);
                case ZoneKind.Jump: return new Color(0.3f, 0.85f, 0.5f, 0.55f);
                default: return new Color(0.9f, 0.9f, 0.9f, 0.6f);
            }
        }

        static bool RoundZone(ZoneKind k) => k == ZoneKind.Portal || k == ZoneKind.Magnet || k == ZoneKind.Switch || k == ZoneKind.Vortex || k == ZoneKind.Jump;
        static bool Directional(ZoneKind k) => k == ZoneKind.Boost || k == ZoneKind.Conveyor || k == ZoneKind.Wind || k == ZoneKind.Ramp || k == ZoneKind.OneWay;

        // Where a block is at time t, by the same formula Mover uses.
        static void Pose(BlockDef b, float t, bool live, out Vector2 at, out float angle)
        {
            at = b.Center;
            angle = b.Angle;
            if (!live) return;
            if (b.Period > 0f) at += b.MoveOffset * Mathf.Sin(t * Mathf.PI * 2f / b.Period + b.Phase);
            angle += b.Spin * t;
        }

        void DrawBlock(BlockDef b, int i, bool selected, float t)
        {
            var color = BlockColor(b.Kind);
            if (b.Period > 0f)
            {
                // The swing: both ends, faint, and the line between.
                var ghost = new Color(color.r, color.g, color.b, 0.25f);
                Shape(b, b.Center - b.MoveOffset, b.Angle, ghost);
                Shape(b, b.Center + b.MoveOffset, b.Angle, ghost);
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.DrawDottedLine(ToScreen(b.Center - b.MoveOffset), ToScreen(b.Center + b.MoveOffset), 3f);
            }
            if (b.Kind == BlockKind.PopWall && animate && b.BlinkPeriod > 0f && Mathf.Repeat(t / b.BlinkPeriod + b.BlinkPhase, 1f) >= 0.5f)
                color.a = 0.3f;
            Pose(b, t, animate, out var at, out float angle);
            Shape(b, at, angle, color);
            if (b.Spin != 0f) Ring(b.Center, b.Size.magnitude * 0.5f, new Color(1f, 1f, 1f, 0.25f));
            if (b.Kind == BlockKind.Gate) Label(at, "G" + b.Group, Color.black);
            if (b.Reception != 0) Label(at, "RECEPTION", Color.white);
            if (selected)
            {
                Handles.color = Color.cyan;
                if (b.Shape == BlockShape.Post) Ring(b.Center, b.Size.x * 0.5f + 0.06f, Color.cyan);
                else DrawLoop(Box(b.Center, b.Size + new Vector2(0.12f, 0.12f), b.Angle), 2f);
                Handle(ResizeHandle(b), false);
                if (b.Shape != BlockShape.Post) Handle(RotateHandle(b.Center, b.Size, b.Angle), true);
            }
        }

        void Shape(BlockDef b, Vector2 at, float angle, Color color)
        {
            if (b.Shape == BlockShape.Post) Disc(at, b.Size.x * 0.5f, color);
            else FillPoly(RoundedBox(at, b.Size, angle, b.CornerRadius), color);
        }

        void DrawZone(ZoneDef z, int i, bool selected, float t)
        {
            var color = ZoneColor(z.Kind);
            if (z.Kind == ZoneKind.Spikes && animate && z.Period > 0f && Mathf.Repeat(t / z.Period + z.Phase, 1f) >= 0.5f) color.a *= 0.35f;
            if (RoundZone(z.Kind))
            {
                Disc(z.Center, z.Size.x * 0.5f, color);
                if (z.Kind == ZoneKind.Vortex)
                {
                    float spin = animate ? t * 90f * z.Turn : 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        float a = (spin + k * 120f) * Mathf.Deg2Rad;
                        Handles.color = new Color(1f, 1f, 1f, 0.6f);
                        Handles.DrawAAPolyLine(2f, ToScreen(z.Center), ToScreen(z.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * z.Size.x * 0.45f));
                    }
                }
                if (z.Kind == ZoneKind.Portal && z.Link > i && z.Link < room.Zones.Count)
                {
                    Handles.color = new Color(0.7f, 0.4f, 1f, 0.7f);
                    Handles.DrawDottedLine(ToScreen(z.Center), ToScreen(room.Zones[z.Link].Center), 4f);
                }
                if (z.Kind == ZoneKind.Switch) Label(z.Center, "S" + z.Group, Color.black);
            }
            else
            {
                FillPoly(Box(z.Center, z.Size, z.Angle), color);
                if (Directional(z.Kind)) Arrow(z.Center, z.Direction, Mathf.Min(z.Size.y * 0.4f, 1.5f));
            }
            if (z.Kind != ZoneKind.Switch) Label(z.Center + new Vector2(0f, -0.3f), z.Kind.ToString().ToUpperInvariant(), new Color(0f, 0f, 0f, 0.6f));
            if (selected)
            {
                if (RoundZone(z.Kind)) Ring(z.Center, z.Size.x * 0.5f + 0.06f, Color.cyan);
                else { Handles.color = Color.cyan; DrawLoop(Box(z.Center, z.Size + new Vector2(0.12f, 0.12f), z.Angle), 2f); }
                Handle(ResizeHandle(z), false);
                if (!RoundZone(z.Kind)) Handle(RotateHandle(z.Center, z.Size, z.Angle), true);
            }
        }

        void DrawRail(RailDef r, int i, bool selected)
        {
            var pts = new Vector3[r.Path.Count];
            for (int k = 0; k < pts.Length; k++) pts[k] = ToScreen(r.Path[k]);
            Handles.color = BlockColor(r.Kind);
            Handles.DrawAAPolyLine(Mathf.Max(2f, r.Width * scale), pts);
            if (!selected) return;
            Handles.color = Color.cyan;
            Handles.DrawAAPolyLine(2f, pts);
            if (r.IsArc) Handle(new Vector2(r.ArcArgs[0], r.ArcArgs[1]) * (r.Flipped ? new Vector2(-1f, 1f) : Vector2.one), false);
            else
                for (int k = 0; k < r.Points.Count; k++)
                    Handle(RailPoint(r, k), false, sel.Kind == Pick.RailPoint && sel.Sub == k);
        }

        void DrawShapePoints()
        {
            var pts = ShapePoints();
            for (int k = 0; k < pts.Count; k++) Handle(pts[k], false, sel.Kind == Pick.ShapePoint && sel.Sub == k);
        }

        void DrawSolution()
        {
            var colors = new[] { new Color(0.1f, 0.85f, 0.4f), new Color(0.2f, 0.6f, 1f), new Color(1f, 0.4f, 0.8f), new Color(1f, 0.6f, 0.1f), new Color(0.6f, 0.4f, 1f) };
            for (int s = 0; s < check.Solution.Count; s++)
            {
                var shot = check.Solution[s];
                if (shot.Path == null || shot.Path.Count < 2) continue;
                var pts = new List<Vector3> { ToScreen(shot.From.Puck) };
                foreach (var p in shot.Path) pts.Add(ToScreen(p));
                Handles.color = colors[s % colors.Length];
                Handles.DrawAAPolyLine(3f, pts.ToArray());
                Label(shot.From.Puck + new Vector2(0.35f, 0.35f), "shot " + (s + 1), colors[s % colors.Length]);
            }
        }

        // ---------- Drawing helpers ----------

        void Disc(Vector2 c, float r, Color color)
        {
            Handles.color = color;
            Handles.DrawSolidDisc(ToScreen(c), Vector3.forward, r * scale);
        }

        void Ring(Vector2 c, float r, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(ToScreen(c), Vector3.forward, r * scale, 2f);
        }

        void FillPoly(List<Vector2> world, Color color)
        {
            Handles.color = color;
            var tris = new List<int>();
            var verts = Geometry.Triangulate(world, null, tris);
            for (int i = 0; i + 2 < tris.Count; i += 3)
                Handles.DrawAAConvexPolygon(ToScreen(verts[tris[i]]), ToScreen(verts[tris[i + 1]]), ToScreen(verts[tris[i + 2]]));
        }

        void DrawLoop(IList<Vector2> world, float width)
        {
            var pts = new Vector3[world.Count + 1];
            for (int i = 0; i < world.Count; i++) pts[i] = ToScreen(world[i]);
            pts[world.Count] = pts[0];
            Handles.DrawAAPolyLine(width, pts);
        }

        void Arrow(Vector2 at, Vector2 dir, float length)
        {
            Handles.color = new Color(0f, 0f, 0f, 0.55f);
            Vector2 tip = at + dir * length * 0.5f, tail = at - dir * length * 0.5f;
            Vector2 side = new Vector2(-dir.y, dir.x) * 0.25f;
            Handles.DrawAAPolyLine(3f, ToScreen(tail), ToScreen(tip));
            Handles.DrawAAPolyLine(3f, ToScreen(tip - dir * 0.3f + side), ToScreen(tip), ToScreen(tip - dir * 0.3f - side));
        }

        void Label(Vector2 at, string text, Color color)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = color } };
            var s = ToScreen(at);
            GUI.Label(new Rect(s.x - 50f, s.y - 8f, 100f, 16f), text, style);
        }

        void Handle(Vector2 at, bool round, bool active = false)
        {
            var s = ToScreen(at);
            Handles.color = active ? Color.yellow : Color.cyan;
            if (round) Handles.DrawSolidDisc(s, Vector3.forward, 5f);
            else Handles.DrawAAConvexPolygon(s + new Vector2(-5, -5), s + new Vector2(5, -5), s + new Vector2(5, 5), s + new Vector2(-5, 5));
        }

        static List<Vector2> Box(Vector2 c, Vector2 size, float angle)
        {
            Vector2 h = size * 0.5f;
            return new List<Vector2>
            {
                c + LevelLint.Rotate(new Vector2(-h.x, -h.y), angle), c + LevelLint.Rotate(new Vector2(h.x, -h.y), angle),
                c + LevelLint.Rotate(new Vector2(h.x, h.y), angle), c + LevelLint.Rotate(new Vector2(-h.x, h.y), angle),
            };
        }

        static List<Vector2> RoundedBox(Vector2 c, Vector2 size, float angle, float corner)
        {
            var local = Geometry.Fillet(Box(Vector2.zero, size, 0f), Mathf.Max(0f, corner));
            for (int i = 0; i < local.Count; i++) local[i] = c + LevelLint.Rotate(local[i], angle);
            return local;
        }

        static Vector2 ResizeHandle(BlockDef b) =>
            b.Shape == BlockShape.Post ? b.Center + new Vector2(b.Size.x * 0.5f, 0f) : b.Center + LevelLint.Rotate(b.Size * 0.5f, b.Angle);

        static Vector2 ResizeHandle(ZoneDef z) =>
            RoundZone(z.Kind) ? z.Center + new Vector2(z.Size.x * 0.5f, 0f) : z.Center + LevelLint.Rotate(z.Size * 0.5f, z.Angle);

        static Vector2 RotateHandle(Vector2 c, Vector2 size, float angle) => c + LevelLint.Rotate(new Vector2(0f, size.y * 0.5f + 0.45f), angle);

        // Rail control points as seen on the board (a flipped rail shows them mirrored).
        static Vector2 RailPoint(RailDef r, int k) => r.Flipped ? new Vector2(-r.Points[k].x, r.Points[k].y) : r.Points[k];

        // Polygon floor corners as seen on the board.
        List<Vector2> ShapePoints()
        {
            var s = room.Shape;
            var pts = new List<Vector2>();
            for (int i = 1; i + 1 < s.Args.Length; i += 2)
            {
                var p = new Vector2(s.Args[i], s.Args[i + 1]);
                if (s.Flipped) p.x = -p.x;
                pts.Add(p + s.Offset);
            }
            return pts;
        }

        // Editing a corner bakes any flip and offset into the points, so what you drag is where it goes.
        void SetShapePoints(List<Vector2> pts)
        {
            var args = new float[1 + pts.Count * 2];
            args[0] = room.Shape.Args[0];
            for (int i = 0; i < pts.Count; i++) { args[1 + i * 2] = pts[i].x; args[2 + i * 2] = pts[i].y; }
            room.Shape = ShapeDef.Make(ShapeKind.Polygon, args);
        }

        static void BakeFlip(RailDef r)
        {
            if (!r.Flipped || r.IsArc) return;
            var pts = new List<Vector2>();
            foreach (var p in r.Points) pts.Add(new Vector2(-p.x, p.y));
            r.Points = pts;
            r.Flipped = false;
        }

        // ---------- Input ----------

        float SnapValue(float v) => snap <= 0f ? v : Mathf.Round(v / snap) * snap;
        Vector2 SnapPoint(Vector2 v) => new Vector2(SnapValue(v.x), SnapValue(v.y));


        bool Near(Vector2 world, Vector2 mouse, float px = 9f) => (ToScreen(world) - mouse).magnitude <= px;

        Sel HitTest(Vector2 mouse)
        {
            var w = ToWorld(mouse);
            // Handles of the current selection first.
            if (room.Shape.Kind == ShapeKind.Polygon)
            {
                var pts = ShapePoints();
                for (int k = 0; k < pts.Count; k++) if (Near(pts[k], mouse)) return new Sel { Kind = Pick.ShapePoint, Index = 0, Sub = k };
            }
            if ((sel.Kind == Pick.Rail || sel.Kind == Pick.RailPoint) && sel.Index < room.Rails.Count && !room.Rails[sel.Index].IsArc)
            {
                var r = room.Rails[sel.Index];
                for (int k = 0; k < r.Points.Count; k++) if (Near(RailPoint(r, k), mouse)) return new Sel { Kind = Pick.RailPoint, Index = sel.Index, Sub = k };
            }
            for (int i = room.Stars.Count - 1; i >= 0; i--) if ((room.Stars[i] - w).magnitude < 0.3f) return new Sel { Kind = Pick.Star, Index = i };
            if ((room.Start - w).magnitude < Rules.PuckRadius + 0.05f) return new Sel { Kind = Pick.Start };
            for (int i = room.Blocks.Count - 1; i >= 0; i--) if (LevelLint.Covers(room.Blocks[i], w, 0.05f)) return new Sel { Kind = Pick.Block, Index = i };
            for (int i = room.Rails.Count - 1; i >= 0; i--)
            {
                var path = room.Rails[i].Path;
                for (int k = 0; k + 1 < path.Count; k++)
                    if (HandleUtility.DistancePointLine(w, path[k], path[k + 1]) < room.Rails[i].Width * 0.5f + 0.05f) return new Sel { Kind = Pick.Rail, Index = i };
            }
            if ((room.Hole - w).magnitude < Rules.HoleRadius) return new Sel { Kind = Pick.Hole };
            for (int i = room.Zones.Count - 1; i >= 0; i--)
            {
                var z = room.Zones[i];
                bool hit = RoundZone(z.Kind) ? (z.Center - w).magnitude < z.Size.x * 0.5f
                    : Mathf.Abs(LevelLint.Rotate(w - z.Center, -z.Angle).x) < z.Size.x * 0.5f && Mathf.Abs(LevelLint.Rotate(w - z.Center, -z.Angle).y) < z.Size.y * 0.5f;
                if (hit) return new Sel { Kind = Pick.Zone, Index = i };
            }
            return Sel.None;
        }

        void BoardInput(Event e)
        {
            Vector2 mouse = e.mousePosition;
            if (!board.Contains(mouse) && drag == DragMode.None && e.type != EventType.MouseUp) return;
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.4f, 6f);
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 2 || e.button == 0 && e.alt:
                    drag = DragMode.Pan;
                    dragStartMouse = mouse;
                    dragStartValue = pan;
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 0:
                    GUI.FocusControl(null);
                    BeginDrag(mouse);
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 1:
                    ContextMenu(mouse);
                    e.Use();
                    break;

                case EventType.MouseDrag when drag != DragMode.None:
                    DoDrag(mouse, e.shift);
                    e.Use();
                    break;

                case EventType.MouseUp when drag != DragMode.None:
                    if (drag != DragMode.Pan && dragChanged) Commit();
                    drag = DragMode.None;
                    e.Use();
                    break;

                case EventType.MouseMove:
                    Repaint();
                    break;
            }
        }

        void BeginDrag(Vector2 mouse)
        {
            dragChanged = false;
            dragStartMouse = mouse;
            // A handle of the selected block or zone?
            if (sel.Kind == Pick.Block && sel.Index < room.Blocks.Count)
            {
                var b = room.Blocks[sel.Index];
                if (Near(ResizeHandle(b), mouse)) { StartDrag(DragMode.Resize, b.Center, b.Size, b.Angle); return; }
                if (b.Shape != BlockShape.Post && Near(RotateHandle(b.Center, b.Size, b.Angle), mouse)) { StartDrag(DragMode.Rotate, b.Center, b.Size, b.Angle); return; }
            }
            if (sel.Kind == Pick.Zone && sel.Index < room.Zones.Count)
            {
                var z = room.Zones[sel.Index];
                if (Near(ResizeHandle(z), mouse)) { StartDrag(DragMode.Resize, z.Center, z.Size, z.Angle); return; }
                if (!RoundZone(z.Kind) && Near(RotateHandle(z.Center, z.Size, z.Angle), mouse)) { StartDrag(DragMode.Rotate, z.Center, z.Size, z.Angle); return; }
            }
            sel = HitTest(mouse);
            if (sel.Kind == Pick.None) { drag = DragMode.None; Repaint(); return; }
            StartDrag(DragMode.Move, PositionOf(sel), Vector2.zero, 0f);
        }

        void StartDrag(DragMode mode, Vector2 value, Vector2 size, float angle)
        {
            drag = mode;
            dragStartValue = value;
            dragStartSize = size;
            dragStartAngle = angle;
        }

        Vector2 PositionOf(Sel s)
        {
            switch (s.Kind)
            {
                case Pick.Block: return room.Blocks[s.Index].Center;
                case Pick.Zone: return room.Zones[s.Index].Center;
                case Pick.Star: return room.Stars[s.Index];
                case Pick.Hole: return room.Hole;
                case Pick.Start: return room.Start;
                case Pick.RailPoint: return RailPoint(room.Rails[s.Index], s.Sub);
                case Pick.ShapePoint: return ShapePoints()[s.Sub];
                case Pick.Rail:
                    var r = room.Rails[s.Index];
                    return r.IsArc ? new Vector2(r.ArcArgs[0], r.ArcArgs[1]) : r.Points[0];
            }
            return Vector2.zero;
        }

        void DoDrag(Vector2 mouse, bool fine)
        {
            Vector2 world = ToWorld(mouse), start = ToWorld(dragStartMouse);
            if (drag == DragMode.Pan)
            {
                pan = dragStartValue + new Vector2(-(mouse.x - dragStartMouse.x), mouse.y - dragStartMouse.y) / scale;
                Repaint();
                return;
            }
            if ((mouse - dragStartMouse).magnitude < 2f && !dragChanged) return;
            dragChanged = true;
            if (drag == DragMode.Resize) Resize(world);
            else if (drag == DragMode.Rotate) Rotate(world, fine);
            else MoveTo(SnapPoint(dragStartValue + (world - start)));
            Repaint();
        }

        void Resize(Vector2 world)
        {
            if (sel.Kind == Pick.Block)
            {
                var b = room.Blocks[sel.Index];
                if (b.Shape == BlockShape.Post)
                {
                    float r = Mathf.Max(0.1f, SnapValue((world - b.Center).magnitude));
                    b.Size = new Vector2(r * 2f, r * 2f);
                }
                else
                {
                    var local = LevelLint.Rotate(world - b.Center, -b.Angle);
                    b.Size = new Vector2(Mathf.Max(0.1f, SnapValue(Mathf.Abs(local.x) * 2f)), Mathf.Max(0.1f, SnapValue(Mathf.Abs(local.y) * 2f)));
                }
            }
            else if (sel.Kind == Pick.Zone)
            {
                var z = room.Zones[sel.Index];
                if (RoundZone(z.Kind))
                {
                    float r = Mathf.Max(0.1f, SnapValue((world - z.Center).magnitude));
                    z.Size = new Vector2(r * 2f, r * 2f);
                }
                else
                {
                    var local = LevelLint.Rotate(world - z.Center, -z.Angle);
                    z.Size = new Vector2(Mathf.Max(0.1f, SnapValue(Mathf.Abs(local.x) * 2f)), Mathf.Max(0.1f, SnapValue(Mathf.Abs(local.y) * 2f)));
                }
            }
        }

        void Rotate(Vector2 world, bool fine)
        {
            Vector2 c = dragStartValue, d = world - c;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
            float step = fine ? 1f : angleSnap;
            angle = Mathf.Round(Mathf.DeltaAngle(0f, angle) / step) * step;
            if (sel.Kind == Pick.Block) room.Blocks[sel.Index].Angle = angle;
            else if (sel.Kind == Pick.Zone) room.Zones[sel.Index].Angle = angle;
        }

        void MoveTo(Vector2 p)
        {
            switch (sel.Kind)
            {
                case Pick.Block: room.Blocks[sel.Index].Center = p; break;
                case Pick.Zone: room.Zones[sel.Index].Center = p; break;
                case Pick.Star: room.Stars[sel.Index] = p; break;
                case Pick.Hole: room.Hole = p; break;
                case Pick.Start: room.Start = p; break;
                case Pick.RailPoint:
                {
                    var r = room.Rails[sel.Index];
                    BakeFlip(r);
                    r.Points = new List<Vector2>(r.Points) { [sel.Sub] = p };
                    r.Rebuild();
                    break;
                }
                case Pick.Rail:
                {
                    var r = room.Rails[sel.Index];
                    if (r.IsArc)
                    {
                        var a = (float[])r.ArcArgs.Clone();
                        a[0] = p.x; a[1] = p.y;
                        r.ArcArgs = a;
                    }
                    else
                    {
                        Vector2 d = p - r.Points[0];
                        var pts = new List<Vector2>();
                        foreach (var q in r.Points) pts.Add(q + d);
                        r.Points = pts;
                    }
                    r.Rebuild();
                    break;
                }
                case Pick.ShapePoint:
                {
                    var pts = ShapePoints();
                    pts[sel.Sub] = p;
                    SetShapePoints(pts);
                    break;
                }
            }
        }

        void ContextMenu(Vector2 mouse)
        {
            var w = SnapPoint(ToWorld(mouse));
            var hit = HitTest(mouse);
            var menu = new GenericMenu();
            if (hit.Kind == Pick.ShapePoint)
            {
                menu.AddItem(new GUIContent("Add corner after this"), false, () => { var p = ShapePoints(); p.Insert(hit.Sub + 1, (p[hit.Sub] + p[(hit.Sub + 1) % p.Count]) * 0.5f); SetShapePoints(p); Commit(); });
                if (ShapePoints().Count > 3) menu.AddItem(new GUIContent("Remove corner"), false, () => { var p = ShapePoints(); p.RemoveAt(hit.Sub); SetShapePoints(p); Commit(); });
                menu.AddSeparator("");
            }
            if (hit.Kind == Pick.RailPoint)
            {
                var r = room.Rails[hit.Index];
                menu.AddItem(new GUIContent("Add point after this"), false, () =>
                {
                    BakeFlip(r);
                    var p = new List<Vector2>(r.Points);
                    var next = hit.Sub + 1 < p.Count ? p[hit.Sub + 1] : p[hit.Sub] + new Vector2(1f, 0f);
                    p.Insert(hit.Sub + 1, (p[hit.Sub] + next) * 0.5f);
                    r.Points = p;
                    r.Rebuild();
                    Commit();
                });
                if (r.Points.Count > 2) menu.AddItem(new GUIContent("Remove point"), false, () => { BakeFlip(r); var p = new List<Vector2>(r.Points); p.RemoveAt(hit.Sub); r.Points = p; r.Rebuild(); sel = new Sel { Kind = Pick.Rail, Index = hit.Index }; Commit(); });
                menu.AddSeparator("");
            }
            if (hit.Kind != Pick.None && hit.Kind != Pick.Hole && hit.Kind != Pick.Start && hit.Kind != Pick.ShapePoint && hit.Kind != Pick.RailPoint)
            {
                menu.AddItem(new GUIContent("Duplicate"), false, () => { sel = hit; DuplicateSelection(); });
                menu.AddItem(new GUIContent("Delete"), false, () => { sel = hit; DeleteSelection(); });
                menu.AddSeparator("");
            }
            void AddBlock(string path, BlockKind kind, Vector2 size, BlockShape shape = BlockShape.Box, System.Action<BlockDef> setup = null) =>
                menu.AddItem(new GUIContent("Add block/" + path), false, () =>
                {
                    var b = new BlockDef(kind, w, size) { Shape = shape };
                    setup?.Invoke(b);
                    room.Blocks.Add(b);
                    sel = new Sel { Kind = Pick.Block, Index = room.Blocks.Count - 1 };
                    Commit();
                });
            AddBlock("Wall (desk)", BlockKind.Wall, new Vector2(3f, 0.6f));
            AddBlock("Chair (bouncer post)", BlockKind.Bouncer, new Vector2(0.9f, 0.9f), BlockShape.Post);
            AddBlock("Bouncer bar", BlockKind.Bouncer, new Vector2(2.4f, 0.5f));
            AddBlock("Cactus (hazard post)", BlockKind.Hazard, new Vector2(0.8f, 0.8f), BlockShape.Post);
            AddBlock("Hazard bar", BlockKind.Hazard, new Vector2(2.4f, 0.45f));
            AddBlock("Moving hazard", BlockKind.Hazard, new Vector2(2.4f, 0.45f), BlockShape.Box, b => { b.MoveOffset = new Vector2(2f, 0f); b.Period = 2.6f; });
            AddBlock("Turnstile (spinning wall)", BlockKind.Wall, new Vector2(3f, 0.4f), BlockShape.Box, b => b.Spin = 90f);
            AddBlock("Sticky desk", BlockKind.Sticky, new Vector2(2.4f, 0.6f));
            AddBlock("Parcel (crate)", BlockKind.Crate, new Vector2(0.9f, 0.9f));
            AddBlock("Glass wall (breakable)", BlockKind.Breakable, new Vector2(3f, 0.6f));
            AddBlock("Boss chair (kicker)", BlockKind.Kicker, new Vector2(0.9f, 0.9f), BlockShape.Post);
            AddBlock("Pop-up wall", BlockKind.PopWall, new Vector2(4f, 0.5f), BlockShape.Box, b => b.BlinkPeriod = 2.4f);
            AddBlock("Gate (badge reader)", BlockKind.Gate, new Vector2(3f, 0.5f), BlockShape.Box, b => b.Group = 0);
            foreach (ZoneKind kind in System.Enum.GetValues(typeof(ZoneKind)))
            {
                var k = kind;
                if (k == ZoneKind.Portal) continue;
                menu.AddItem(new GUIContent("Add zone/" + k), false, () =>
                {
                    var z = new ZoneDef { Kind = k, Center = w, Size = RoundZone(k) ? new Vector2(k == ZoneKind.Switch ? 1.1f : 2.4f, k == ZoneKind.Switch ? 1.1f : 2.4f) : k == ZoneKind.OneWay ? new Vector2(3f, 0.7f) : new Vector2(2.4f, 3f) };
                    if (k == ZoneKind.Spikes) z.Period = 2f;
                    if (k == ZoneKind.Switch) z.Group = 0;
                    room.Zones.Add(z);
                    sel = new Sel { Kind = Pick.Zone, Index = room.Zones.Count - 1 };
                    Commit();
                });
            }
            menu.AddItem(new GUIContent("Add zone/Portal pair"), false, () =>
            {
                int a = room.Zones.Count;
                room.Zones.Add(new ZoneDef { Kind = ZoneKind.Portal, Center = w, Size = new Vector2(1.1f, 1.1f), Link = a + 1 });
                room.Zones.Add(new ZoneDef { Kind = ZoneKind.Portal, Center = w + new Vector2(2f, 0f), Size = new Vector2(1.1f, 1.1f), Link = a });
                sel = new Sel { Kind = Pick.Zone, Index = a };
                Commit();
            });
            menu.AddItem(new GUIContent("Add rail/Straight"), false, () => AddRail(RailDef.Bent(BlockKind.Wall, 0f, new List<Vector2> { w, w + new Vector2(2f, 0f) })));
            menu.AddItem(new GUIContent("Add rail/Bent (L)"), false, () => AddRail(RailDef.Bent(BlockKind.Wall, 0.3f, new List<Vector2> { w, w + new Vector2(2f, 0f), w + new Vector2(2f, 2f) })));
            menu.AddItem(new GUIContent("Add rail/Arc"), false, () => AddRail(RailDef.Curve(BlockKind.Wall, w.x, w.y, 2f, 0f, 180f)));
            menu.AddItem(new GUIContent("Add coffee"), false, () => { room.Stars.Add(w); sel = new Sel { Kind = Pick.Star, Index = room.Stars.Count - 1 }; Commit(); });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Put the bed here"), false, () => { room.Hole = w; Commit(); });
            menu.AddItem(new GUIContent("Put the start here"), false, () => { room.Start = w; Commit(); });
            if (EditorGUIUtility.systemCopyBuffer.StartsWith(Clip)) menu.AddItem(new GUIContent("Paste here"), false, () => Paste(w));
            menu.ShowAsContext();
        }

        void AddRail(RailDef r)
        {
            room.Rails.Add(r);
            sel = new Sel { Kind = Pick.Rail, Index = room.Rails.Count - 1 };
            Commit();
        }

        void DeleteSelection()
        {
            switch (sel.Kind)
            {
                case Pick.Block: room.Blocks.RemoveAt(sel.Index); break;
                case Pick.Zone:
                    // Keep portal links pointing at the right zones.
                    int gone = sel.Index;
                    room.Zones.RemoveAt(gone);
                    foreach (var z in room.Zones) z.Link = z.Link == gone ? -1 : z.Link > gone ? z.Link - 1 : z.Link;
                    break;
                case Pick.Rail: room.Rails.RemoveAt(sel.Index); break;
                case Pick.Star: room.Stars.RemoveAt(sel.Index); break;
                default: return;
            }
            sel = Sel.None;
            Commit();
        }

        string Copy()
        {
            switch (sel.Kind)
            {
                case Pick.Block: return Clip + "block:" + JsonLite.Write(LevelData.BlockObj(room.Blocks[sel.Index]));
                case Pick.Zone: return Clip + "zone:" + JsonLite.Write(LevelData.ZoneObj(room.Zones[sel.Index]));
                case Pick.Rail: return Clip + "rail:" + JsonLite.Write(LevelData.RailObj(room.Rails[sel.Index]));
                case Pick.Star: return Clip + "star:" + JsonLite.Write(new List<object> { room.Stars[sel.Index].x, room.Stars[sel.Index].y });
            }
            return null;
        }

        void Paste(Vector2? at)
        {
            string text = EditorGUIUtility.systemCopyBuffer;
            if (!text.StartsWith(Clip)) return;
            text = text.Substring(Clip.Length);
            int colon = text.IndexOf(':');
            if (colon < 0) return;
            string kind = text.Substring(0, colon);
            object value;
            try { value = JsonLite.Parse(text.Substring(colon + 1)); }
            catch (JsonLite.ParseException) { return; }
            var offset = new Vector2(snap > 0f ? Mathf.Max(snap, 0.5f) : 0.5f, -0.5f);
            try
            {
                switch (kind)
                {
                    case "block":
                        var b = LevelData.ParseBlock((Dictionary<string, object>)value, room.Id);
                        b.Center = at ?? b.Center + offset;
                        b.Reception = 0;
                        room.Blocks.Add(b);
                        sel = new Sel { Kind = Pick.Block, Index = room.Blocks.Count - 1 };
                        break;
                    case "zone":
                        var z = LevelData.ParseZone((Dictionary<string, object>)value, room.Id);
                        z.Center = at ?? z.Center + offset;
                        z.Link = -1;
                        room.Zones.Add(z);
                        sel = new Sel { Kind = Pick.Zone, Index = room.Zones.Count - 1 };
                        break;
                    case "rail":
                        var r = LevelData.ParseRail((Dictionary<string, object>)value, room.Id);
                        room.Rails.Add(r);
                        sel = new Sel { Kind = Pick.Rail, Index = room.Rails.Count - 1 };
                        MoveTo(SnapPoint(at ?? PositionOf(sel) + offset));
                        break;
                    case "star":
                        var list = (List<object>)value;
                        var p = new Vector2(((JsonLite.Number)list[0]).Float, ((JsonLite.Number)list[1]).Float);
                        room.Stars.Add(at ?? p + offset);
                        sel = new Sel { Kind = Pick.Star, Index = room.Stars.Count - 1 };
                        break;
                    default: return;
                }
            }
            catch (System.Exception e) when (e is LevelData.FormatException || e is System.InvalidCastException)
            {
                ShowNotification(new GUIContent("Can't paste that: " + e.Message));
                return;
            }
            Commit();
        }

        void DuplicateSelection()
        {
            string copied = Copy();
            if (copied == null) return;
            string keep = EditorGUIUtility.systemCopyBuffer;
            EditorGUIUtility.systemCopyBuffer = copied;
            Paste(null);
            EditorGUIUtility.systemCopyBuffer = keep;
        }

        void Nudge(Vector2 d)
        {
            if (sel.Kind == Pick.None) return;
            MoveTo(PositionOf(sel) + d);
            Commit();
        }

        void HandleShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || room == null) return;
            // Typing in a field keeps its keys.
            if (EditorGUIUtility.editingTextField) return;
            bool ctrl = e.control || e.command;
            float step = snap > 0f ? snap : 0.05f;
            switch (e.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace: DeleteSelection(); break;
                case KeyCode.Z when ctrl && e.shift: Redo(); break;
                case KeyCode.Z when ctrl: Undo(); break;
                case KeyCode.Y when ctrl: Redo(); break;
                case KeyCode.S when ctrl: SaveAll(); break;
                case KeyCode.D when ctrl: DuplicateSelection(); break;
                case KeyCode.C when ctrl: { var c = Copy(); if (c != null) EditorGUIUtility.systemCopyBuffer = c; break; }
                case KeyCode.V when ctrl: Paste(null); break;
                case KeyCode.LeftArrow: Nudge(new Vector2(-step, 0f)); break;
                case KeyCode.RightArrow: Nudge(new Vector2(step, 0f)); break;
                case KeyCode.UpArrow: Nudge(new Vector2(0f, step)); break;
                case KeyCode.DownArrow: Nudge(new Vector2(0f, -step)); break;
                case KeyCode.Escape: sel = Sel.None; break;
                default: return;
            }
            e.Use();
            Repaint();
        }

        // Exercises the editor's model without a screen (OUT-12), for Run All Checks: open a room, add, move, copy,
        // paste and delete things, undo and redo back to the start, duplicate and mirror the room. Nothing is saved.
        // Returns a list of what went wrong (empty = fine).
        public static List<string> SelfTest()
        {
            var fails = new List<string>();
            var w = CreateInstance<LevelEditorWindow>();
            string keepClip = EditorGUIUtility.systemCopyBuffer;
            try
            {
                w.Reload();
                if (w.rooms.Count != Levels.All.Length + 1) fails.Add($"loaded {w.rooms.Count} rooms, expected {Levels.All.Length + 1}");
                w.Select("first");
                if (w.room == null) { fails.Add("can't open the room \"first\""); return fails; }
                string original = w.committed;
                int blocks = w.room.Blocks.Count;

                w.room.Blocks.Add(new BlockDef(BlockKind.Wall, new Vector2(1f, 1f), new Vector2(2f, 0.6f)));
                w.sel = new Sel { Kind = Pick.Block, Index = blocks };
                w.Commit();
                w.MoveTo(new Vector2(1.5f, -2f));
                w.Commit();
                if (w.room.Blocks[blocks].Center != new Vector2(1.5f, -2f)) fails.Add("move didn't move the block");
                EditorGUIUtility.systemCopyBuffer = w.Copy();
                w.Paste(null);
                if (w.room.Blocks.Count != blocks + 2) fails.Add("copy and paste didn't add a block");
                w.DeleteSelection();
                if (w.room.Blocks.Count != blocks + 1) fails.Add("delete didn't remove the pasted block");
                w.room.Rails.Add(RailDef.Bent(BlockKind.Wall, 0.3f, new List<Vector2> { Vector2.zero, new Vector2(2f, 0f), new Vector2(2f, 2f) }, true));
                w.Commit();
                w.sel = new Sel { Kind = Pick.RailPoint, Index = w.room.Rails.Count - 1, Sub = 1 };
                w.MoveTo(new Vector2(-3f, 0f));
                w.Commit();
                var rail = w.room.Rails[w.room.Rails.Count - 1];
                if (rail.Flipped || RailPoint(rail, 1) != new Vector2(-3f, 0f)) fails.Add("dragging a flipped rail's point doesn't put it under the mouse");
                if (LevelData.ToJson(LevelData.FromJson(w.committed)) != w.committed) fails.Add("an edited room doesn't round-trip");

                while (w.undo.Count > 0) w.Undo();
                if (w.committed != original) fails.Add("undoing every step doesn't give back the original room");
                int steps = 0;
                while (w.redo.Count > 0) { w.Redo(); steps++; }
                if (steps != 6 || w.room.Blocks.Count != blocks + 1) fails.Add($"redo replayed {steps} steps, expected 6");

                int roomsBefore = w.rooms.Count;
                w.Select("first");
                w.Duplicate(true);
                if (w.rooms.Count != roomsBefore + 1 || w.room.Id == "first" || !w.room.Tags.Contains("remix:first")) fails.Add("mirror didn't make a tagged remix");
                else if (w.room.Hole.x != -Levels.All[0].Hole.x) fails.Add("the mirrored room isn't flipped");
                w.Duplicate(false);
                if (w.rooms.Count != roomsBefore + 2) fails.Add("duplicate didn't make a room");
                if (w.ChapterOf(w.room.Id) != 0) fails.Add("a duplicate should land in its original's chapter");
                w.NewRoom(ShapeDef.Circle(7.2f));
                if (LevelLint.Errors(w.problems) != 0) fails.Add("a new room starts with layout errors: " + string.Join("; ", w.problems));
            }
            catch (System.Exception e)
            {
                fails.Add(e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                EditorGUIUtility.systemCopyBuffer = keepClip;
                w.dirty.Clear();
                w.manifestDirty = false;
                DestroyImmediate(w);
            }
            return fails;
        }

        // A small window for typing a new id.
        class RenamePopup : EditorWindow
        {
            public string Value;
            public System.Action<string> Done;

            void OnGUI()
            {
                titleContent = new GUIContent("Rename id");
                GUILayout.Label("New id (lower case letters, digits, dashes):", EditorStyles.wordWrappedLabel);
                GUI.SetNextControlName("id");
                Value = EditorGUILayout.TextField(Value);
                EditorGUI.FocusTextInControl("id");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Rename") || Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    Close();
                    Done?.Invoke(Value.Trim());
                }
                if (GUILayout.Button("Cancel")) Close();
                GUILayout.EndHorizontal();
            }
        }
    }
}
