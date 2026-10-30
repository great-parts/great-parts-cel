using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// A cell of the test matrix: the mode, the operating system and the target of a run. A skip entry applies to a run when its
/// scope contains the cell of the run.
/// </summary>
/// <param name="Mode"><c>jit</c> or <c>native-aot</c>.</param>
/// <param name="OperatingSystem"><c>linux-x64</c> or <c>macos</c>.</param>
/// <param name="Target"><c>net10.0</c> or <c>net11.0</c>.</param>
internal readonly record struct RunCell(string Mode, string OperatingSystem, string Target)
{
    private static readonly string[] Modes = ["jit", "native-aot"];
    private static readonly string[] OperatingSystems = ["linux-x64", "macos"];
    private static readonly string[] Targets = ["net10.0", "net11.0"];

    /// <summary>The cells of the matrix: JIT on <c>linux-x64</c> and <c>macos</c>, Native AOT on <c>linux-x64</c>; both targets.</summary>
    internal static IReadOnlyList<RunCell> Matrix { get; } = [.. Combine(Modes, OperatingSystems, Targets).Where(cell => cell.Mode == "jit" || cell.OperatingSystem != "macos")];

    /// <summary>The cell of this run: the mode from <see cref="RuntimeFeature.IsDynamicCodeSupported"/>, the operating system of the process and the target of the build.</summary>
    internal static RunCell Current { get; } = Of(RuntimeFeature.IsDynamicCodeSupported, CurrentPlatform(), RuntimeInformation.ProcessArchitecture, BuildTarget);

    /// <summary>The target framework this assembly is built for.</summary>
#if NET11_0_OR_GREATER
    internal const string BuildTarget = "net11.0";
#else
    internal const string BuildTarget = "net10.0";
#endif

    /// <summary>The cell of a run with these properties.</summary>
    /// <param name="isDynamicCodeSupported">Whether the runtime compiles code at run time: <c>jit</c> when it does, else <c>native-aot</c>.</param>
    /// <param name="platform">The operating system of the process.</param>
    /// <param name="architecture">The architecture of the process.</param>
    /// <param name="target">The target framework of the build.</param>
    /// <exception cref="PlatformNotSupportedException">The operating system and the architecture are none of the matrix.</exception>
    internal static RunCell Of(bool isDynamicCodeSupported, OSPlatform platform, Architecture architecture, string target)
    {
        string operatingSystem =
            platform == OSPlatform.Linux && architecture == Architecture.X64 ? "linux-x64"
            : platform == OSPlatform.OSX ? "macos"
            : throw new PlatformNotSupportedException($"The suites run on linux-x64 and macos, not on {platform} {architecture}.");
        return new RunCell(isDynamicCodeSupported ? "jit" : "native-aot", operatingSystem, target);
    }

    /// <summary>
    /// Reads the scope of a skip entry: three parts separated by one space — the mode, the operating system and the target. A part
    /// is one value, a list of values separated by commas without spaces, or <c>*</c> for any value. The scope is every
    /// combination of the parts that the <see cref="Matrix"/> has.
    /// </summary>
    /// <param name="text">The text of the scope.</param>
    /// <param name="cells">The cells of the scope, or an empty set when the text is not valid.</param>
    /// <param name="problem">What is wrong with the text, or <see langword="null"/> when it is valid.</param>
    /// <returns><see langword="true"/> when the scope is valid and has at least one cell of the matrix.</returns>
    internal static bool TryParseScope(string text, out IReadOnlySet<RunCell> cells, out string? problem)
    {
        cells = new HashSet<RunCell>();
        string[] parts = text.Split(' ');
        if (parts.Length != 3)
        {
            problem = $"the scope '{text}' is not three parts separated by a space: the mode, the operating system and the target";
            return false;
        }

        problem = UnknownValue(parts[0], Modes) ?? UnknownValue(parts[1], OperatingSystems) ?? UnknownValue(parts[2], Targets);
        if (problem is not null)
        {
            return false;
        }

        HashSet<RunCell> combinations = [.. Combine(Values(parts[0], Modes), Values(parts[1], OperatingSystems), Values(parts[2], Targets))];
        combinations.IntersectWith(Matrix);
        if (combinations.Count == 0)
        {
            problem = $"the scope '{text}' has no cell of the matrix";
            return false;
        }

        cells = combinations;
        return true;
    }

    /// <summary>The cell as the findings and the summary line name it: <c>jit, linux-x64, net11.0</c>.</summary>
    public override string ToString() => $"{Mode}, {OperatingSystem}, {Target}";

    private static OSPlatform CurrentPlatform() =>
        System.OperatingSystem.IsLinux() ? OSPlatform.Linux
        : System.OperatingSystem.IsMacOS() ? OSPlatform.OSX
        : OSPlatform.Create(RuntimeInformation.OSDescription);

    private static string? UnknownValue(string part, string[] known)
    {
        if (part == "*")
        {
            return null;
        }

        string? unknown = part.Split(',').FirstOrDefault(value => !known.Contains(value));
        return unknown is null ? null : $"the scope has the unknown value '{unknown}'; a part is {string.Join(", ", known)}, a list of them or *";
    }

    private static string[] Values(string part, string[] known) => part == "*" ? known : part.Split(',');

    private static IEnumerable<RunCell> Combine(string[] modes, string[] operatingSystems, string[] targets) =>
        from mode in modes
        from operatingSystem in operatingSystems
        from target in targets
        select new RunCell(mode, operatingSystem, target);
}
