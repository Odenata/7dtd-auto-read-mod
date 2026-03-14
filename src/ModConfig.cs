using System;
using System.IO;
using System.Xml;

namespace AutoReadMod
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;
        public bool ReadBooks { get; set; } = true;
        public bool ReadMagazines { get; set; } = true;
        public bool ReadSchematics { get; set; } = true;
        public bool DebugMode { get; set; } = false;
        public bool ExperimentalApiTesting { get; set; } = false;

        private static ModConfig instance;
        private static string configPath;

        public static ModConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Load();
                }
                return instance;
            }
        }

        private static string GetConfigPath()
        {
            // Prefer mod folder next to DLL (game install Mods/AutoReadMod or 7DaysToDie/Mods/AutoReadMod)
            var assemblyDir = Path.GetDirectoryName(typeof(ModConfig).Assembly.Location);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                var modFolderConfig = Path.Combine(assemblyDir, "Config.xml");
                if (File.Exists(modFolderConfig))
                    return modFolderConfig;
            }
            // Fallback: Unity LocalLow (older game versions)
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "..", "LocalLow", "The Fun Pimps", "7 Days To Die", "Mods", "AutoReadMod", "Config.xml");
        }

        private static ModConfig Load()
        {
            configPath = GetConfigPath();
            var config = new ModConfig();
            
            if (!File.Exists(configPath))
            {
                CreateDefault();
            }

            try
            {
                var doc = new XmlDocument();
                doc.Load(configPath);

                var enabledNode = doc.SelectSingleNode("//Enabled");
                if (enabledNode != null)
                    config.Enabled = bool.Parse(enabledNode.InnerText);

                var booksNode = doc.SelectSingleNode("//ItemTypes/Books");
                if (booksNode != null)
                    config.ReadBooks = bool.Parse(booksNode.InnerText);

                var magazinesNode = doc.SelectSingleNode("//ItemTypes/Magazines");
                if (magazinesNode != null)
                    config.ReadMagazines = bool.Parse(magazinesNode.InnerText);

                var schematicsNode = doc.SelectSingleNode("//ItemTypes/Schematics");
                if (schematicsNode != null)
                    config.ReadSchematics = bool.Parse(schematicsNode.InnerText);

                var debugNode = doc.SelectSingleNode("//DebugMode");
                if (debugNode != null)
                    config.DebugMode = bool.Parse(debugNode.InnerText);

                var experimentalNode = doc.SelectSingleNode("//ExperimentalApiTesting");
                if (experimentalNode != null)
                    config.ExperimentalApiTesting = bool.Parse(experimentalNode.InnerText);

                SafeLog($"AutoReadMod config loaded: Enabled={config.Enabled}");
            }
            catch (Exception)
            {
                // Use defaults on error
            }

            return config;
        }

        private static void CreateDefault()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                
                var defaultConfig = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<AutoReadModConfig>
  <Enabled>true</Enabled>
  <DebugMode>false</DebugMode>
  <ExperimentalApiTesting>false</ExperimentalApiTesting>
  <ItemTypes>
    <Books>true</Books>
    <Magazines>true</Magazines>
    <Schematics>true</Schematics>
  </ItemTypes>
</AutoReadModConfig>";
                
                File.WriteAllText(configPath, defaultConfig);
                SafeLog($"Created default AutoReadMod config at: {configPath}");
            }
            catch (Exception)
            {
                // Silently fail
            }
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

        public static void Reload()
        {
            instance = Load();
        }
    }
}
