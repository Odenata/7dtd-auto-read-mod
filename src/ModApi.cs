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

            try
            {
                var harmony = new Harmony("com.learninggrounds.autoreadmod");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
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
