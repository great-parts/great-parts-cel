using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The cell of a run and the scope of a skip entry (docs/design/conformance.md, "Skip lists and findings").
public sealed class RunCellTests
{
    private const string AllCells =
        "jit linux-x64 net10.0, jit linux-x64 net11.0, jit macos net10.0, jit macos net11.0, "
        + "native-aot linux-x64 net10.0, native-aot linux-x64 net11.0";

    [Fact]
    public void Matrix_IsJitOnLinuxAndMacosAndNativeAotOnLinux_ForBothTargets()
    {
        Assert.Equal(AllCells, Describe(RunCell.Matrix));
    }

    [Theory]
    [InlineData(true, "LINUX", Architecture.X64, "net10.0", "jit linux-x64 net10.0")]
    [InlineData(false, "LINUX", Architecture.X64, "net11.0", "native-aot linux-x64 net11.0")]
    [InlineData(true, "OSX", Architecture.Arm64, "net10.0", "jit macos net10.0")]
    [InlineData(true, "OSX", Architecture.X64, "net11.0", "jit macos net11.0")]
    public static void Of_IsTheModeTheSystemAndTheTarget(bool isDynamicCodeSupported, string platform, Architecture architecture, string target, string cell)
    {
        Assert.Equal(cell, Describe([RunCell.Of(isDynamicCodeSupported, OSPlatform.Create(platform), architecture, target)]));
    }

    [Theory]
    [InlineData("LINUX", Architecture.Arm64)]
    [InlineData("FREEBSD", Architecture.X64)]
    public static void Of_SystemOutsideTheMatrix_Throws(string platform, Architecture architecture)
    {
        Assert.Throws<PlatformNotSupportedException>(() => RunCell.Of(true, OSPlatform.Create(platform), architecture, "net10.0"));
    }

    [Fact]
    public void Current_IsACellOfTheMatrixWithTheModeOfTheRuntimeAndTheTargetOfTheRuntime()
    {
        RunCell cell = RunCell.Current;

        Assert.Contains(cell, RunCell.Matrix);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported ? "jit" : "native-aot", cell.Mode);
        Assert.Equal($"net{Environment.Version.Major}.0", cell.Target);
    }

    [Theory]
    [InlineData("* * *", AllCells)]
    [InlineData("jit * *", "jit linux-x64 net10.0, jit linux-x64 net11.0, jit macos net10.0, jit macos net11.0")]
    [InlineData("native-aot * *", "native-aot linux-x64 net10.0, native-aot linux-x64 net11.0")]
    [InlineData("* macos *", "jit macos net10.0, jit macos net11.0")]
    [InlineData("native-aot linux-x64,macos *", "native-aot linux-x64 net10.0, native-aot linux-x64 net11.0")]
    [InlineData("jit,native-aot macos,linux-x64 net11.0", "jit linux-x64 net11.0, jit macos net11.0, native-aot linux-x64 net11.0")]
    [InlineData("* * net10.0", "jit linux-x64 net10.0, jit macos net10.0, native-aot linux-x64 net10.0")]
    [InlineData("jit macos net10.0", "jit macos net10.0")]
    public static void Scope_IsEveryCombinationOfItsPartsThatTheMatrixHas(string scope, string cells)
    {
        ArgumentNullException.ThrowIfNull(scope);

        Assert.True(RunCell.TryParseScope(scope, out IReadOnlySet<RunCell> parsed, out string? problem));
        Assert.Null(problem);
        Assert.Equal(cells, Describe(parsed));
    }

    private static string Describe(IEnumerable<RunCell> cells) =>
        string.Join(", ", cells.Select(cell => $"{cell.Mode} {cell.OperatingSystem} {cell.Target}").Order(StringComparer.Ordinal));
}
