using Xunit;
using System;
using System.IO;
using System.Xml;

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
    }
}
