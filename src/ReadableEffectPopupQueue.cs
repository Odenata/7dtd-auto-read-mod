using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace AutoReadMod
{
    /// <summary>
    /// FIFO queue for effect tooltips. Shows one at a time with ~10s spacing matching
    /// <see cref="ReadableEffectPopup.TimeoutSeconds"/>.
    /// </summary>
    internal static class ReadableEffectPopupQueue
    {
        private static readonly ConcurrentQueue<string> Pending = new ConcurrentQueue<string>();
        private static float _nextShowAllowedAt = -999f;
        private static readonly object Sync = new object();

        internal static int PendingCountForTest => Pending.Count;

        internal static void Enqueue(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            Pending.Enqueue(message.Trim());
        }

        internal static void ClearForTests()
        {
            lock (Sync)
            {
                while (Pending.TryDequeue(out _))
                {
                }

                _nextShowAllowedAt = -999f;
            }
        }

        /// <summary>Drain one message when spacing allows. Call from the main-thread UI tick.</summary>
        internal static void OnUpdate()
        {
            if (ModConfig.Instance == null || !ModConfig.Instance.ShowReadEffects)
                return;

            float now;
            try
            {
                now = Time.time;
            }
            catch
            {
                now = 0f;
            }

            lock (Sync)
            {
                if (now < _nextShowAllowedAt)
                    return;

                if (!Pending.TryDequeue(out var message) || string.IsNullOrWhiteSpace(message))
                    return;

                ReadableEffectPopup.TryShow(message);
                _nextShowAllowedAt = now + ReadableEffectPopup.TimeoutSeconds;
            }
        }
    }
}
