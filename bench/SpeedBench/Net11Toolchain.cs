using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;

namespace SpeedBench;

/// <summary>
/// The toolchain of the rig. BenchmarkDotNet 0.15.8 does not know .NET 11: the runtime of the running process is a runtime
/// of a version it cannot name (RuntimeMoniker.NotRecognized), and the check of the installed SDK of its own toolchain for
/// the projects it generates throws NotImplementedException for such a runtime before any measurement. This toolchain builds and
/// runs the same generated project for net11.0 with the same generator, builder and executor, and without that check.
/// </summary>
internal static class Net11Toolchain
{
    private const string TargetFramework = "net11.0";

    // One instance: BenchmarkDotNet groups the benchmarks by toolchain, and Toolchain compares by reference.
    private static readonly IToolchain Instance = new Toolchain(
        ".NET 11.0",
        new CsProjGenerator(TargetFramework, null, null, null),
        new DotNetCliBuilder(TargetFramework),
        new DotNetCliExecutor(null));

    /// <summary>A job that only sets the toolchain: it applies to every job of the run, including the job named by --job.</summary>
    public static Job Mutator => Job.Default.WithToolchain(Instance).AsMutator();
}
