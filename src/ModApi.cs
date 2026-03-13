using HarmonyLib;
using System;
using System.Linq;

namespace AutoReadMod
{
    public class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            SafeLog("Loading Auto-Read Mod");
            
            // Load configuration
            var config = ModConfig.Instance;
            SafeLog($"Auto-Read Mod config: Enabled={config.Enabled}, Books={config.ReadBooks}, Magazines={config.ReadMagazines}, Schematics={config.ReadSchematics}");
            
            // Apply Harmony patches
            var harmony = new Harmony("com.learninggrounds.autoreadmod");
            harmony.PatchAll();
            
            SafeLog("Auto-Read Mod loaded successfully");
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
