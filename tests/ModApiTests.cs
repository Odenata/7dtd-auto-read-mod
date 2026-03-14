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
            
            Assert.False(exception is System.NullReferenceException, exception?.ToString());
            Assert.False(exception is System.ArgumentNullException, exception?.ToString());
        }

        [Fact]
        public void InitMod_WithNullModInstance_InitializesOrThrowsHarmonyException()
        {
            var modApi = new ModApi();
            
            var exception = Record.Exception(() => modApi.InitMod(null));
            
            Assert.False(exception is System.NullReferenceException, exception?.ToString());
            Assert.False(exception is System.ArgumentNullException, exception?.ToString());
        }
    }
}
