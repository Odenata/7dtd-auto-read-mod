using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;

namespace AutoReadMod.Tests.Integration
{
    public class GameLaunchTests
    {
        private const int TimeoutSeconds = 60;
        private readonly string gamePath = @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die";
        private readonly string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "..", "LocalLow", "The Fun Pimps", "7 Days To Die", "output_log.txt");

        [Fact(Skip = "Integration test - run manually; launches game and checks output_log.txt for 'AutoReadMod loaded successfully'")]
        public void GameLaunches_ModLoadsSuccessfully()
        {
            var gameExe = Path.Combine(gamePath, "7DaysToDie.exe");
            
            Assert.True(File.Exists(gameExe), $"Game not found at {gameExe}");

            // Backup and clear old log
            if (File.Exists(logPath))
            {
                File.Copy(logPath, $"{logPath}.backup", true);
                File.Delete(logPath);
            }

            Process gameProcess = null;
            try
            {
                // Launch game
                gameProcess = Process.Start(gameExe);
                Assert.NotNull(gameProcess);

                // Wait for log file
                var waited = 0;
                while (!File.Exists(logPath) && waited < TimeoutSeconds)
                {
                    Thread.Sleep(1000);
                    waited++;
                }

                Assert.True(File.Exists(logPath), "Log file not created");

                // Monitor log for success message
                var startTime = DateTime.Now;
                var found = false;

                while ((DateTime.Now - startTime).TotalSeconds < TimeoutSeconds)
                {
                    if (File.Exists(logPath))
                    {
                        var content = File.ReadAllText(logPath);
                        if (content.Contains("AutoReadMod loaded successfully"))
                        {
                            found = true;
                            break;
                        }
                    }
                    Thread.Sleep(500);
                }

                Assert.True(found, "AutoReadMod did not load successfully within timeout (check output_log.txt for [AutoReadMod] messages)");
            }
            finally
            {
                // Clean up
                if (gameProcess != null && !gameProcess.HasExited)
                {
                    gameProcess.Kill();
                    gameProcess.WaitForExit(5000);
                }
            }
        }
    }
}
