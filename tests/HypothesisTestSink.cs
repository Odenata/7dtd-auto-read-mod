using System;
using System.Runtime.CompilerServices;
using AutoReadMod;

namespace AutoReadMod.Tests
{
    /// <summary>
    /// Routes <see cref="GameReflection.DiagnosticLine"/> and <see cref="GameReflection.EmitHypothesisLine"/>
    /// to the test host console so <c>dotnet test</c> captures hypothesis output.
    /// </summary>
    internal static class HypothesisTestSink
    {
        [ModuleInitializer]
        internal static void EnableConsoleHypothesis()
        {
            GameReflection.DiagnosticLine += msg => Console.WriteLine(msg);
        }
    }
}
