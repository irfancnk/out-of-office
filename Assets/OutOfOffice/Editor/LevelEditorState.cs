using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // The level editor's undo snapshot (OUT-12): the open room as JSON. Unity's Undo records this object, so every
    // edit in the editor is one undo step, and an undo or redo puts the room back from this text.
    public class LevelEditorState : ScriptableObject
    {
        public string Json = "";
    }
}
