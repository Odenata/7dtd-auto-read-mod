using System;
using GearsAPI.Settings;
using GearsAPI.Settings.Global;
using GearsAPI.Settings.World;
using UnityEngine;

namespace AutoReadMod
{
    /// <summary>
    /// Soft-dep Gears bridge. Discovered by Gears when present; ignored when Gears is not installed.
    /// World settings = enforcement (synced). Global = debug + visualization (per client).
    /// GearsAPI 3: values are typed. World settings have no OnValueChanged; re-read SettingValue
    /// before loot processing so a host edit is picked up on the next container.
    /// </summary>
    public class GearsModApi : IGearsModApi
    {
        private const string WorldCategory = "Behavior";
        private const string GlobalTab = "Client";
        private const string GlobalCategoryDebug = "Debug";
        private const string GlobalCategoryAudio = "Audio";
        private const string GlobalCategoryViz = "Visualization";

        private static IModWorldSettings worldSettings;
        private static bool globalListenersAttached;

        public void InitMod(IGearsMod modInstance)
        {
            SafeLog("GearsModApi.InitMod");
        }

        public void OnGlobalSettingsLoaded(IModGlobalSettings modSettings)
        {
            if (modSettings == null)
                return;

            try
            {
                ApplyGlobalFromGears(modSettings);
                if (!globalListenersAttached)
                {
                    AttachGlobalListeners(modSettings);
                    globalListenersAttached = true;
                }
                ModConfig.Instance.Save();
                SafeLog("Gears global settings applied");
            }
            catch (Exception ex)
            {
                SafeLog($"Gears OnGlobalSettingsLoaded failed: {ex.Message}");
            }
        }

        public void OnWorldSettingsLoaded(IModWorldSettings settings)
        {
            if (settings == null)
                return;

            try
            {
                worldSettings = settings;
                ApplyWorldFromGears(settings);
                ModConfig.Instance.Save();
                SafeLog("Gears world settings applied");
            }
            catch (Exception ex)
            {
                SafeLog($"Gears OnWorldSettingsLoaded failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-read world SettingValue into ModConfig. World settings have no OnValueChanged.
        /// </summary>
        public static void SyncWorldSettingsIfLoaded()
        {
            if (worldSettings == null)
                return;

            try
            {
                ApplyWorldFromGears(worldSettings);
            }
            catch (Exception ex)
            {
                SafeLog($"Gears SyncWorldSettingsIfLoaded failed: {ex.Message}");
            }
        }

        private static void ApplyWorldFromGears(IModWorldSettings settings)
        {
            var category = settings.GetCategory(WorldCategory);
            if (category == null)
                return;

            var config = ModConfig.Instance;
            config.ApplyWorldSettings(
                enabled: ReadWorldBool(category, "Enabled", config.Enabled),
                skipAlreadyRead: ReadWorldBool(category, "SkipAlreadyRead", config.SkipAlreadyRead),
                readBooks: ReadWorldBool(category, "Books", config.ReadBooks),
                readMagazines: ReadWorldBool(category, "Magazines", config.ReadMagazines),
                readSchematics: ReadWorldBool(category, "Schematics", config.ReadSchematics),
                readOnInventoryAdd: ReadWorldBool(category, "ReadOnInventoryAdd", config.ReadOnInventoryAdd));
        }

        private static void ApplyGlobalFromGears(IModGlobalSettings settings)
        {
            var tab = settings.GetTab(GlobalTab);
            if (tab == null)
                return;

            var config = ModConfig.Instance;
            var debugCat = tab.GetCategory(GlobalCategoryDebug);
            var audioCat = tab.GetCategory(GlobalCategoryAudio);
            var vizCat = tab.GetCategory(GlobalCategoryViz);

            bool debugMode = config.DebugMode;
            bool playReadSounds = config.PlayReadSounds;
            bool playBookSetCompleteSound = config.PlayBookSetCompleteSound;
            bool vizEnabled = config.VisualizationEnabled;
            bool showReadEffects = config.ShowReadEffects;
            int maxVisible = config.VisualizationMaxVisible;
            Color book = config.VisualizationColorBook;
            Color magazine = config.VisualizationColorMagazine;
            Color recipe = config.VisualizationColorRecipe;
            Color schematic = config.VisualizationColorSchematic;

            if (debugCat != null)
                debugMode = ReadGlobalBool(debugCat, "DebugMode", debugMode);

            if (audioCat != null)
            {
                playReadSounds = ReadGlobalBool(audioCat, "PlayReadSounds", playReadSounds);
                playBookSetCompleteSound = ReadGlobalBool(audioCat, "PlayBookSetCompleteSound", playBookSetCompleteSound);
            }

            if (vizCat != null)
            {
                vizEnabled = ReadGlobalBool(vizCat, "Enabled", vizEnabled);
                showReadEffects = ReadGlobalBool(vizCat, "ShowReadEffects", showReadEffects);
                maxVisible = ReadGlobalInt(vizCat, "MaxVisible", maxVisible);
                book = ReadGlobalColor(vizCat, "ColorBook", book);
                magazine = ReadGlobalColor(vizCat, "ColorMagazine", magazine);
                recipe = ReadGlobalColor(vizCat, "ColorRecipe", recipe);
                schematic = ReadGlobalColor(vizCat, "ColorSchematic", schematic);
            }

            config.ApplyGlobalSettings(
                debugMode,
                vizEnabled,
                maxVisible,
                book,
                magazine,
                recipe,
                schematic,
                playReadSounds,
                playBookSetCompleteSound,
                showReadEffects);
        }

        private static void AttachGlobalListeners(IModGlobalSettings settings)
        {
            var tab = settings.GetTab(GlobalTab);
            if (tab == null)
                return;

            HookBool(tab.GetCategory(GlobalCategoryDebug), "DebugMode", value => ModConfig.Instance.DebugMode = value);
            var audio = tab.GetCategory(GlobalCategoryAudio);
            HookBool(audio, "PlayReadSounds", value => ModConfig.Instance.PlayReadSounds = value);
            HookBool(audio, "PlayBookSetCompleteSound", value => ModConfig.Instance.PlayBookSetCompleteSound = value);

            var viz = tab.GetCategory(GlobalCategoryViz);
            HookBool(viz, "Enabled", value => ModConfig.Instance.VisualizationEnabled = value);
            HookBool(viz, "ShowReadEffects", value => ModConfig.Instance.ShowReadEffects = value);
            HookInt(viz, "MaxVisible", value => ModConfig.Instance.VisualizationMaxVisible = value);
            HookColor(viz, "ColorBook", value => ModConfig.Instance.VisualizationColorBook = value);
            HookColor(viz, "ColorMagazine", value => ModConfig.Instance.VisualizationColorMagazine = value);
            HookColor(viz, "ColorRecipe", value => ModConfig.Instance.VisualizationColorRecipe = value);
            HookColor(viz, "ColorSchematic", value => ModConfig.Instance.VisualizationColorSchematic = value);
        }

        private static void HookBool(IGlobalModSettingsCategory category, string name, Action<bool> apply)
        {
            var setting = category?.GetSetting<ISwitchGlobalSetting<bool>>(name);
            if (setting == null)
                return;
            setting.OnValueChanged += (_, value) => ApplyAndSave(() => apply(value));
        }

        private static void HookInt(IGlobalModSettingsCategory category, string name, Action<int> apply)
        {
            var setting = category?.GetSetting<ISliderGlobalSetting<int>>(name);
            if (setting == null)
                return;
            setting.OnValueChanged += (_, value) => ApplyAndSave(() => apply(value));
        }

        private static void HookColor(IGlobalModSettingsCategory category, string name, Action<Color> apply)
        {
            var setting = category?.GetSetting<IColorSelectorGlobalSetting>(name);
            if (setting == null)
                return;
            setting.OnValueChanged += (_, value) => ApplyAndSave(() => apply(value));
        }

        private static void ApplyAndSave(Action apply)
        {
            try
            {
                apply();
                ModConfig.Instance.Save();
            }
            catch (Exception ex)
            {
                SafeLog($"Gears OnValueChanged failed: {ex.Message}");
            }
        }

        private static bool ReadWorldBool(IWorldModSettingsCategory category, string name, bool defaultValue)
        {
            var setting = category.GetSetting<ISwitchWorldSetting<bool>>(name);
            if (setting == null)
                return defaultValue;
            return setting.SettingValue;
        }

        private static bool ReadGlobalBool(IGlobalModSettingsCategory category, string name, bool defaultValue)
        {
            var setting = category.GetSetting<ISwitchGlobalSetting<bool>>(name);
            if (setting == null)
                return defaultValue;
            return setting.SettingValue;
        }

        private static int ReadGlobalInt(IGlobalModSettingsCategory category, string name, int defaultValue)
        {
            var setting = category.GetSetting<ISliderGlobalSetting<int>>(name);
            if (setting == null)
                return defaultValue;
            return setting.SettingValue;
        }

        private static Color ReadGlobalColor(IGlobalModSettingsCategory category, string name, Color defaultColor)
        {
            var setting = category.GetSetting<IColorSelectorGlobalSetting>(name);
            if (setting == null)
                return defaultColor;
            return setting.SettingValue;
        }

        private static void SafeLog(string message)
        {
            try
            {
                UnityEngine.Debug.Log($"[AutoReadMod] {message}");
            }
            catch
            {
                // Unity not available (test environment)
            }
        }
    }
}
