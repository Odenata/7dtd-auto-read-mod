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
        public void ModConfig_CanSetProperties()
        {
            // Arrange
            var config = new ModConfig();
            
            // Act
            config.Enabled = false;
            config.ReadBooks = false;
            config.ReadMagazines = false;
            config.ReadSchematics = false;
            config.DebugMode = true;
            
            // Assert
            Assert.False(config.Enabled);
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
        public void DefaultConfig_VisualizationDurationSecondsIsPositive()
        {
            var config = new ModConfig();
            Assert.True(config.VisualizationDurationSeconds > 0);
        }

        [Fact]
        public void ModConfig_VisualizationPropertiesCanBeSet()
        {
            var config = new ModConfig();
            config.VisualizationEnabled = false;
            config.VisualizationDurationSeconds = 5f;
            config.VisualizationIconScale = 1.5f;
            config.VisualizationMaxVisible = 4;

            Assert.False(config.VisualizationEnabled);
            Assert.Equal(5f, config.VisualizationDurationSeconds);
            Assert.Equal(1.5f, config.VisualizationIconScale);
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
    }
}
