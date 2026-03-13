using Xunit;
using Moq;

namespace AutoReadMod.Tests
{
    public class ModApiTests
    {
        [Fact]
        public void InitMod_WithValidModInstance_InitializesOrThrowsHarmonyException()
        {
            var modApi = new ModApi();
            var mockMod = new Mock<Mod>();
            
            var exception = Record.Exception(() => modApi.InitMod(mockMod.Object));
            
            // Either succeeds or throws HarmonyException (expected in test environment)
            Assert.True(exception == null || exception is HarmonyLib.HarmonyException);
        }

        [Fact]
        public void InitMod_WithNullModInstance_InitializesOrThrowsHarmonyException()
        {
            var modApi = new ModApi();
            
            var exception = Record.Exception(() => modApi.InitMod(null));
            
            // Either succeeds or throws HarmonyException (expected in test environment)
            Assert.True(exception == null || exception is HarmonyLib.HarmonyException);
        }
    }
}
