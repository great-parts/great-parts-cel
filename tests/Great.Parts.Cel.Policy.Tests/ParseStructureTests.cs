using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests;

// The model that Parse builds and the errors of the structure that the examples do not name: every key of the format, the semantic of a
// rule, the strings of the block scalars with their segments, the places of the errors.
[Trait("Requirement", "F-30")]
public sealed class ParseStructureTests
{
    private const string Source = "p.yaml";

    private static PolicyDocument Document(string text) => CelPolicy.Parse(text, Source).GetPolicyOrThrow().Document;

    private static CelError OnlyError(string text) => Assert.Single(CelPolicy.Parse(text, Source).Errors);

    [Fact]
    public void RuleWithEveryKey_HasItsVariablesAndMatches()
    {
        const string Text = """
            rule:
              id: r1
              description: the first rule
              variables:
                - name: a
                  expression: x + 1
                - name: b
                  expression: "variables.a * 2"
              match:
                - condition: variables.b > 2
                  output: "'big'"
                  explanation: "'because'"
                - output: "'small'"
            """;

        PolicyRule rule = Document(Text).Rule!;

        Assert.Equal("r1", rule.Id!.Text);
        Assert.Equal("the first rule", rule.Description!.Text);
        Assert.Equal(PolicySemantic.FirstMatch, rule.Semantic);
        Assert.Equal(["a", "b"], rule.Variables.Select(v => v.Name!.Text).ToArray());
        Assert.Equal(["x + 1", "variables.a * 2"], rule.Variables.Select(v => v.Expression!.Text).ToArray());
        Assert.Equal(2, rule.Matches.Count);
        Assert.Equal("variables.b > 2", rule.Matches[0].Condition!.Text);
        Assert.Equal("'big'", rule.Matches[0].Output!.Text);
        Assert.Equal("'because'", rule.Matches[0].Explanation!.Text);
        Assert.Null(rule.Matches[1].Condition);
        Assert.Null(rule.Matches[1].Explanation);
        Assert.Null(rule.Matches[1].Rule);
    }

    [Fact]
    public void AggregateRule_HasTheAggregateSemantic()
    {
        PolicyRule rule = Document("rule:\n  aggregate:\n    - output: \"1\"").Rule!;

        Assert.Equal(PolicySemantic.Aggregate, rule.Semantic);
        Assert.Single(rule.Matches);
    }

    [Fact]
    public void NestedRule_IsTheRuleOfTheMatch()
    {
        PolicyRule rule = Document("rule:\n  match:\n    - condition: c\n      rule:\n        aggregate:\n          - output: \"2\"").Rule!;

        PolicyRule nested = rule.Matches[0].Rule!;
        Assert.Equal(PolicySemantic.Aggregate, nested.Semantic);
        Assert.Equal("2", Assert.Single(nested.Matches).Output!.Text);
        Assert.Null(rule.Matches[0].Output);
    }

    [Fact]
    public void Imports_AreInTheOrderOfTheDocumentAndAnImportWithoutANameHasTheEmptyName()
    {
        CelPolicy policy = CelPolicy.Parse("imports:\n  - name: a.b.T\n  - other: x\n  - name: c.D\nrule: {match: [{output: \"1\"}]}", Source).GetPolicyOrThrow();

        Assert.Equal(["a.b.T", string.Empty, "c.D"], policy.Imports.ToArray());
    }

    // A name in a block scalar has the indentation of the block, and its line break when the lines end with a carriage return; the list
    // has the names as the compilation reads them.
    [Fact]
    public void Imports_AreTrimmedOfTheIndentationOfABlockAndOfTheSpacesOfAQuotedName()
    {
        const string Text = "imports:\n  - name: >\n      a.b.T\n  - name: \"  c.D \"\n  - name: >\r\n      e.F\r\nrule: {match: [{output: \"1\"}]}";

        CelPolicy policy = CelPolicy.Parse(Text, Source).GetPolicyOrThrow();

        Assert.Equal(["a.b.T", "c.D", "e.F"], policy.Imports.ToArray());
        Assert.NotEqual("a.b.T", policy.Document.Imports[0].Name!.Text);
        Assert.NotEqual("c.D", policy.Document.Imports[1].Name!.Text);
    }

    [Fact]
    public void DocumentWithoutAnythingButARule_HasNoNameNoDescriptionAndNoImports()
    {
        CelPolicy policy = CelPolicy.Parse("rule: {match: [{output: \"1\"}]}", Source).GetPolicyOrThrow();

        Assert.Null(policy.Name);
        Assert.Null(policy.Description);
        Assert.Empty(policy.Imports);
    }

    [Fact]
    public void DocumentWithoutARule_IsAPolicyWithoutARule()
    {
        PolicyDocument document = Document("name: n");

        Assert.Null(document.Rule);
    }

    [Fact]
    public void DescriptionAndNameThatAreBlocks_FollowTheirOwnRules()
    {
        const string Text = "description: >\n  a\n  b\nname: |\n    c\nrule: {match: [{output: \"1\"}]}";

        CelPolicy policy = CelPolicy.Parse(Text, Source).GetPolicyOrThrow();

        Assert.Equal("a b\n", policy.Description);
        Assert.Equal("    c", policy.Name);
    }

    [Theory]
    [InlineData("rule:\n  foo: 1", "unsupported rule tag: foo", "foo", 2, 3)]
    [InlineData("rule:\n  variables:\n    - foo: 1", "unsupported variable tag: foo", "foo", 3, 7)]
    [InlineData("rule:\n  match:\n    - foo: 1\n      output: \"1\"", "unsupported match tag: foo", "foo", 3, 7)]
    public static void UnsupportedKey_NamesTheKindOfTheNode(string text, string message, string subject, int line, int column)
    {
        CelError error = OnlyError(text);

        Assert.Equal(message, error.Message);
        Assert.Equal(subject, error.Subject);
        Assert.Equal((line, column), Where(error));
    }

    [Theory]
    [InlineData("rule: 1", "got yaml node type tag:yaml.org,2002:int, wanted type(s) [tag:yaml.org,2002:map]")]
    [InlineData("rule:\n  variables: x", "got yaml node type tag:yaml.org,2002:str, wanted type(s) [tag:yaml.org,2002:seq]")]
    [InlineData("rule:\n  match: {a: b}", "got yaml node type tag:yaml.org,2002:map, wanted type(s) [tag:yaml.org,2002:seq]")]
    [InlineData("rule:\n  match:\n    - x", "got yaml node type tag:yaml.org,2002:str, wanted type(s) [tag:yaml.org,2002:map]")]
    [InlineData("name: [a]", "got yaml node type tag:yaml.org,2002:seq, wanted type(s) [tag:yaml.org,2002:str !txt]")]
    [InlineData("rule:\n  id: true", "got yaml node type tag:yaml.org,2002:bool, wanted type(s) [tag:yaml.org,2002:str !txt]")]
    [InlineData("rule:\n  id: 2024-01-01", "got yaml node type tag:yaml.org,2002:timestamp, wanted type(s) [tag:yaml.org,2002:str !txt]")]
    [InlineData("rule:\n  id: ~", "got yaml node type tag:yaml.org,2002:null, wanted type(s) [tag:yaml.org,2002:str !txt]")]
    [InlineData("imports: x", "got yaml node type tag:yaml.org,2002:str, wanted type(s) [tag:yaml.org,2002:seq]")]
    [InlineData("imports:\n  - x", "got yaml node type tag:yaml.org,2002:str, wanted type(s) [tag:yaml.org,2002:map]")]
    [InlineData("rule:\n  id: !!binary x", "unsupported yaml tag type: tag:yaml.org,2002:binary")]
    public static void NodeOfAnotherType_IsAnErrorOfTheTypeWithTheTextOfCelGo(string text, string message)
    {
        CelError error = OnlyError(text);

        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void SeveralErrors_AreAllReported()
    {
        CelPolicyParseResult result = CelPolicy.Parse("rule:\n  id: 1\n  foo: 2\n  match:\n    - bar: 3", Source);

        Assert.Equal(
            ["got yaml node type tag:yaml.org,2002:int, wanted type(s) [tag:yaml.org,2002:str !txt]", "unsupported rule tag: foo", "unsupported match tag: bar", "match does not specify a rule or output"],
            result.Errors.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void QuotedKey_IsAtTheCharacterAfterTheQuote()
    {
        CelError error = OnlyError("rule: {match: [{output: \"1\"}]}\n\"foo\": 1");

        Assert.Equal("unsupported policy tag: foo", error.Message);
        Assert.Equal((2, 2), Where(error));
        Assert.Equal(3, error.Span.Length);
    }

    [Fact]
    public void ErrorLength_IsTheLengthOfTheScalar()
    {
        CelError error = OnlyError("rule: {match: [{output: \"1\"}]}\nfoobar: 1");

        Assert.Equal(6, error.Span.Length);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
    }

    // A block scalar is the lines of the document from the line after the key while they are indented deeper than the key.
    [Fact]
    public void BlockScalar_IsTheLinesOfTheDocumentWithOneSegmentAtTheStartOfTheFirstLine()
    {
        const string Text = "rule:\n  match:\n    - output: |\n        a +\n          b\n      condition: \"true\"";

        PolicyString output = Document(Text).Rule!.Matches[0].Output!;

        Assert.Equal("        a +\n          b", output.Text);
        CelSourceSegment segment = Assert.Single(output.Segments);
        Assert.Equal(0, segment.ExpressionOffset);
        Assert.Equal(("p.yaml", 4, 1, Text.IndexOf("        a +", System.StringComparison.Ordinal)), (segment.DocumentLocation.SourceName, segment.DocumentLocation.Line, segment.DocumentLocation.Column, segment.DocumentLocation.Offset));
    }

    [Fact]
    public void BlockScalar_StopsAtAnEmptyLine()
    {
        const string Text = "rule:\n  match:\n    - output: |\n        a\n\n        b";

        PolicyString output = Document(Text).Rule!.Matches[0].Output!;

        Assert.Equal("        a", output.Text);
    }

    [Fact]
    public void BlockScalar_OfADocumentWithCarriageReturns_KeepsThemInTheText()
    {
        const string Text = "rule:\r\n  match:\r\n    - output: |\r\n        a\r\n        b\r\n";

        PolicyString output = Document(Text).Rule!.Matches[0].Output!;

        Assert.Equal("        a\r\n        b\r", output.Text);
        Assert.Equal(4, Assert.Single(output.Segments).DocumentLocation.Line);
    }

    [Fact]
    public void BlockScalarWithTheTagTxt_IsTheValueOfYaml()
    {
        const string Text = "rule:\n  match:\n    - output: !txt |\n        a\n        b\n";

        PolicyString output = Document(Text).Rule!.Matches[0].Output!;

        Assert.Equal("a\nb\n", output.Text);
        Assert.Equal(2, output.Segments.Length);
    }

    [Fact]
    public void BlockScalarWithAnotherTag_IsTheValueOfYaml()
    {
        const string Text = "rule:\n  match:\n    - output: !!str |\n        a\n        b\n";

        PolicyString output = Document(Text).Rule!.Matches[0].Output!;

        Assert.Equal("a\nb\n", output.Text);
    }

    [Fact]
    public void BlockScalarAtTheEndOfTheDocumentWithoutLines_IsEmpty()
    {
        PolicyString output = Document("rule:\n  match:\n    - output: |").Rule!.Matches[0].Output!;

        Assert.Equal(string.Empty, output.Text);
        Assert.Equal(0, Assert.Single(output.Segments).ExpressionOffset);
    }

    [Fact]
    public void QuotedExpression_HasTheSegmentsOfTheScalar()
    {
        const string Text = "rule:\n  match:\n    - output: 'x +\n        y'";

        PolicyString output = Document(Text).Rule!.Matches[0].Output!;

        Assert.Equal("x + y", output.Text);
        Assert.Equal([(0, 3, 16), (4, 4, 9)], output.Segments.Select(s => (s.ExpressionOffset, s.DocumentLocation.Line, s.DocumentLocation.Column)).ToArray());
    }

    [Fact]
    public void LimitOfTheCompilation_IsNotAnErrorOfTheReading()
    {
        var text = new System.Text.StringBuilder("rule:\n  variables:\n");
        for (int i = 0; i < 150; i++)
        {
            text.Append("    - name: v").Append(i).Append("\n      expression: \"1\"\n");
        }

        text.Append("  match:\n    - output: \"1\"\n");

        PolicyRule rule = Document(text.ToString()).Rule!;

        Assert.Equal(150, rule.Variables.Count);
    }

    [Fact]
    public void LineTable_CountsTheThreeLineEndings()
    {
        var table = new LineTable("a\nb\r\nc\rd");

        Assert.Equal((1, 1), table.Locate(0));
        Assert.Equal((2, 1), table.Locate(2));
        Assert.Equal((2, 2), table.Locate(3));
        Assert.Equal((3, 1), table.Locate(5));
        Assert.Equal((4, 1), table.Locate(7));
        Assert.Equal((4, 2), table.Locate(8));
    }
}
