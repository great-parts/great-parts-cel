using System.IO;
using Great.Parts.Cel.Eng.Run;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The reading of the processor time of the host that the wait of eng/run uses (docs/design/eng-test.md, "Waiting for the host").
public sealed class HostLoadTests
{
    private const string Stat =
        "cpu  3291706 10 434853 17663495 7136 0 124622 5 7 3\n" +
        "cpu0 224704 0 78611 1449659 691 0 73624 0 0 0\n" +
        "cpu1 285422 0 30832 1468249 1335 0 15846 0 0 0\n" +
        "intr 123 4 5\n" +
        "ctxt 99\n";

    [Fact]
    public void Parse_TakesIdleAndIowaitAsNotBusy_AndCountsTheProcessors()
    {
        CpuTimes? times = CpuTimes.Parse(Stat);

        // Total: user + nice + system + idle + iowait + irq + softirq + steal; the guest columns are part of user and nice.
        long total = 3291706 + 10 + 434853 + 17663495 + 7136 + 0 + 124622 + 5;
        Assert.Equal(new CpuTimes(total - 17663495 - 7136, total, 2), times);
    }

    [Fact]
    public void Cores_IsTheBusyShareOfTheProcessors()
    {
        CpuTimes before = new(1000, 10_000, 4);
        CpuTimes after = new(1000 + 100, 10_000 + 400, 4);

        // A quarter of the time of four processors is one processor.
        Assert.Equal(1.0, CpuTimes.Cores(before, after), 6);
    }

    [Fact]
    public void Cores_WhenNoTimePassed_IsZero()
    {
        CpuTimes times = new(1000, 10_000, 4);

        Assert.Equal(0.0, CpuTimes.Cores(times, times));
    }

    [Theory]
    [InlineData("")]
    [InlineData("intr 1 2 3\n")]
    [InlineData("cpu  1 2 3\ncpu0 1 2 3\n")]
    [InlineData("cpu  a b c d e f g h\ncpu0 1 2 3 4 5 6 7 8\n")]
    [InlineData("cpu  1 2 3 4 5 6 7 8\n")]
    public static void Parse_OfAnotherForm_IsNull(string text) => Assert.Null(CpuTimes.Parse(text));

    [Fact]
    public void Read_OfAFileThatIsNotThere_IsNull()
    {
        using TempDirectory directory = new();

        Assert.Null(CpuTimes.Read(Path.Combine(directory.Path, "stat")));
    }

    [Fact]
    public void Read_OfAFile_IsItsParsedContent()
    {
        using TempDirectory directory = new();
        directory.Write("stat", Stat);

        Assert.Equal(CpuTimes.Parse(Stat), CpuTimes.Read(Path.Combine(directory.Path, "stat")));
    }
}
