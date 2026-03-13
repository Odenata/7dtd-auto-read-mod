using Xunit;
using System.IO;

namespace AutoReadMod.Tests.Tools
{
    public class DeployToolTests
    {
        [Fact]
        public void DeployScript_Exists()
        {
            var scriptPath = Path.Combine("..", "..", "..", "..", "tools", "deploy.ps1");
            Assert.True(File.Exists(scriptPath), "deploy.ps1 should exist");
        }

        [Fact]
        public void DeployScript_ContainsRequiredCommands()
        {
            var scriptPath = Path.Combine("..", "..", "..", "..", "tools", "deploy.ps1");
            var content = File.ReadAllText(scriptPath);
            
            Assert.Contains("Copy-Item", content);
            Assert.Contains("Mods", content);
        }
    }
}
