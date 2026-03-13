using System;
using System.IO;
using System.Xml;

namespace GameApiExporter
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;
        public bool ReadBooks { get; set; } = true;
        public bool ReadMagazines { get; set; } = true;
        public bool ReadSchematics { get; set; } = true;
        public bool DebugMode { get; set; } = false;
        public bool ExperimentalApiTesting { get; set; } = false;
        public bool RuntimeApiExportOnStartup { get; set; } = true;
        public string TargetAssemblies { get; set; } = "Assembly-CSharp,Assembly-CSharp-firstpass";
        public string ExportDirectoryName { get; set; } = "api-export";

        private static ModConfig instance;
        private static readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "..", "LocalLow", "The Fun Pimps", "7 Days To Die", "Mods", "GameApiExporter", "Config.xml");

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

        private static ModConfig Load()
        {
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

                var exportNode = doc.SelectSingleNode("//RuntimeApiExportOnStartup");
                if (exportNode != null)
                    config.RuntimeApiExportOnStartup = bool.Parse(exportNode.InnerText);

                var targetAssembliesNode = doc.SelectSingleNode("//TargetAssemblies");
                if (targetAssembliesNode != null)
                    config.TargetAssemblies = targetAssembliesNode.InnerText.Trim();

                var exportDirNode = doc.SelectSingleNode("//ExportDirectoryName");
                if (exportDirNode != null)
                    config.ExportDirectoryName = exportDirNode.InnerText.Trim();

                SafeLog($"Game API Exporter config loaded: Enabled={config.Enabled}");
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
<GameApiExporterConfig>
  <Enabled>true</Enabled>
  <DebugMode>false</DebugMode>
  <ExperimentalApiTesting>false</ExperimentalApiTesting>
  <RuntimeApiExportOnStartup>true</RuntimeApiExportOnStartup>
  <TargetAssemblies>Assembly-CSharp,Assembly-CSharp-firstpass</TargetAssemblies>
  <ExportDirectoryName>api-export</ExportDirectoryName>
  <ItemTypes>
    <Books>true</Books>
    <Magazines>true</Magazines>
    <Schematics>true</Schematics>
  </ItemTypes>
</GameApiExporterConfig>";
                
                File.WriteAllText(configPath, defaultConfig);
                SafeLog($"Created default Game API Exporter config at: {configPath}");
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
