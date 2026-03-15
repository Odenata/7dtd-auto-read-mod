using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace AutoReadMod
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;
        public bool ReadBooks { get; set; } = true;
        public bool ReadMagazines { get; set; } = true;
        public bool ReadSchematics { get; set; } = true;
        public bool DebugMode { get; set; } = false;

        // Visualization (splash when items are auto-read)
        public bool VisualizationEnabled { get; set; } = true;
        public float VisualizationDurationSeconds { get; set; } = 6f;
        public float VisualizationIconScale { get; set; } = 1f;
        public int VisualizationMaxVisible { get; set; } = 8;
        public Color VisualizationColorBook { get; set; } = new Color(0.2f, 0.9f, 0.3f, 1f);
        public Color VisualizationColorMagazine { get; set; } = new Color(0f, 0.4f, 1f, 1f);
        public Color VisualizationColorRecipe { get; set; } = new Color(0.9f, 0.8f, 0f, 1f);
        public Color VisualizationColorSchematic { get; set; } = new Color(0.95f, 0.2f, 0.9f, 1f);

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

                var visEnabledNode = doc.SelectSingleNode("//Visualization/Enabled");
                if (visEnabledNode != null)
                    config.VisualizationEnabled = bool.Parse(visEnabledNode.InnerText);

                var visDurationNode = doc.SelectSingleNode("//Visualization/DurationSeconds");
                if (visDurationNode != null && float.TryParse(visDurationNode.InnerText, out float duration))
                    config.VisualizationDurationSeconds = duration;

                var visScaleNode = doc.SelectSingleNode("//Visualization/IconScale");
                if (visScaleNode != null && float.TryParse(visScaleNode.InnerText, out float scale))
                    config.VisualizationIconScale = scale;

                var visMaxNode = doc.SelectSingleNode("//Visualization/MaxVisible");
                if (visMaxNode != null && int.TryParse(visMaxNode.InnerText, out int maxVisible))
                    config.VisualizationMaxVisible = maxVisible;

                var colorBookNode = doc.SelectSingleNode("//Visualization/ColorBook");
                if (colorBookNode != null)
                    config.VisualizationColorBook = TryParseHexColor(colorBookNode.InnerText?.Trim(), config.VisualizationColorBook);
                var colorMagazineNode = doc.SelectSingleNode("//Visualization/ColorMagazine");
                if (colorMagazineNode != null)
                    config.VisualizationColorMagazine = TryParseHexColor(colorMagazineNode.InnerText?.Trim(), config.VisualizationColorMagazine);
                var colorRecipeNode = doc.SelectSingleNode("//Visualization/ColorRecipe");
                if (colorRecipeNode != null)
                    config.VisualizationColorRecipe = TryParseHexColor(colorRecipeNode.InnerText?.Trim(), config.VisualizationColorRecipe);
                var colorSchematicNode = doc.SelectSingleNode("//Visualization/ColorSchematic");
                if (colorSchematicNode != null)
                    config.VisualizationColorSchematic = TryParseHexColor(colorSchematicNode.InnerText?.Trim(), config.VisualizationColorSchematic);

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
  <ItemTypes>
    <Books>true</Books>
    <Magazines>true</Magazines>
    <Schematics>true</Schematics>
  </ItemTypes>
  <Visualization>
    <Enabled>true</Enabled>
    <DurationSeconds>6</DurationSeconds>
    <IconScale>1</IconScale>
    <MaxVisible>8</MaxVisible>
    <ColorBook>00CC40</ColorBook>
    <ColorMagazine>0066FF</ColorMagazine>
    <ColorRecipe>E6CC00</ColorRecipe>
    <ColorSchematic>FF00FF</ColorSchematic>
  </Visualization>
</AutoReadModConfig>";
                
                File.WriteAllText(configPath, defaultConfig);
                SafeLog($"Created default AutoReadMod config at: {configPath}");
            }
            catch (Exception)
            {
                // Silently fail
            }
        }

        private static Color TryParseHexColor(string hex, Color defaultColor)
        {
            if (string.IsNullOrEmpty(hex)) return defaultColor;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return defaultColor;
            try
            {
                int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }
            catch { return defaultColor; }
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
