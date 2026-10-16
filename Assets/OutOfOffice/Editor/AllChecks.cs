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
        // A suite that throws counts as one failure, and the rest still run.
        static int RunAll() => Safe(PhysicsTests.Run) + Safe(() => LevelCheck.Run(Levels.All)) + Safe(PresentationChecks.Run);

        static int Safe(System.Func<int> suite)
        {
            try { return suite(); }
            catch (System.Exception e) { Debug.LogException(e); return 1; }
        }
    }
}
