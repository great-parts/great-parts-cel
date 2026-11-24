using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;

namespace Great.Parts.Cel.Policy.Tests.Examples;

// The composition of the rules of a policy into one expression: the composed expression of each policy is the expression that cel-go
// composes (policy/helper_test.go and composer_test.go), equal as a tree. A text is compared as the tree it parses to, printed again.
[Trait("Requirement", "F-30")]
public sealed class ComposeTests
{
    private const string Unnest = """
        name: unnest
        rule:
          variables:
            - name: even_greater
              expression: >
                values.filter(x, x > 2)
          match:
            - condition: >
                variables.even_greater.size() == 0 ? false :
                variables.even_greater.all(x, x % 2 == 0)
              output: >
                "some divisible by 2"
            - condition: "values.map(x, x * 3).exists(x, x % 4 == 0)"
              output: >
                "at least one divisible by 4"
            - condition: "values.map(x, x * x * x).exists(x, x % 6 == 0)"
              output: >
                "at least one power of 6"
        """;

    private static CelParsedExpr Composed(string document, int unnestHeight = ExpressionUnnester.DefaultHeight)
    {
        CelPolicyCompileResult<CelValue> result = PolicyCompiler.Compile<CelValue>(Read(document), Env, null, unnestHeight);
        Assert.True(result.IsSuccess, string.Join('\n', result.Errors));
        Assert.NotNull(result.Expression);
        return result.Expression;
    }

    [Fact]
    public void UnconditionalNestedRule_IsUnwrappedWithOrValue()
    {
        const string Document = """
            name: nested_rules_unconditional_chaining
            rule:
              match:
                - rule:
                    variables:
                      - name: foo
                        expression: "3"
                    match:
                      - condition: "x > variables.foo"
                        output: "'a'"
                      - condition: "x == variables.foo"
                        output: "'b'"
                - output: "'c'"
            """;

        AssertEqualExpression(
            """
            cel.@block([3],
            ((x > @index0) ? optional.of("a") : ((x == @index0) ? optional.of("b") : optional.none()))
              .orValue("c"))
            """,
            Composed(Document));
    }

    [Fact]
    public void NestedRuleThenAConditionalMatch_IsChainedWithOr()
    {
        const string Document = """
            name: nested_rules_unconditional_chaining_optional
            rule:
              match:
                - rule:
                    variables:
                      - name: foo
                        expression: "3"
                    match:
                      - condition: "x > variables.foo"
                        output: "'a'"
                      - condition: "x == variables.foo"
                        output: "'b'"
                - condition: "x == 1"
                  output: "'c'"
            """;

        AssertEqualExpression(
            """
            cel.@block([3],
            ((x > @index0) ? optional.of("a") : ((x == @index0) ? optional.of("b") : optional.none()))
              .or((x == 1) ? optional.of("c") : optional.none()))
            """,
            Composed(Document));
    }

    [Fact]
    public void NestedRuleInANestedRule_IsUnwrappedAndWrappedAgain()
    {
        const string Document = """
            name: nested_rules_unwrap_rewrap
            rule:
              match:
                - condition: "x == 1"
                  rule:
                    match:
                      - rule:
                          match:
                            - condition: "y == 1"
                              output: "'a'"
                      - output: "'b'"
            """;

        AssertEqualExpression(
            """
            (x == 1)
              ? optional.of(((y == 1) ? optional.of("a") : optional.none()).orValue("b"))
              : optional.none()
            """,
            Composed(Document));
    }

    [Fact]
    public void UnconditionalNestedRuleThatMayGiveNothing_IsAnOptionalStepOfItsRule()
    {
        const string Document = """
            rule:
              match:
                - condition: "2 == 2"
                  rule:
                    match:
                      - condition: "1 == 1"
                        output: "'foo'"
                - condition: "true"
                  rule:
                    match:
                      - condition: "3 == 3"
                        output: "'bar'"
            """;

        AssertEqualExpression(
            """
            (2 == 2) ? ((1 == 1) ? optional.of("foo") : optional.none())
              : ((3 == 3) ? optional.of("bar") : optional.none())
            """,
            Composed(Document));
    }

    [Fact]
    public void AggregateRule_AddsTheListsOfItsMatches()
    {
        const string Document = "rule:\n  aggregate:\n    - condition: \"x > 0\"\n      output: \"1\"\n    - output: \"2\"";

        AssertEqualExpression("((x > 0) ? [1] : []) + [2]", Composed(Document));
    }

    [Fact]
    public void AggregateRuleWithAnEmptyNestedRule_IsTheEmptyList()
    {
        const string Document = "rule:\n  aggregate:\n    - condition: \"true\"\n      rule:\n        match: []";

        AssertEqualExpression("[]", Composed(Document));
    }

    [Fact]
    public void VariableUsedInTheOutput_IsASlotOfTheBlock()
    {
        AssertEqualExpression("cel.@block([x + 1], @index0 * 2)", Composed("rule:\n  variables:\n    - name: a\n      expression: \"x + 1\"\n  match:\n    - output: \"variables.a * 2\""));
    }

    [Fact]
    public void DeepCall_MovesIntoSlotsAtTheDefaultHeight_ThatTheLibraryOfCelGoSets()
    {
        AssertEqualExpression(
            """
            cel.@block([values.filter(x, x > 2)],
            ((@index0.size() == 0) ? false : @index0.all(x, x % 2 == 0))
            ? optional.of("some divisible by 2")
            : (values.map(x, x * 3).exists(x, x % 4 == 0)
               ? optional.of("at least one divisible by 4")
               : (values.map(x, x * x * x).exists(x, x % 6 == 0)
                 ? optional.of("at least one power of 6")
                 : optional.none())))
            """,
            Composed(Unnest));
    }

    [Fact]
    public void DeepCall_MovesIntoSlotsFromTheHeightGiven()
    {
        AssertEqualExpression(
            """
            cel.@block([
              values.filter(x, x > 2),
              @index0.size() == 0,
              @index1 ? false : @index0.all(x, x % 2 == 0),
              values.map(x, x * x * x).exists(x, x % 6 == 0)
                ? optional.of("at least one power of 6")
                : optional.none(),
              values.map(x, x * 3).exists(x, x % 4 == 0)
                ? optional.of("at least one divisible by 4")
                : @index3],
              @index2 ? optional.of("some divisible by 2") : @index4)
            """,
            Composed(Unnest, 2));
    }

    [Fact]
    public void ComposedExpression_HasPositionsOnlyForTheNodesOfTheExpressions()
    {
        const string Document = "rule:\n  match:\n    - condition: \"x > 1\"\n      output: \"'big'\"\n    - output: \"'small'\"";

        CelParsedExpr expression = Composed(Document);

        var conditional = Assert.IsType<CelCallNode>(expression.Root);
        Assert.Equal(default(CelSourceSpan), expression.GetSpan(conditional.Id));
        CelSourceSpan condition = expression.GetSpan(conditional.Arguments[0].Id);
        Assert.Equal((3, 21), (condition.Start.Line, condition.Start.Column));
    }
}
