using Xunit;
using System.IO;

namespace AutoReadMod.Tests.Tools
{
    public class BuildToolTests
    {
        [Fact]
        public void BuildScript_Exists()
        {
            var scriptPath = Path.Combine("..", "..", "..", "..", "tools", "build.ps1");
            Assert.True(File.Exists(scriptPath), "build.ps1 should exist");
        }

        [Fact]
        public void BuildScript_ContainsRequiredCommands()
        {
            var scriptPath = Path.Combine("..", "..", "..", "..", "tools", "build.ps1");
            var content = File.ReadAllText(scriptPath);
            
            Assert.Contains("dotnet build", content);
            Assert.Contains("Assembly", content);
        }
    }
}
