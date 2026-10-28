using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Compares the tests that ran under JIT with the tests that ran under Native AOT for the same commit, operating system and target
/// (<c>docs/design/eng-checks-artifacts.md</c>, "test-count"). The input is two xUnit.net XML reports, <c>--jit</c> and <c>--aot</c>,
/// and the text <c>--where</c> for the messages. A test only in the JIT report is <c>ENG810</c>, a test only in the Native AOT report
/// is <c>ENG811</c>.
/// </summary>
internal sealed class TestCountCommand : ICheckCommand
{
    /// <summary>The option of the command line: the report of the JIT run.</summary>
    internal const string JitOption = "--jit";

    /// <summary>The option of the command line: the report of the Native AOT run.</summary>
    internal const string AotOption = "--aot";

    /// <summary>The option of the command line: the text for the messages, such as <c>linux-x64, net11.0</c>.</summary>
    internal const string WhereOption = "--where";

    /// <inheritdoc/>
    public string Name => "test-count";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        string jitPath = Required(context, JitOption);
        string aotPath = Required(context, AotOption);
        string where = Required(context, WhereOption).ReplaceLineEndings(" ");

        TestReport jit = TestReport.Read(jitPath);
        TestReport aot = TestReport.Read(aotPath);
        TestPlaces places = new(context.Root, jit);
        List<Finding> findings = [];
        foreach (ReportedTest test in Only(jit, aot))
        {
            findings.Add(places.FindingAt(test, jit, EngCodes.Eng810, Message(test, "JIT", "Native AOT", where)));
        }

        foreach (ReportedTest test in Only(aot, jit))
        {
            findings.Add(places.FindingAt(test, aot, EngCodes.Eng811, Message(test, "Native AOT", "JIT", where)));
        }

        return findings;
    }

    private static string Required(CheckContext context, string option) =>
        context.Options.TryGetValue(option, out string? value)
            ? value
            : throw new CheckInputException($"the command test-count needs the option {option}");

    // The tests of the report that the other report does not have, in the order of the report.
    private static IEnumerable<ReportedTest> Only(TestReport report, TestReport other) =>
        report.Tests.Where(pair => !other.Tests.ContainsKey(pair.Key)).Select(pair => pair.Value);

    // One line: the name of a test is written by the test framework and may have a line break.
    private static string Message(ReportedTest test, string ran, string missing, string where) =>
        $"test '{test.Name.ReplaceLineEndings(" ")}' ran under {ran} but not under {missing} ({where})";
}
