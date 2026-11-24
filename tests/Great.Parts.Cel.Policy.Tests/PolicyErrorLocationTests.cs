using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests;

// The places of the errors of the policies of the test data of cel-go v0.33.4 (policy/testdata) in the document, against the places that
// its test lists (policy/helper_test.go, policyErrorTests). The documents are written here as they are in cel-go, with the lines of
// their license header, so that a line is the line of cel-go. Where the place of cel-go is the place of the node, the line and the column
// are equal; the places where this library differs are named in the test with the reason.
[Trait("Requirement", "F-30")]
public sealed class PolicyErrorLocationTests
{
    // The 13 lines of the license and the empty line, so that the first key is on the line 15, as in the files of cel-go.
    private const string Header = "#\n#\n#\n#\n#\n#\n#\n#\n#\n#\n#\n#\n#\n\n";

    // policy/testdata/errors/policy.yaml, whose environment has the sets lists and sets, and no strings.
    private const string Errors = Header + """
        name: "errors"
        imports:
          - name: " untrimmed.Import1  "
          - name: >
              punc.Import!
          - name: "bad import"
        rule:
          variables:
            - name: want
              expression: spec.labels
            - name: want
              expression: "2"
            - name: missing
              expression: variables.want.filter(l, !(lin resource.labels))
            - name: bad_data
              expression: "{1:305 2:569}"
            - name: invalid
              expression: >
                resource.labels.filter(l,
                  l in variables.want && variables.want[l] != resource.labels[l])
          match:
            - condition: variables.missing.size() > 0
              output: |
                "missing one or more required labels: %s".format(variables.missing])
            - condition: variables.invalid.size() > 0
              output: |
                "invalid values provided on one or more labels: %s".format([variables.invalid])
            - condition: "1 > 0"
              output: "true"
            - condition: "1 < 0"
              output: "'false'"
        """;

    // The document of errors with its expressions and its imports valid, on the same lines, and the outputs of the first two matches of
    // the type bool, as the outputs with a syntax error have no type in cel-go: what remains are the errors that the checks by type
    // report, which this library reports when no expression has an error.
    private const string TypeErrors = Header + """
        name: "errors"
        imports:
          - name: " untrimmed.Import1  "
          - name: >
              punc.Import
          - name: "good.Import2"
        rule:
          variables:
            - name: want
              expression: request.labels
            - name: want
              expression: "2"
            - name: missing
              expression: request.labels.filter(l, !(l in resource.labels))
            - name: bad_data
              expression: "{1:305, 2:569}"
            - name: invalid
              expression: >
                resource.labels.filter(l,
                  l in request.labels && request.labels[l] != resource.labels[l])
          match:
            - condition: variables.missing.size() > 0
              output: |
                variables.missing.size() > 1
            - condition: variables.invalid.size() > 0
              output: |
                variables.invalid.size() > 2
            - condition: "1 > 0"
              output: "true"
            - condition: "1 < 0"
              output: "'false'"
        """;

    // policy/testdata/errors_unreachable/policy.yaml
    private const string Unreachable = Header + """
        name: "errors_unreachable"
        rule:
          variables:
            - name: want
              expression: request.labels
            - name: missing
              expression: variables.want.filter(l, !(l in resource.labels))
            - name: invalid
              expression: >
                resource.labels.filter(l,
                  l in variables.want && variables.want[l] != resource.labels[l])
          match:
            - rule:
                match:
                  - output: "''"
            - condition: variables.missing.size() > 0
              output: |
                "missing one or more required labels: %s".format([variables.missing])
            - condition: variables.invalid.size() > 0
              rule:
                match:
                  - output: |
                      "invalid values provided on one or more labels: %s".format([variables.invalid])
                  - condition: "false"
                    output: "'unreachable'"
        """;

    // policy/testdata/nested_incompatible_outputs/policy.yaml
    private const string NestedIncompatible = Header + """
        name: nested_incompatible_outputs
        rule:
          match:
            - condition: "1 < 0"
              output: "true"
            - condition: "1 > 0"
              rule:
                match:
                  - output: "'false'"
        """;

    // policy/testdata/aggregate_errors/policy.yaml
    private const string AggregateErrors = Header + """
        name: aggregate_errors
        rule:
          aggregate:
            - condition: "true"
              rule:
                match:
                  - condition: "true"
                    output: "optional.of('USER_PII')"
                  - condition: "true"
                    output: "403"
        """;

    // policy/testdata/aggregate_list_errors/policy.yaml
    private const string AggregateListErrors = Header + """
        name: aggregate_list_errors
        rule:
          aggregate:
            - condition: "true"
              rule:
                match:
                  - condition: "true"
                    output: "['tag1', 'tag2']"
                  - condition: "true"
                    output: "403"
        """;

    // policy/testdata/aggregate_nested_mixed_semantics/policy.yaml
    private const string NestedMixed = Header + """
        name: aggregate_nested_mixed_semantics
        rule:
          aggregate:
            - condition: "true"
              rule:
                match:
                  - condition: "true"
                    rule:
                      aggregate:
                        - condition: "true"
                          output: "'foo'"
        """;

    // The environment of the configuration of cel-go of these documents: the variables and the function of the configuration, the sets
    // lists and sets, and strings when the policy calls its functions.
    private static CelEnvironment Environment(bool withStrings)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<string>("destination.ip")
            .AddVariable<string>("origin.ip")
            .AddVariable("spec.restricted_destinations", CelType.ListOf(CelType.String))
            .AddVariable<string>("spec.origin")
            .AddVariable("request", CelType.MapOf(CelType.String, CelType.Dyn))
            .AddVariable("resource", CelType.MapOf(CelType.String, CelType.Dyn))
            .AddFunction<string, string>("locationCode", code => code)
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Sets(0))
            .AddExtension(CelExtensions.Optional(2));
        return (withStrings ? builder.AddExtension(CelExtensions.Strings(5)) : builder).Build().GetEnvironmentOrThrow();
    }

    private static (int Line, int Column, string Message)[] PlacesOf(CelPolicyCompileResult<CelValue> result) =>
        [.. result.Errors.Select(error => (Where(error).Line, Where(error).Column, error.Message))];

    private static void AssertPlaces(CelPolicyCompileResult<CelValue> result, params (int Line, int Column, string Message)[] expected)
    {
        (int Line, int Column, string Message)[] actual = PlacesOf(result);
        Assert.Equal(expected.Select(item => (item.Line, item.Column)), actual.Select(item => (item.Line, item.Column)));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Contains(expected[i].Message, actual[i].Message, StringComparison.Ordinal);
        }
    }

    // The errors of the expressions and of the imports, at the places of cel-go. Of the ten errors that cel-go lists for this document
    // the test differs in four, each for a reason:
    //  - the parser of this library gives the first syntax error of an expression, at the place of the token that it cannot accept, which
    //    is the place of cel-go for the first error of each expression; the second error of an expression, "extraneous input" at 28:66,
    //    is an effect of the recovery of ANTLR that this parser does not have;
    //  - the import written as a block is reported at the indicator of the block, 18:11, where cel-go reports 19:1, the start of the
    //    text of the block; the import in quotes has the place of cel-go, 20:12;
    //  - the checks by type (the overlapping variable at 25:7 and the incompatible outputs at 45:16) are not made while an expression
    //    has an error, so the test of the second document has them;
    //  - the second variable want, of the type int, is not a declaration in cel-go, which keeps the first one, so the later uses of
    //    variables.want have no error there; here the later uses see the type int and are two errors, at 34:13 and 34:48.
    [Fact]
    public void ErrorsDocument_ReportsTheErrorsOfTheExpressionsAndImportsAtThePlacesOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(Errors).Compile<CelValue>(Environment(withStrings: false));

        AssertPlaces(
            result,
            (18, 11, "invalid qualified name: punc.Import!"),
            (20, 12, "invalid qualified name: bad import"),
            (24, 19, "undeclared reference to 'spec'"),
            (28, 50, "syntax error"),
            (30, 27, "syntax error"),
            (34, 13, "no matching overload"),
            (34, 48, "no matching overload"),
            (38, 75, "syntax error"),
            (41, 67, "undeclared reference to 'format'"));
    }

    [Fact]
    public void TypeErrorsDocument_ReportsTheVariableAndTheOutputAtThePlacesOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(TypeErrors).Compile<CelValue>(Environment(withStrings: true));

        AssertPlaces(
            result,
            (25, 7, "overlapping identifier for name 'variables.want'"),
            (45, 16, "incompatible output types: block has output type string, but previous outputs have type bool"));
    }

    [Fact]
    public void UnreachableDocument_ReportsTheRuleAndTheMatchesAtThePlacesOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(Unreachable).Compile<CelValue>(Environment(withStrings: true));

        AssertPlaces(
            result,
            (28, 9, "rule creates unreachable outputs"),
            (36, 13, "match creates unreachable outputs"),
            (38, 13, "Condition is always false"));
    }

    [Fact]
    public void NestedIncompatibleOutputsDocument_ReportsTheNestedRuleAtThePlaceOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(NestedIncompatible).Compile<CelValue>(Env);

        AssertPlaces(result, (22, 9, "incompatible output types: block has output type string, but previous outputs have type bool"));
    }

    [Fact]
    public void AggregateErrorsDocument_ReportsTheMatchAndTheOutputAtThePlacesOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(AggregateErrors).Compile<CelValue>(Env);

        AssertPlaces(
            result,
            (21, 13, "match creates unreachable outputs"),
            (24, 22, "incompatible output types: block has output type int, but previous outputs have type optional_type(string)"));
    }

    [Fact]
    public void AggregateListErrorsDocument_ReportsTheMatchAndTheOutputAtThePlacesOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(AggregateListErrors).Compile<CelValue>(Env);

        AssertPlaces(
            result,
            (21, 13, "match creates unreachable outputs"),
            (24, 22, "incompatible output types: block has output type int, but previous outputs have type list(string)"));
    }

    [Fact]
    public void NestedMixedSemanticsDocument_ReportsTheNestedAggregateAtThePlaceOfCelGo()
    {
        CelPolicyCompileResult<CelValue> result = Read(NestedMixed).Compile<CelValue>(Env);

        AssertPlaces(result, (23, 15, "nested aggregate rules are not allowed"));
    }

    // policy/testdata/limits of cel-go sets the limit to 2 and to 5 expressions, which this library does not offer: its limit is 100, so
    // the documents here have a hundred and one variables and a hundred and one nested rules. The place is the one of cel-go for its
    // limit: the name of the variable that passes it, and the first key of the rule that passes it, which, as the count is made when a
    // rule is done, is the outermost of the nested rules.
    [Fact]
    public void LimitOfNestedExpressions_IsReportedAtTheVariableNameAndAtTheFirstKeyOfTheRule()
    {
        var variables = new StringBuilder("rule:\n  variables:\n");
        for (int i = 0; i < 101; i++)
        {
            variables.Append(CultureInfo.InvariantCulture, $"    - name: v{i}\n      expression: \"1\"\n");
        }

        CelPolicyCompileResult<CelValue> byVariable = Compile<CelValue>(variables.Append("  match:\n    - output: \"1\"").ToString());

        var rules = new StringBuilder("rule:\n");
        for (int level = 0; level < 101; level++)
        {
            string margin = new(' ', 6 * level);
            rules.Append(margin).Append("  match:\n").Append(margin).Append("    - rule:\n");
        }

        string deepest = new(' ', 6 * 101);
        rules.Append(deepest).Append("  match:\n").Append(deepest).Append("    - output: \"1\"\n");
        CelPolicyCompileResult<CelValue> byRule = Compile<CelValue>(rules.ToString());

        AssertPlaces(byVariable, (203, 13, "variable exceeds nested expression limit"));
        CelError rule = Assert.Single(byRule.Errors);
        Assert.Equal("rule exceeds nested expression limit", rule.Message);
        Assert.Equal((4, 9), Where(rule));
    }
}
