using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AutoReadMod
{
    /// <summary>
    /// Thread-safe queue of visualization events produced by the auto-read pipeline and consumed by the overlay.
    /// </summary>
    internal static class AutoReadVisualizationQueue
    {
        private static readonly ConcurrentQueue<AutoReadVisualizationEvent> Queue = new ConcurrentQueue<AutoReadVisualizationEvent>();

        public static void Enqueue(AutoReadVisualizationEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            Queue.Enqueue(evt);
        }

        public static bool TryDequeue(out AutoReadVisualizationEvent evt)
        {
            return Queue.TryDequeue(out evt);
        }

        /// <summary>
        /// Drains the queue and returns all events. Used by tests to assert on emitted visualization data.
        /// </summary>
        internal static List<AutoReadVisualizationEvent> DequeueAllForTest()
        {
            var list = new List<AutoReadVisualizationEvent>();
            while (Queue.TryDequeue(out var evt))
            {
                list.Add(evt);
            }

            return list;
        }
    }
}
