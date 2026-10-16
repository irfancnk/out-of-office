using System;
using System.Collections.Concurrent;

namespace OutOfOffice
{
    // Hands work from SDK threads to Unity's main thread (OUT-31). Ad and consent SDKs call back on their own
    // threads, where most Unity APIs throw; their callbacks are posted here and run on the next frame.
    public static class MainThread
    {
        static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        public static void Post(Action action)
        {
            if (action != null) queue.Enqueue(action);
        }

        // Called once a frame by the game.
        public static void Drain()
        {
            while (queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }
    }
}
