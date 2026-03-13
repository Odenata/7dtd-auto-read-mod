using System;
using System.Linq;

namespace GameApiExporter
{
    public class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            SafeLog("Loading Game API Exporter");
            
            // Load configuration
            var config = ModConfig.Instance;
            SafeLog($"Game API Exporter config: Enabled={config.Enabled}, ExportOnStartup={config.RuntimeApiExportOnStartup}, TargetAssemblies={config.TargetAssemblies}");
            
            RuntimeApiExporter.TryExportOnStartup();

            SafeLog("Game API Exporter loaded successfully");
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
