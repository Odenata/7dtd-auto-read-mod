using Xunit;
using System;
using System.IO;
using System.Xml;
using UnityEngine;

namespace AutoReadMod.Tests
{
    public class ModConfigTests
    {
        [Fact]
        public void Instance_ReturnsNonNull()
        {
            var config = ModConfig.Instance;
            Assert.NotNull(config);
        }

        [Fact]
        public void DefaultConfig_EnabledIsTrue()
        {
            var config = ModConfig.Instance;
            Assert.True(config.Enabled);
        }

        [Fact]
        public void DefaultConfig_ReadBooksIsTrue()
        {
            var config = ModConfig.Instance;
            Assert.True(config.ReadBooks);
        }

        [Fact]
        public void DefaultConfig_ReadMagazinesIsTrue()
        {
            var config = ModConfig.Instance;
            Assert.True(config.ReadMagazines);
        }

        [Fact]
        public void DefaultConfig_ReadSchematicsIsTrue()
        {
            var config = ModConfig.Instance;
            Assert.True(config.ReadSchematics);
        }

        [Fact]
        public void DefaultConfig_DebugModeIsFalse()
        {
            var config = ModConfig.Instance;
            Assert.False(config.DebugMode);
        }

        [Fact]
        public void Reload_RefreshesInstance()
        {
            // Arrange
            var originalInstance = ModConfig.Instance;
            
            // Act
            ModConfig.Reload();
            var newInstance = ModConfig.Instance;
            
            // Assert
            Assert.NotNull(newInstance);
            // Note: Instances may be different objects after reload
        }

        [Fact]
        public void DefaultConfig_SkipAlreadyReadIsFalse()
        {
            var config = new ModConfig();
            Assert.False(config.SkipAlreadyRead);
        }

        [Fact]
        public void ModConfig_CanSetProperties()
        {
            // Arrange
            var config = new ModConfig();
            
            // Act
            config.Enabled = false;
            config.SkipAlreadyRead = true;
            config.ReadBooks = false;
            config.ReadMagazines = false;
            config.ReadSchematics = false;
            config.DebugMode = true;
            
            // Assert
            Assert.False(config.Enabled);
            Assert.True(config.SkipAlreadyRead);
            Assert.False(config.ReadBooks);
            Assert.False(config.ReadMagazines);
            Assert.False(config.ReadSchematics);
            Assert.True(config.DebugMode);
        }

        [Fact]
        public void ModConfig_DefaultConstructor_SetsDefaults()
        {
            // Arrange & Act
            var config = new ModConfig();
            
            // Assert
            Assert.True(config.Enabled);
            Assert.False(config.SkipAlreadyRead);
            Assert.True(config.ReadBooks);
            Assert.True(config.ReadMagazines);
            Assert.True(config.ReadSchematics);
            Assert.False(config.DebugMode);
        }

        [Fact]
        public void DefaultConfig_VisualizationEnabledIsTrue()
        {
            var config = new ModConfig();
            Assert.True(config.VisualizationEnabled);
        }

        [Fact]
        public void ModConfig_VisualizationPropertiesCanBeSet()
        {
            var config = new ModConfig();
            config.VisualizationEnabled = false;
            config.VisualizationMaxVisible = 4;

            Assert.False(config.VisualizationEnabled);
            Assert.Equal(4, config.VisualizationMaxVisible);
        }

        [Fact]
        public void DefaultConfig_VisualizationColorsHaveExpectedDefaults()
        {
            var config = new ModConfig();
            // Book: green (g dominant)
            Assert.True(config.VisualizationColorBook.g > config.VisualizationColorBook.r);
            Assert.True(config.VisualizationColorBook.g > config.VisualizationColorBook.b);
            Assert.Equal(1f, config.VisualizationColorBook.a);
            // Magazine: blue (b dominant)
            Assert.True(config.VisualizationColorMagazine.b > config.VisualizationColorMagazine.r);
            Assert.True(config.VisualizationColorMagazine.b > config.VisualizationColorMagazine.g);
            // Recipe: yellow (r and g high)
            Assert.True(config.VisualizationColorRecipe.r > 0.5f);
            Assert.True(config.VisualizationColorRecipe.g > 0.5f);
            Assert.True(config.VisualizationColorRecipe.b < 0.5f);
            // Schematic: magenta (r and b high)
            Assert.True(config.VisualizationColorSchematic.r > 0.5f);
            Assert.True(config.VisualizationColorSchematic.b > 0.5f);
        }

        [Fact]
        public void ModConfig_VisualizationColorPropertiesCanBeSet()
        {
            var config = new ModConfig();
            var green = new Color(0f, 1f, 0f, 1f);
            var blue = new Color(0f, 0f, 1f, 1f);
            config.VisualizationColorBook = green;
            config.VisualizationColorMagazine = blue;

            Assert.Equal(green.r, config.VisualizationColorBook.r);
            Assert.Equal(green.g, config.VisualizationColorBook.g);
            Assert.Equal(blue.b, config.VisualizationColorMagazine.b);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("True", true)]
        [InlineData("enabled", true)]
        [InlineData("Disabled", false)]
        [InlineData("1", true)]
        [InlineData("0", false)]
        public void ParseBoolSetting_MapsGearsStyleValues(string raw, bool expected)
        {
            Assert.Equal(expected, ModConfig.ParseBoolSetting(raw));
        }

        [Fact]
        public void ParseBoolSetting_EmptyUsesDefault()
        {
            Assert.True(ModConfig.ParseBoolSetting(null, true));
            Assert.False(ModConfig.ParseBoolSetting(" ", false));
        }

        [Fact]
        public void ApplyWorldSettings_UpdatesEnforcementKnobs()
        {
            var config = new ModConfig();
            config.ApplyWorldSettings(
                enabled: false,
                skipAlreadyRead: true,
                readBooks: false,
                readMagazines: true,
                readSchematics: false,
                readOnInventoryAdd: true);

            Assert.False(config.Enabled);
            Assert.True(config.SkipAlreadyRead);
            Assert.False(config.ReadBooks);
            Assert.True(config.ReadMagazines);
            Assert.False(config.ReadSchematics);
            Assert.True(config.ReadOnInventoryAdd);
            // Global knobs untouched
            Assert.False(config.DebugMode);
            Assert.True(config.VisualizationEnabled);
        }

        [Fact]
        public void DefaultConfig_ReadOnInventoryAddIsFalse()
        {
            var config = new ModConfig();
            Assert.False(config.ReadOnInventoryAdd);
        }

        [Fact]
        public void ApplyGlobalSettings_UpdatesClientKnobs()
        {
            var config = new ModConfig();
            var book = ModConfig.ColorFromRgb255(0, 204, 64);
            var mag = ModConfig.ColorFromRgb255(0, 102, 255);
            var recipe = ModConfig.ColorFromRgb255(230, 204, 0);
            var schematic = ModConfig.ColorFromRgb255(255, 0, 255);

            config.ApplyGlobalSettings(
                debugMode: true,
                visualizationEnabled: false,
                visualizationMaxVisible: 12,
                colorBook: book,
                colorMagazine: mag,
                colorRecipe: recipe,
                colorSchematic: schematic);

            Assert.True(config.DebugMode);
            Assert.False(config.VisualizationEnabled);
            Assert.Equal(12, config.VisualizationMaxVisible);
            Assert.Equal(ModConfig.ColorToHex(book), ModConfig.ColorToHex(config.VisualizationColorBook));
            Assert.Equal("00CC40", ModConfig.ColorToHex(config.VisualizationColorBook));
            Assert.Equal("0066FF", ModConfig.ColorToHex(config.VisualizationColorMagazine));
            // World knobs untouched
            Assert.True(config.Enabled);
            Assert.True(config.ReadBooks);
        }

        [Fact]
        public void ColorToHex_And_FromRgb255_RoundTripDefaults()
        {
            Assert.Equal("00CC40", ModConfig.ColorToHex(ModConfig.ColorFromRgb255(0, 204, 64)));
            Assert.Equal("FF00FF", ModConfig.ColorToHex(ModConfig.ColorFromRgb255(255, 0, 255)));
        }

        [Fact]
        public void ParseIntSetting_ParsesOrFallsBack()
        {
            Assert.Equal(8, ModConfig.ParseIntSetting("8", 1));
            Assert.Equal(3, ModConfig.ParseIntSetting("nope", 3));
        }

        [Fact]
        public void GetConfigPath_PrefersModApiPathDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AutoReadModContentRootTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                ModContentRoot.ClearForTests();
                ModContentRoot.SetFromModApiForTests(tempDir);

                var path = ModConfig.GetConfigPath();
                Assert.Equal(Path.Combine(Path.GetFullPath(tempDir), "Config.xml"), path);
            }
            finally
            {
                ModContentRoot.ClearForTests();
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ApplyFromModApi_CachesExistingDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AutoReadModContentRootTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                ModContentRoot.ClearForTests();
                ModContentRoot.ApplyFromModApi(new FakeModWithPath { Path = tempDir });

                Assert.Equal(Path.GetFullPath(tempDir), ModContentRoot.ResolveModDirectory());
                Assert.EndsWith("Config.xml", ModConfig.GetConfigPath());
                Assert.StartsWith(Path.GetFullPath(tempDir), ModConfig.GetConfigPath());
            }
            finally
            {
                ModContentRoot.ClearForTests();
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void GetConfigPath_DoesNotThrowWhenModPathUnset()
        {
            ModContentRoot.ClearForTests();
            // Assembly.Location is usually set under Bazel/MSBuild; when empty in-game,
            // ResolveModDirectory returns null and GetConfigPath uses LocalLow without throwing.
            var path = ModConfig.GetConfigPath();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith("Config.xml", path);
        }

        private sealed class FakeModWithPath
        {
            public string Path { get; set; }
        }
    }
}
