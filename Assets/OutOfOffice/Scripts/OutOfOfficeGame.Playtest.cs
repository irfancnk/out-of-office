namespace OutOfOffice
{
    // "Play this room" from the level editor (OUT-12): the editor names a room and enters Play mode, and the game
    // opens straight into it, past the title and the energy cost. Editor only; a build never does this.
    public partial class OutOfOfficeGame
    {
        public const string PlaytestKey = "OutOfOffice.Playtest";

        // True when the editor asked for a room and it is now loaded and ready to shoot.
        bool StartPlaytest()
        {
#if UNITY_EDITOR
            string id = UnityEditor.SessionState.GetString(PlaytestKey, "");
            if (string.IsNullOrEmpty(id)) return false;
            UnityEditor.SessionState.EraseString(PlaytestKey);  // once: the next Play starts the game as normal
            int index = id == Levels.Tutorial.Id ? TutorialIndex : Levels.IndexOf(id);
            if (index == -1) return false;
            LoadLevel(index);
            hud.HideAll();
            hud.HideTitle();
            state = State.Ready;
            UnityEngine.Debug.Log($"Out of Office: playtesting {id} from the level editor");
            return true;
#else
            return false;
#endif
        }
    }
}
