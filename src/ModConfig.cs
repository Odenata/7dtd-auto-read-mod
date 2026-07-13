using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace AutoReadMod
{
    public class ModConfig
    {
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// When true, stop consuming units that would grant no progression/recipe benefit
        /// (maxed magazines, already-unlocked books/perks, known schematics). Partial stacks leave leftovers.
        /// Default false preserves always-consume-from-loot behavior.
        /// </summary>
        public bool SkipAlreadyRead { get; set; } = false;
        public bool ReadBooks { get; set; } = true;
        public bool ReadMagazines { get; set; } = true;
        public bool ReadSchematics { get; set; } = true;
        public bool DebugMode { get; set; } = false;

        // Visualization (splash when items are auto-read)
        public bool VisualizationEnabled { get; set; } = true;
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

        /// <summary>
        /// World-synced enforcement knobs (Gears World settings when present).
        /// </summary>
        public void ApplyWorldSettings(
            bool enabled,
            bool skipAlreadyRead,
            bool readBooks,
            bool readMagazines,
            bool readSchematics)
        {
            Enabled = enabled;
            SkipAlreadyRead = skipAlreadyRead;
            ReadBooks = readBooks;
            ReadMagazines = readMagazines;
            ReadSchematics = readSchematics;
        }

        /// <summary>
        /// Per-client Global knobs (debug + visualization).
        /// </summary>
        public void ApplyGlobalSettings(
            bool debugMode,
            bool visualizationEnabled,
            int visualizationMaxVisible,
            Color colorBook,
            Color colorMagazine,
            Color colorRecipe,
            Color colorSchematic)
        {
            DebugMode = debugMode;
            VisualizationEnabled = visualizationEnabled;
            if (visualizationMaxVisible > 0)
                VisualizationMaxVisible = visualizationMaxVisible;
            VisualizationColorBook = colorBook;
            VisualizationColorMagazine = colorMagazine;
            VisualizationColorRecipe = colorRecipe;
            VisualizationColorSchematic = colorSchematic;
        }

        /// <summary>
        /// Persist current Instance to Config.xml (best-effort mirror for non-Gears / uninstall Gears).
        /// </summary>
        public void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(configPath))
                    configPath = GetConfigPath();

                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
                sb.AppendLine("<AutoReadModConfig>");
                sb.AppendLine("  <!-- Turn the whole mod on or off without uninstalling. Values: true, false -->");
                sb.AppendLine($"  <Enabled>{BoolXml(Enabled)}</Enabled>");
                sb.AppendLine("  <!-- When true, leave maxed magazines / already-read books / known schematics in loot (partial stacks OK). Values: true, false -->");
                sb.AppendLine($"  <SkipAlreadyRead>{BoolXml(SkipAlreadyRead)}</SkipAlreadyRead>");
                sb.AppendLine("  <!-- If true, writes extra info to the game log (for troubleshooting). Values: true, false -->");
                sb.AppendLine($"  <DebugMode>{BoolXml(DebugMode)}</DebugMode>");
                sb.AppendLine("  <!-- Which item types to auto-read when you open loot. Set to false to skip that type. Values: true, false -->");
                sb.AppendLine("  <ItemTypes>");
                sb.AppendLine($"    <Books>{BoolXml(ReadBooks)}</Books>");
                sb.AppendLine($"    <Magazines>{BoolXml(ReadMagazines)}</Magazines>");
                sb.AppendLine($"    <Schematics>{BoolXml(ReadSchematics)}</Schematics>");
                sb.AppendLine("  </ItemTypes>");
                sb.AppendLine("  <!-- On-screen notifications when items are auto-read (same area as loot pickups) -->");
                sb.AppendLine("  <Visualization>");
                sb.AppendLine("    <!-- Show or hide the notifications. Values: true, false -->");
                sb.AppendLine($"    <Enabled>{BoolXml(VisualizationEnabled)}</Enabled>");
                sb.AppendLine("    <!-- Maximum number of notifications shown at once (older ones drop off). Values: positive number, e.g. 8 -->");
                sb.AppendLine($"    <MaxVisible>{VisualizationMaxVisible}</MaxVisible>");
                sb.AppendLine("    <!-- Notification text color per item type. Six hex digits, no # (e.g. 00CC40 = green). -->");
                sb.AppendLine($"    <ColorBook>{ColorToHex(VisualizationColorBook)}</ColorBook>");
                sb.AppendLine($"    <ColorMagazine>{ColorToHex(VisualizationColorMagazine)}</ColorMagazine>");
                sb.AppendLine($"    <ColorRecipe>{ColorToHex(VisualizationColorRecipe)}</ColorRecipe>");
                sb.AppendLine($"    <ColorSchematic>{ColorToHex(VisualizationColorSchematic)}</ColorSchematic>");
                sb.AppendLine("  </Visualization>");
                sb.AppendLine("</AutoReadModConfig>");

                File.WriteAllText(configPath, sb.ToString());
                SafeLog($"AutoReadMod config saved: Enabled={Enabled}, SkipAlreadyRead={SkipAlreadyRead}");
            }
            catch (Exception ex)
            {
                SafeLog($"AutoReadMod config save failed: {ex.Message}");
            }
        }

        /// <summary>Parse Gears Switch / string values into bool (true/false, enabled/disabled, etc.).</summary>
        public static bool ParseBoolSetting(string value, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            var v = value.Trim();
            if (bool.TryParse(v, out var parsed))
                return parsed;

            switch (v.ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "on":
                case "enable":
                case "enabled":
                    return true;
                case "0":
                case "no":
                case "off":
                case "disable":
                case "disabled":
                    return false;
                default:
                    return defaultValue;
            }
        }

        public static int ParseIntSetting(string value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : defaultValue;
        }

        public static string ColorToHex(Color color)
        {
            int r = ClampByte(color.r * 255f);
            int g = ClampByte(color.g * 255f);
            int b = ClampByte(color.b * 255f);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        public static Color ColorFromRgb255(int r, int g, int b)
        {
            return new Color(
                Clamp01(r / 255f),
                Clamp01(g / 255f),
                Clamp01(b / 255f),
                1f);
        }

        private static int ClampByte(float value)
        {
            var rounded = (int)Math.Round(value);
            if (rounded < 0) return 0;
            if (rounded > 255) return 255;
            return rounded;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static string BoolXml(bool value) => value ? "true" : "false";

        private static string GetConfigPath()
        {
            // Prefer mod folder next to DLL (game install Mods/AutoReadMod or 7DaysToDie/Mods/AutoReadMod)
            var assemblyDir = Path.GetDirectoryName(typeof(ModConfig).Assembly.Location);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                var modFolderConfig = Path.Combine(assemblyDir, "Config.xml");
                if (File.Exists(modFolderConfig))
                    return modFolderConfig;
                // Prefer writing next to the DLL even if the file does not exist yet
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

                var skipAlreadyReadNode = doc.SelectSingleNode("//SkipAlreadyRead");
                if (skipAlreadyReadNode != null)
                    config.SkipAlreadyRead = bool.Parse(skipAlreadyReadNode.InnerText);

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

                SafeLog($"AutoReadMod config loaded: Enabled={config.Enabled}, SkipAlreadyRead={config.SkipAlreadyRead}");
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
  <!-- Turn the whole mod on or off without uninstalling. Values: true, false -->
  <Enabled>true</Enabled>
  <!-- When true, leave maxed magazines / already-read books / known schematics in loot (partial stacks OK). Values: true, false -->
  <SkipAlreadyRead>false</SkipAlreadyRead>
  <!-- If true, writes extra info to the game log (for troubleshooting). Values: true, false -->
  <DebugMode>false</DebugMode>
  <!-- Which item types to auto-read when you open loot. Set to false to skip that type. Values: true, false -->
  <ItemTypes>
    <Books>true</Books>
    <Magazines>true</Magazines>
    <Schematics>true</Schematics>
  </ItemTypes>
  <!-- On-screen notifications when items are auto-read (same area as loot pickups) -->
  <Visualization>
    <!-- Show or hide the notifications. Values: true, false -->
    <Enabled>true</Enabled>
    <!-- Maximum number of notifications shown at once (older ones drop off). Values: positive number, e.g. 8 -->
    <MaxVisible>8</MaxVisible>
    <!-- Notification text color per item type. Six hex digits, no # (e.g. 00CC40 = green). -->
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

        internal static Color TryParseHexColor(string hex, Color defaultColor)
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
