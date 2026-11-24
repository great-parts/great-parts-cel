using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Examples;

// The places of the errors of a policy are places in the document, exact for the text of a block, of a quoted scalar with escape sequences
// and of a scalar folded over lines, where cel-go shifts them; an error of evaluation reports the document too.
[Trait("Requirement", "F-30")]
public sealed class PositionsTests
{
    // policy/testdata/yaml_parsing_cel_error/policy.yaml of cel-go: the folded and the literal blocks, with the indentation of the lines
    // kept in the expression, so the places of the expression are the places of the document.
    private const string Blocks = """
        name: yaml_parsing_cel_error

        description: |
            A block literal description with mutliple lines.

        rule:
          match:
            - condition: "match_id == 'folded_error_presentation'"
              output: >
                "foo" +
                ("bar" + 1)
            - condition: "match_id == 'folded_error_presentation_indent'"
              output: >
                  "foo" +
                  ("bar" + 1)
            - condition: "match_id == 'literal_error_presentation'"
              output: |
                "foo" +
                ("bar" + 1)
            - condition: "match_id == 'literal_error_presentation_indent'"
              output: |
                  "foo" +
                  ("bar" + 1)
            - output: "'no match encountered'"
        """;

    [Fact]
    public void ErrorsInBlocks_ReportTheLineAndTheColumnOfTheDocument()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Blocks);

        // The same places as the test of cel-go: the operator + of ("bar" + 1) in each of the four outputs.
        Assert.Equal(4, result.Errors.Length);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code));
        (int, int)[] expected = [(11, 16), (15, 18), (19, 16), (23, 18)];
        Assert.Equal(expected, result.Errors.Select(Where).ToArray());
    }

    [Fact]
    public void EscapeSequenceInQuotes_DoesNotShiftThePlace()
    {
        const string Document = """"
            rule:
              match:
                - output: "'a' + \"b\\\"\" + nope"
            """";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Equal(Place(Document, Document.IndexOf("nope", StringComparison.Ordinal)), Show(Where(error)));
    }

    [Fact]
    public void ScalarFoldedOverLines_ReportsTheLineOfTheName()
    {
        const string Document = "rule:\n  match:\n    - output: 'x +\n      nope'";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Equal((4, 7), Where(error));
    }

    [Fact]
    public void EvaluationErrorInAVariable_ReportsThePlaceInTheDocument()
    {
        const string Document = "rule:\n  variables:\n    - name: bad\n      expression: \"1 / 0\"\n  match:\n    - output: \"variables.bad\"";
        CelPolicyCompileResult<long> result = Compile<long>(Document);

        CelEvalResult<long> evaluated = result.GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(evaluated.Errors);
        Assert.Equal(CelErrorCode.DivisionByZero, error.Code);
        Assert.Equal(Source, Location(error).SourceName);
        Assert.Equal(Place(Document, Document.IndexOf('/', StringComparison.Ordinal)), Show(Where(error)));
    }

    private static string Show((int Line, int Column) place) =>
        place.Line.ToString(CultureInfo.InvariantCulture) + ":" + place.Column.ToString(CultureInfo.InvariantCulture);
}
