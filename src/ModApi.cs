using System;
using System.Reflection;
using HarmonyLib;

namespace AutoReadMod
{
    public class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            SafeLog("Loading AutoReadMod");
            
            var config = ModConfig.Instance;
            SafeLog($"Config: Enabled={config.Enabled}, DebugMode={config.DebugMode}");
            AutoReader.EmitDebugFileLocation();

            try
            {
                var harmony = new Harmony("com.learninggrounds.autoreadmod");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                var updateTarget = XUiUpdatePatch.FindUpdateTarget();
                if (updateTarget != null)
                {
                    harmony.Patch(updateTarget, postfix: new HarmonyMethod(typeof(XUiUpdatePatch), "Postfix"));
                    SafeLog("Per-frame update patch applied");
                }
                else
                {
                    SafeLog("Per-frame update target not found (deferred container + visualization may not run)");
                }

                try
                {
                    var gmUpdate = typeof(GameManager).GetMethod(
                        "Update",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        System.Type.EmptyTypes,
                        null);
                    if (gmUpdate != null)
                    {
                        harmony.Patch(
                            gmUpdate,
                            postfix: new HarmonyMethod(typeof(GameManagerDeferredTickHook), nameof(GameManagerDeferredTickHook.GameManagerUpdatePostfix)));
                        SafeLog("GameManager.Update deferred-tick backup patch applied");
                    }
                }
                catch (Exception gmEx)
                {
                    SafeLog($"GameManager.Update backup patch skipped: {gmEx.Message}");
                }
                SafeLog("Harmony patches applied");
            }
            catch (Exception ex)
            {
                SafeLog($"Harmony patch failed: {ex.Message}");
                SafeLog(ex.StackTrace ?? "");
            }

            SafeLog("AutoReadMod loaded successfully");
        }

        private static void SafeLog(string message)
        {
            try
            {
                UnityEngine.Debug.Log(message);
            }
            catch
            {
                // Unity not available (test environment)
            }
        }
    }
}
