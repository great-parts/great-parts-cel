using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cel.Expr;
using Cel.Expr.Conformance.Test;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>Reads the cases of a <c>SimpleTestFile</c> of the cel-spec format into the common model of a case.</summary>
internal static class CaseReader
{
    /// <summary>A case without a result matcher expects the value <c>bool true</c>, as in the cel-go runner.</summary>
    private static readonly Expectation DefaultExpectation = new(ExpectationKind.Value, "none", new Value { BoolValue = true }, null);

    /// <summary>Reads the cases of an embedded text file of a suite from its binary form, with the positions from its text.</summary>
    /// <param name="path">The path of the text file relative to the repository root.</param>
    internal static IReadOnlyList<ConformanceCase> ReadEmbedded(string path) =>
        Read(path, SimpleTestFile.Parser.ParseFrom(EmbeddedData.ReadBinary(path)), EmbeddedData.ReadText(path));

    /// <summary>Reads the cases of a file.</summary>
    /// <param name="path">The path of the text file relative to the repository root.</param>
    /// <param name="file">The file, parsed from its binary form.</param>
    /// <param name="text">The text of the file.</param>
    /// <exception cref="InvalidDataException">The scan of the text finds other sections or cases than the file has.</exception>
    internal static IReadOnlyList<ConformanceCase> Read(string path, SimpleTestFile file, string text)
    {
        IReadOnlyList<IReadOnlyList<TextPosition>> positions = CasePositions.Read(text);
        if (!positions.Select(section => section.Count).SequenceEqual(file.Section.Select(section => section.Test.Count)))
        {
            throw new InvalidDataException(
                $"The scan of '{path}' finds sections with [{string.Join(", ", positions.Select(section => section.Count))}] test tokens, "
                + $"the file has sections with [{string.Join(", ", file.Section.Select(section => section.Test.Count))}] cases.");
        }

        List<ConformanceCase> cases = [];
        for (int section = 0; section < file.Section.Count; section++)
        {
            SimpleTestSection source = file.Section[section];
            for (int test = 0; test < source.Test.Count; test++)
            {
                cases.Add(Case(path, $"{file.Name}/{source.Name}", source.Test[test], positions[section][test]));
            }
        }

        return cases;
    }

    private static ConformanceCase Case(string path, string prefix, SimpleTest test, TextPosition position) =>
        new(
            $"{prefix}/{test.Name}",
            path,
            position.Line,
            position.Column,
            test.Expr,
            new CaseEnvironment(test.Container, [.. test.TypeEnv], test.DisableMacros),
            new Dictionary<string, ExprValue>(test.Bindings, System.StringComparer.Ordinal),
            test.CheckOnly ? CheckMode.CheckOnly : test.DisableCheck ? CheckMode.WithoutCheck : CheckMode.Check,
            Expected(test));

    private static Expectation Expected(SimpleTest test) => test.ResultMatcherCase switch
    {
        SimpleTest.ResultMatcherOneofCase.Value => new(ExpectationKind.Value, "value", test.Value, null),
        SimpleTest.ResultMatcherOneofCase.TypedResult => new(ExpectationKind.TypedResult, "typed_result", test.TypedResult.Result, test.TypedResult.DeducedType),
        SimpleTest.ResultMatcherOneofCase.EvalError => new(ExpectationKind.EvaluationError, "eval_error", null, null),
        SimpleTest.ResultMatcherOneofCase.AnyEvalErrors => new(ExpectationKind.Other, "any_eval_errors", null, null),
        SimpleTest.ResultMatcherOneofCase.Unknown => new(ExpectationKind.Other, "unknown", null, null),
        SimpleTest.ResultMatcherOneofCase.AnyUnknowns => new(ExpectationKind.Other, "any_unknowns", null, null),
        _ => DefaultExpectation,
    };
}
