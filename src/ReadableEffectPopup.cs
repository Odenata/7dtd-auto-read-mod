using System;
using System.Reflection;
using UnityEngine;

namespace AutoReadMod
{
    /// <summary>
    /// Shows effect tooltips via reflected <c>GameManager.ShowTooltip</c> (same family as LimitByCraftingSkillMod),
    /// without denied sound or restriction tint. Timeout is 10 seconds.
    /// </summary>
    internal static class ReadableEffectPopup
    {
        internal const float TimeoutSeconds = 10f;

        internal static bool TryShow(string fullText)
        {
            if (string.IsNullOrWhiteSpace(fullText))
                return false;

            var player = GameReflection.TryGetLocalEntityPlayer();
            if (player == null)
            {
                try
                {
                    if (ModConfig.Instance != null && ModConfig.Instance.DebugMode)
                        UnityEngine.Debug.Log("[AutoReadMod] Effect tooltip (no local player): " + fullText);
                }
                catch
                {
                    // ignore
                }

                return false;
            }

            try
            {
                if (TryInvokeShowTooltip(player, fullText.Trim()))
                    return true;
            }
            catch (Exception ex)
            {
                try
                {
                    if (ModConfig.Instance != null && ModConfig.Instance.DebugMode)
                        UnityEngine.Debug.LogWarning("[AutoReadMod] ShowTooltip failed: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }

            try
            {
                UnityEngine.Debug.Log("[AutoReadMod] " + fullText);
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private static bool TryInvokeShowTooltip(object player, string fullText)
        {
            var gmType = typeof(GameManager);
            var gameAssembly = gmType.Assembly;
            var eplType = gameAssembly.GetType("EntityPlayerLocal");
            if (eplType == null || !eplType.IsInstanceOfType(player))
                return false;

            var toolTipEventType = gameAssembly.GetType("ToolTipEvent");
            const string alertSound = "";
            const bool showImmediately = true;
            const bool pinTooltip = false;
            const float timeout = TimeoutSeconds;

            if (toolTipEventType != null)
            {
                var sig9 = new[]
                {
                    eplType, typeof(string), typeof(string), typeof(string),
                    toolTipEventType, typeof(bool), typeof(bool), typeof(float)
                };
                var m9 = gmType.GetMethod("ShowTooltip", BindingFlags.Static | BindingFlags.Public, null, sig9, null);
                if (m9 != null)
                {
                    m9.Invoke(null, new object[]
                    {
                        player, fullText, string.Empty, alertSound, null,
                        showImmediately, pinTooltip, timeout
                    });
                    return true;
                }

                var sig9Arr = new[]
                {
                    eplType, typeof(string), typeof(string[]), typeof(string),
                    toolTipEventType, typeof(bool), typeof(bool), typeof(float)
                };
                var m9a = gmType.GetMethod("ShowTooltip", BindingFlags.Static | BindingFlags.Public, null, sig9Arr, null);
                if (m9a != null)
                {
                    m9a.Invoke(null, new object[]
                    {
                        player, fullText, Array.Empty<string>(), alertSound, null,
                        showImmediately, pinTooltip, timeout
                    });
                    return true;
                }
            }

            var sig5 = new[] { eplType, typeof(string), typeof(bool), typeof(bool), typeof(float) };
            var m5 = gmType.GetMethod("ShowTooltip", BindingFlags.Static | BindingFlags.Public, null, sig5, null);
            if (m5 != null)
            {
                m5.Invoke(null, new object[] { player, fullText, showImmediately, pinTooltip, timeout });
                return true;
            }

            var epType = gameAssembly.GetType("EntityPlayer");
            if (epType != null && epType.IsInstanceOfType(player))
            {
                var sigMp = new[] { epType, typeof(string), typeof(string) };
                var mMp = gmType.GetMethod("ShowTooltipMP", BindingFlags.Static | BindingFlags.Public, null, sigMp, null);
                if (mMp != null)
                {
                    mMp.Invoke(null, new object[] { player, fullText, alertSound });
                    return true;
                }
            }

            return false;
        }
    }
}
