using System;

namespace AutoReadMod
{
    public class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            SafeLog("Loading AutoReadMod");
            
            var config = ModConfig.Instance;
            SafeLog($"Config: Enabled={config.Enabled}, DebugMode={config.DebugMode}");

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
