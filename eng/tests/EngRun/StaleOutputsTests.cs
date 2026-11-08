using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Run;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The deletion of the unfinished outputs of a Native AOT publish before the next publish (docs/design/eng-test.md, "Unfinished Native
// AOT outputs").
public sealed class StaleOutputsTests
{
    [Fact]
    public void Delete_RemovesTheDirectoriesWithTheirContent_AndSkipsThoseThatAreNotThere()
    {
        using TempDirectory directory = new();
        directory.Write("a/deep/file.o", "half");
        directory.Write("b/file", "x");
        directory.Write("kept/file", "x");

        string? reason = StaleOutputs.Delete([Path.Combine(directory.Path, "a"), Path.Combine(directory.Path, "missing"), Path.Combine(directory.Path, "b")]);

        Assert.Null(reason);
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "a")));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "b")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "kept/file")));
    }

    [Fact]
    public void PublishLane_StartsWithTheDeletionOfTheNativeDirectoryAndTheOutput_SoAHalfWrittenObjectFileIsNotTakenForAResult()
    {
        using TempDirectory directory = new();
        string project = Path.Combine(directory.Path, "tests/Stale.Probe").Replace('\\', '/');
        TestProject published = new("Stale.Probe", project, ["net10.0", "net11.0"], PublishAot: true);
        string native = $"{project}/obj/Release/net10.0/linux-x64/native";
        string otherTarget = $"{project}/obj/Release/net11.0/linux-x64/native";
        string intermediate = $"{project}/obj/Release/net10.0/linux-x64/Stale.Probe.dll";
        // What a publish that was killed in the middle leaves: an object file that is shorter than it should be.
        Directory.CreateDirectory(native);
        Directory.CreateDirectory(otherTarget);
        File.WriteAllText($"{native}/Stale.Probe.o", "half of an object file");
        File.WriteAllText($"{otherTarget}/Stale.Probe.o", "the other target");
        File.WriteAllText(intermediate, "compiled");

        // Stage 2 has the lanes of the JIT run, then one lane for each target; the first step of the lane of net10.0 is the deletion.
        Stage run = Commands.TestStages([published])[1];
        Step[] lane = [.. run.Lanes.Skip(1).First()];
        Assert.Equal(Commands.NativeDirectory(published, "net10.0"), native);
        Assert.NotNull(lane[0].Action);
        Assert.True(lane[0].StopsOnFailure);
        Assert.Equal("publish", lane[1].Arguments[0]);

        string? reason = lane[0].Action!();

        Assert.Null(reason);
        Assert.False(Directory.Exists(native), "the native directory of the target that is published is deleted");
        Assert.True(File.Exists($"{otherTarget}/Stale.Probe.o"), "the native directory of the other target is not touched by this lane");
        Assert.True(File.Exists(intermediate), "the compiled assembly is kept: only the output of the native compiler is not trusted");
    }
}
