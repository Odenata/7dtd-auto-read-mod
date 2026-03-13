using Xunit;
using System.IO;

namespace GameApiExporter.Tests.Tools
{
    public class PackageToolTests
    {
        [Fact]
        public void PackageScript_Exists()
        {
            var scriptPath = Path.Combine("..", "..", "..", "..", "tools", "package.ps1");
            Assert.True(File.Exists(scriptPath), "package.ps1 should exist");
        }

        [Fact]
        public void PackageScript_ContainsRequiredCommands()
        {
            var scriptPath = Path.Combine("..", "..", "..", "..", "tools", "package.ps1");
            var content = File.ReadAllText(scriptPath);
            
            Assert.Contains("Compress-Archive", content);
            Assert.Contains("VERSION", content);
        }
    }
}
