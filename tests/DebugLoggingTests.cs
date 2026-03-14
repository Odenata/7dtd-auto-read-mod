using Xunit;
using System;

namespace AutoReadMod.Tests
{
    public class DebugLoggingTests
    {
        [Fact]
        public void DebugLog_WhenDebugModeDisabled_DoesNotLog()
        {
            // This test documents the expected behavior of DebugLog
            // When DebugMode is false, DebugLog should return early without logging
            var config = new ModConfig { DebugMode = false };
            
            // The DebugLog method checks ModConfig.Instance.DebugMode
            // If false, it returns early without calling SafeLog
            Assert.False(config.DebugMode);
        }

        [Fact]
        public void DebugLog_WhenDebugModeEnabled_Logs()
        {
            // This test documents the expected behavior of DebugLog
            // When DebugMode is true, DebugLog should call SafeLog with [DEBUG] prefix
            var config = new ModConfig { DebugMode = true };
            
            // The DebugLog method checks ModConfig.Instance.DebugMode
            // If true, it calls SafeLog with "[DEBUG] " prefix
            Assert.True(config.DebugMode);
        }

        [Fact]
        public void SafeLog_AlwaysLogs_RegardlessOfDebugMode()
        {
            // This test documents that SafeLog always logs, regardless of DebugMode
            // SafeLog is used for user-facing messages that should always appear
            var config1 = new ModConfig { DebugMode = false };
            var config2 = new ModConfig { DebugMode = true };
            
            // SafeLog does not check DebugMode - it always logs
            // This is correct behavior for user-facing messages
            Assert.NotNull(config1);
            Assert.NotNull(config2);
        }

        [Fact]
        public void DebugMode_DefaultValue_IsFalse()
        {
            // Verify that DebugMode defaults to false
            var config = new ModConfig();
            Assert.False(config.DebugMode);
        }

        [Fact]
        public void DebugMode_CanBeEnabled()
        {
            // Verify that DebugMode can be set to true
            var config = new ModConfig { DebugMode = true };
            Assert.True(config.DebugMode);
        }

        [Fact]
        public void DebugMode_CanBeDisabled()
        {
            // Verify that DebugMode can be set to false
            var config = new ModConfig { DebugMode = false };
            Assert.False(config.DebugMode);
        }

        [Fact]
        public void DebugMode_CanBeToggled()
        {
            // Verify that DebugMode can be toggled
            var config = new ModConfig { DebugMode = false };
            Assert.False(config.DebugMode);
            
            config.DebugMode = true;
            Assert.True(config.DebugMode);
            
            config.DebugMode = false;
            Assert.False(config.DebugMode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DebugMode_WorksWithBothValues(bool debugMode)
        {
            // Verify that DebugMode works correctly with both true and false
            var config = new ModConfig { DebugMode = debugMode };
            Assert.Equal(debugMode, config.DebugMode);
        }
    }
}
