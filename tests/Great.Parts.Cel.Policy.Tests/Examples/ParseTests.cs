using System;
using Great.Parts.Cel.Diagnostics;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Examples;

// CelPolicy.Parse: the document of a policy is read into a policy, or every error of the document comes back with its place; any text
// gives a result. The texts of the errors of the structure are those of cel-go.
[Trait("Requirement", "F-30")]
public sealed class ParseTests
{
    private const string Source = "p.yaml";

    // A policy with the output "1" under the key under test, to which each example adds what it tests.
    private const string Minimal = "rule:\n  match:\n    - output: \"1\"";

    [Fact]
    public void FullDocument_HasTheNameTheDescriptionTheImportsAndTheText()
    {
        const string Text = "name: n\ndescription: d\nimports:\n  - name: a.b.T\nrule:\n  match:\n    - output: \"1\"";

        CelPolicyParseResult result = CelPolicy.Parse(Text, Source);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        CelPolicy policy = result.Policy;
        Assert.Equal("n", policy.Name);
        Assert.Equal("d", policy.Description);
        Assert.Equal("a.b.T", Assert.Single(policy.Imports));
        Assert.Equal(Source, policy.SourceName);
        Assert.Equal(Text, policy.Text);
    }

    [Fact]
    public void UnsupportedKey_IsAnErrorAtTheKeyWithTheKeyAsTheSubject()
    {
        CelPolicyParseResult result = CelPolicy.Parse(Minimal + "\nfoo: 1", Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Equal("unsupported policy tag: foo", error.Message);
        Assert.Equal(Source, Location(error).SourceName);
        Assert.Equal((4, 1), Where(error));
        Assert.Equal("foo", error.Subject);
    }

    [Fact]
    public void MatchAndAggregateInOneRule_IsAnErrorAtTheSecondKey()
    {
        CelPolicyParseResult result = CelPolicy.Parse("rule:\n  match: []\n  aggregate: []", Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("Only one of 'match' or 'aggregate' may be set in a rule", error.Message);
        Assert.Equal((3, 3), Where(error));
        Assert.Equal("aggregate", error.Subject);
    }

    [Fact]
    public void MatchWithoutAnOutputOrARule_IsAnErrorAtTheMatch()
    {
        CelPolicyParseResult result = CelPolicy.Parse("rule:\n  match:\n    - condition: \"true\"", Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("match does not specify a rule or output", error.Message);
        Assert.Equal((3, 7), Where(error));
    }

    [Fact]
    public void OutputThenRule_IsAnErrorAtTheRule()
    {
        CelPolicyParseResult result = CelPolicy.Parse(
            "rule:\n  match:\n    - output: \"1\"\n      rule:\n        match:\n          - output: \"2\"",
            Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("only the rule or the output may be set", error.Message);
        Assert.Equal((4, 7), Where(error));
        Assert.Equal("rule", error.Subject);
    }

    [Fact]
    public void RuleThenOutput_IsAnErrorAtTheOutput()
    {
        CelPolicyParseResult result = CelPolicy.Parse(
            "rule:\n  match:\n    - rule:\n        match:\n          - output: \"2\"\n      output: \"1\"",
            Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("only the rule or the output may be set", error.Message);
        Assert.Equal((6, 7), Where(error));
        Assert.Equal("output", error.Subject);
    }

    [Fact]
    public void ExplanationOfANestedRule_IsAnError()
    {
        CelPolicyParseResult result = CelPolicy.Parse(
            "rule:\n  match:\n    - rule:\n        match:\n          - output: \"2\"\n      explanation: \"x\"",
            Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("explanation can only be set on output match cases, not nested rules", error.Message);
        Assert.Equal("explanation", error.Subject);
    }

    [Fact]
    public void OutputThatIsAnInteger_IsAnErrorOfTheTypeAtTheValue()
    {
        CelPolicyParseResult result = CelPolicy.Parse("rule:\n  match:\n    - output: 1", Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            "got yaml node type tag:yaml.org,2002:int, wanted type(s) [tag:yaml.org,2002:str !txt]",
            error.Message);
        Assert.Equal((3, 15), Where(error));
    }

    [Fact]
    public void OutputWithAnUnknownTag_IsAnError()
    {
        CelPolicyParseResult result = CelPolicy.Parse("rule:\n  match:\n    - output: !foo \"x\"", Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("unsupported yaml tag type: !foo", error.Message);
        Assert.Equal("!foo", error.Subject);
    }

    [Fact]
    public void DocumentThatIsAList_IsAnErrorOfTheType()
    {
        CelPolicyParseResult result = CelPolicy.Parse("- a", Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal("got yaml node type tag:yaml.org,2002:seq, wanted type(s) [tag:yaml.org,2002:map]", error.Message);
    }

    [Fact]
    public void TextThatIsNotYaml_IsInvalidYamlAndGetPolicyOrThrowThrowsIt()
    {
        CelPolicyParseResult result = CelPolicy.Parse("a: [", Source);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Policy);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidYaml, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        CelException exception = Assert.Throws<CelException>(() => result.GetPolicyOrThrow());
        Assert.Equal(error, Assert.Single(exception.Errors));
    }

    [Fact]
    public void EmptyDocument_IsInvalidPolicy()
    {
        CelPolicyParseResult result = CelPolicy.Parse(string.Empty, Source);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal((1, 1), Where(error));
    }

    [Fact]
    public void NullTextAndEmptyName_AreArgumentErrors()
    {
        Assert.Throws<ArgumentNullException>(() => CelPolicy.Parse(null!, "p"));
        Assert.Throws<ArgumentException>(() => CelPolicy.Parse(string.Empty, string.Empty));
    }

    // The file testdata/yaml_parsing/policy.yaml of cel-go: the expressions of the block scalars are the lines of the document with their
    // indentation, joined by line feeds.
    [Fact]
    public void YamlParsingFileOfCelGo_HasTheBlocksAsLinesOfTheDocument()
    {
        const string Text = """
            name: yaml_parsing

            description: |
              A block literal description with mutliple lines.
               - line 2
               - line 3

            rule:
              match:
                - condition: "match_id == 'folded_unambiguous'"
                  output: >
                    "a string expression that " +
                    "is folded"
                - condition: "match_id == 'folded_line_break'"
                  output: >
                    '''a string expression that
                    is folded'''
                - condition: "match_id == 'folded_line_break_indent'"
                  output: >
                      '''a string expression that
                      is folded'''
                - condition: "match_id == 'literal_unambiguous'"
                  output: |
                      "a string expression that " +
                      "is a literal block"
                - condition: "match_id == 'literal_line_break'"
                  output: |
                    '''a string expression that
                    is a literal block'''
                - condition: "match_id == 'literal_line_break_indent'"
                  output: |
                      '''a string expression that
                      is a literal block'''
                - output: "'no match encountered'"
            """;

        CelPolicyParseResult result = CelPolicy.Parse(Text, Source);

        CelPolicy policy = result.GetPolicyOrThrow();
        Assert.Equal("yaml_parsing", policy.Name);
        Assert.Equal("A block literal description with mutliple lines.\n - line 2\n - line 3\n", policy.Description);
        PolicyRule rule = policy.Document.Rule!;
        Assert.Equal(PolicySemantic.FirstMatch, rule.Semantic);
        Assert.Equal(7, rule.Matches.Count);
        Assert.Equal("        \"a string expression that \" +\n        \"is folded\"", rule.Matches[0].Output!.Text);
        Assert.Equal("        '''a string expression that\n        is folded'''", rule.Matches[1].Output!.Text);
        Assert.Equal("          '''a string expression that\n          is folded'''", rule.Matches[2].Output!.Text);
        Assert.Equal("          \"a string expression that \" +\n          \"is a literal block\"", rule.Matches[3].Output!.Text);
        Assert.Equal("        '''a string expression that\n        is a literal block'''", rule.Matches[4].Output!.Text);
        Assert.Equal("          '''a string expression that\n          is a literal block'''", rule.Matches[5].Output!.Text);
        Assert.Equal("'no match encountered'", rule.Matches[6].Output!.Text);
    }
}
