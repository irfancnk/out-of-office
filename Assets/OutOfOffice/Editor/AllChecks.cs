using UnityEditor;

namespace OutOfOffice.EditorTools
{
    // Batch entry point for every automated check: Unity -batchmode -executeMethod OutOfOffice.EditorTools.AllChecks.Run
    public static class AllChecks
    {
        [MenuItem("Out of Office/Run All Checks")]
        public static void RunFromMenu()
        {
            PhysicsTests.Run();
            LevelCheck.Run(Levels.All);
        }

        public static void Run()
        {
            int physics = PhysicsTests.Run();
            int levels = LevelCheck.Run(Levels.All);
            EditorApplication.Exit(physics == 0 && levels == 0 ? 0 : 4);
        }
    }
}
