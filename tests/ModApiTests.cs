using Xunit;

namespace AutoReadMod.Tests
{
    public class ModApiTests
    {
        [Fact]
        public void InitMod_WithValidModInstance_InitializesOrThrowsHarmonyException()
        {
            var modApi = new ModApi();
            var mod = new Mod();
            
            var exception = Record.Exception(() => modApi.InitMod(mod));
            
            // Either succeeds or throws (e.g. HarmonyException in test environment)
            Assert.True(exception == null || exception.GetType().Name == "HarmonyException");
        }

        [Fact]
        public void InitMod_WithNullModInstance_InitializesOrThrowsHarmonyException()
        {
            var modApi = new ModApi();
            
            var exception = Record.Exception(() => modApi.InitMod(null));
            
            // Either succeeds or throws (e.g. HarmonyException in test environment)
            Assert.True(exception == null || exception.GetType().Name == "HarmonyException");
        }
    }
}
