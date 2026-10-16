using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Batch entry point for every automated check: Unity -batchmode -executeMethod OutOfOffice.EditorTools.AllChecks.Run
    public static class AllChecks
    {
        [MenuItem("Out of Office/Run All Checks")]
        public static void RunFromMenu()
        {
            int failures = RunAll();
            Debug.Log($"HSALL done, failures: {failures}");
        }

        public static void Run()
        {
            int failures = RunAll();
            Debug.Log($"HSALL done, failures: {failures}");
            EditorApplication.Exit(failures == 0 ? 0 : 4);
        }

        // Physics and save rules, every level solvable, and the cosmetic rig and face placement.
        static int RunAll() => PhysicsTests.Run() + LevelCheck.Run(Levels.All) + PresentationChecks.Run();
    }
}
