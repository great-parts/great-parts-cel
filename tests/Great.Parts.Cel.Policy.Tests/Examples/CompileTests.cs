using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Examples;

// CelPolicy.Compile: a policy compiles in an environment into one program that gives the result of the rules, as cel-go composes them; a
// policy with errors gives all of them, each at its place in the document, and no program. The documents are short ones of the
// examples and the policies of the test data of cel-go (policy/testdata).
[Trait("Requirement", "F-30")]
public sealed class CompileTests
{
    private const string FirstMatch = "rule:\n  match:\n    - condition: \"x > 1\"\n      output: \"'big'\"\n    - output: \"'small'\"";

    private const string Conditional = "rule:\n  match:\n    - condition: \"x > 1\"\n      output: \"'big'\"";

    private const string Chaining = """
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

    [Theory]
    [InlineData(2L, "big")]
    [InlineData(0L, "small")]
    public static void FirstMatch_GivesTheOutputOfTheFirstMatchThatHolds(long x, string expected)
    {
        CelPolicyCompileResult<string> result = Compile<string>(FirstMatch);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(expected, Run(result, x));
    }

    [Fact]
    public void RuleThatMayGiveNothing_IsAnOptionalValue()
    {
        CelPolicyCompileResult<CelOptional<string>> result = Compile<CelOptional<string>>(Conditional);

        Assert.False(Run(result, 0).HasValue);
        Assert.Equal("big", Run(result, 2).Value);
    }

    [Fact]
    public void RuleThatMayGiveNothing_IsNotAString()
    {
        CelPolicyCompileResult<string> result = Compile<string>(Conditional);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Program);
        Assert.Null(result.Expression);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
        Assert.Equal(CelType.OptionalOf(CelType.String), error.ActualType);
    }

    [Theory]
    [InlineData(3L, "b")]
    [InlineData(2L, "c")]
    public static void NestedRule_FallsThroughToTheNextMatchWhenItGivesNothing(long x, string expected)
    {
        CelPolicyCompileResult<string> result = Compile<string>(Chaining);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, Run(result, x));
    }

    [Theory]
    [InlineData(1L, new long[] { 1, 2 })]
    [InlineData(0L, new long[] { 2 })]
    public static void AggregateRule_GivesTheOutputsOfAllMatchesThatHold(long x, long[] expected)
    {
        const string Document = "rule:\n  aggregate:\n    - condition: \"x > 0\"\n      output: \"1\"\n    - output: \"2\"";

        CelPolicyCompileResult<IReadOnlyList<long>> result = Compile<IReadOnlyList<long>>(Document);

        Assert.Equal(expected, Run(result, x));
    }

    [Fact]
    public void Variable_IsUsedByTheLaterExpressionsAndTheOutput()
    {
        const string Document = """
            rule:
              variables:
                - name: a
                  expression: "x + 1"
                - name: b
                  expression: "variables.a * 2"
              match:
                - output: "variables.b"
            """;

        Assert.Equal(4L, Run(Compile<long>(Document), 1));
    }

    [Fact]
    public void VariableThatIsNotUsed_IsNotEvaluated()
    {
        const string Document = """
            rule:
              variables:
                - name: bad
                  expression: "1 / 0"
              match:
                - output: "'ok'"
            """;

        Assert.Equal("ok", Run(Compile<string>(Document), 0));
    }

    [Fact]
    public void ImportedName_IsAnAbbreviationOfTheCompilationOnly()
    {
        const string Document = "imports:\n  - name: acme.limit\nrule:\n  match:\n    - output: \"limit + 1\"";
        CelPolicy policy = Read(Document);
        var bindings = new CelBindings { ["acme.limit"] = 41L };

        CelPolicyCompileResult<long> result = policy.Compile<long>(Env);

        Assert.Equal(42L, result.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.False(Env.Compile<long>("limit + 1").IsSuccess);
    }

    [Fact]
    public void ExplanationPolicy_OutputsTheExplanationsInsteadOfTheOutputs()
    {
        const string Document = """
            rule:
              match:
                - condition: "x > 1"
                  output: "'a'"
                  explanation: "'why a'"
                - output: "'b'"
                  explanation: "'why b'"
            """;
        CelPolicy policy = Read(Document);

        CelPolicy explanations = policy.WithExplanationOutputs();

        Assert.Equal("why a", Run(explanations.Compile<string>(Env), 2));
        Assert.Equal("why b", Run(explanations.Compile<string>(Env), 0));
        Assert.Equal("a", Run(policy.Compile<string>(Env), 2));
        Assert.Equal(policy.Text, explanations.Text);
    }

    [Fact]
    public void ExplanationPolicy_LeavesOutTheMatchesWithoutAnExplanation()
    {
        const string Document = """
            rule:
              match:
                - condition: "x > 1"
                  output: "'a'"
                  explanation: "'why a'"
                - output: "'b'"
            """;

        CelPolicyCompileResult<CelOptional<string>> result = Read(Document).WithExplanationOutputs().Compile<CelOptional<string>>(Env);

        Assert.False(Run(result, 0).HasValue);
        Assert.Equal("why a", Run(result, 2).Value);
    }

    [Fact]
    public void UndeclaredReference_IsAnErrorAtTheNameInTheDocument()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>("rule:\n  match:\n    - output: \"nope\"");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Contains("undeclared reference to 'nope'", error.Message, StringComparison.Ordinal);
        Assert.Equal((3, 16), Where(error));
        Assert.Equal(Source, Location(error).SourceName);
    }

    [Fact]
    public void ConditionThatIsNotABool_IsAnError()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>("rule:\n  match:\n    - condition: \"1\"\n      output: \"'a'\"");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
        Assert.Equal(CelType.Bool, error.ExpectedType);
        Assert.Equal(CelType.Int, error.ActualType);
        Assert.Equal((3, 19), Where(error));
    }

    [Fact]
    public void OutputsOfOtherTypes_AreAnErrorAtTheLaterOutput()
    {
        const string Document = "rule:\n  match:\n    - condition: \"x > 0\"\n      output: \"true\"\n    - output: \"'a'\"";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.IncompatibleOutputTypes, error.Code);
        Assert.Equal("incompatible output types: block has output type string, but previous outputs have type bool", error.Message);
        Assert.Equal(CelType.Bool, error.ExpectedType);
        Assert.Equal(CelType.String, error.ActualType);
        Assert.Equal((5, 16), Where(error));
    }

    [Fact]
    public void UnconditionalMatchThatIsNotTheLast_MakesTheNextOnesUnreachable()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>("rule:\n  match:\n    - output: \"'a'\"\n    - output: \"'b'\"");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UnreachableMatch, error.Code);
        Assert.Equal("match creates unreachable outputs", error.Message);
        Assert.Equal((3, 7), Where(error));
    }

    [Fact]
    public void ConditionThatIsFalse_IsAnError()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>("rule:\n  match:\n    - condition: \"false\"\n      output: \"'a'\"\n    - output: \"'b'\"");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UnreachableMatch, error.Code);
        Assert.Equal("Condition is always false", error.Message);
    }

    [Fact]
    public void VariableOfAnotherTypeThanTheVisibleOne_IsAnErrorAtTheVariable()
    {
        const string Document = """
            rule:
              variables:
                - name: want
                  expression: "1"
                - name: want
                  expression: "'2'"
              match:
                - output: "1"
            """;

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.OverlappingVariable, error.Code);
        Assert.Equal("overlapping identifier for name 'variables.want'", error.Message);
        Assert.Equal("variables.want", error.Subject);
        Assert.Equal((5, 7), Where(error));
    }

    [Fact]
    public void VariableOfTheSameName_ShadowsTheEarlierOne()
    {
        const string Document = """
            rule:
              variables:
                - name: foo
                  expression: "1"
                - name: foo
                  expression: "2"
              match:
                - output: "variables.foo"
            """;

        CelPolicyCompileResult<long> result = Compile<long>(Document);

        Assert.True(result.IsSuccess);
        Assert.Equal(2L, Run(result, 0));
    }

    [Fact]
    public void AggregateRuleInAnAggregateRule_IsAnError()
    {
        const string Document = "rule:\n  aggregate:\n    - rule:\n        aggregate:\n          - output: \"1\"";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal("nested aggregate rules are not allowed", error.Message);
        Assert.Equal((4, 9), Where(error));
    }

    [Fact]
    public void MoreThanAHundredVariables_IsAnErrorAtTheOneBeyondTheLimit()
    {
        var document = new StringBuilder("rule:\n  variables:\n");
        for (int i = 0; i <= 100; i++)
        {
            document.Append(CultureInfo.InvariantCulture, $"    - name: v{i}\n      expression: \"1\"\n");
        }

        document.Append("  match:\n    - output: \"1\"");

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(document.ToString());

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal("variable exceeds nested expression limit", error.Message);
        Assert.Equal((203, 13), Where(error));
    }

    [Fact]
    public void OptionalOutputInAnEnvironmentWithoutTheSet_IsOneErrorAboutTheSet()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Conditional, EnvNoOptional);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.MissingExtensionDependency, error.Code);
        Assert.Equal("optional", error.Subject);
        Assert.Null(error.SourceLocation);
    }

    [Fact]
    public void VariableInAnEnvironmentWithoutBindings_IsOneErrorAboutTheSet()
    {
        const string Document = "rule:\n  variables:\n    - name: a\n      expression: \"x + 1\"\n  match:\n    - output: \"variables.a\"";

        CelPolicyCompileResult<long> result = Compile<long>(Document, EnvNoBindings);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.MissingExtensionDependency, error.Code);
        Assert.Equal("bindings", error.Subject);
    }

    [Fact]
    public void PolicyWithoutVariablesInAnEnvironmentWithoutBindings_Compiles()
    {
        CelPolicyCompileResult<string> result = Compile<string>(FirstMatch, EnvNoBindings);

        Assert.Equal("big", Run(result, 2));
    }

    [Fact]
    public void SyntaxErrorAndTypeError_AreBothReportedInTheOrderOfTheDocument()
    {
        const string Document = "rule:\n  match:\n    - condition: \"x >\"\n      output: \"nope\"";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        Assert.Equal(2, result.Errors.Length);
        Assert.Equal(CelErrorStage.Parse, result.Errors[0].Stage);
        Assert.Equal(3, Where(result.Errors[0]).Line);
        Assert.Equal(CelErrorCode.UndeclaredReference, result.Errors[1].Code);
        Assert.Equal(4, Where(result.Errors[1]).Line);
    }

    [Fact]
    public void ImportThatIsNotAQualifiedName_IsAnErrorAtTheImport()
    {
        const string Document = "imports:\n  - name: \"bad import\"\nrule:\n  match:\n    - output: \"1\"";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidAbbreviation, error.Code);
        Assert.Equal("bad import", error.Subject);
        Assert.Equal((2, 12), Where(error));
    }

    // The name of an import in a block of YAML has the indentation of the block, and a quoted one may have blanks: the environment
    // drops them, as cel-go does, so the second import is valid and the first is reported by its name alone.
    [Fact]
    public void ImportInABlock_IsReportedByItsNameWithoutTheIndentation()
    {
        const string Document = "imports:\n  - name: >\n      punc.Import!\n  - name: \" untrimmed.Import1  \"\nrule:\n  match:\n    - output: \"1\"";

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(Document);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidAbbreviation, error.Code);
        Assert.Equal("punc.Import!", error.Subject);
        Assert.Equal("invalid qualified name: punc.Import!, wanted name of the form 'qualified.name'", error.Message);
    }

    [Fact]
    public void ImportWithBlanksAround_IsAnAbbreviationOfItsName()
    {
        const string Document = "imports:\n  - name: \"  acme.limit \"\nrule:\n  match:\n    - output: \"limit + 1\"";
        var bindings = new CelBindings { ["acme.limit"] = 41L };

        CelPolicyCompileResult<long> result = Read(Document).Compile<long>(Env);

        Assert.Equal(42L, result.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void OptionsWithAPosition_AreRejected()
    {
        CelPolicy policy = Read(FirstMatch);
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("d.yaml", 0, 1, 1) };

        Assert.Throws<ArgumentException>(() => policy.Compile<string>(Env, options));
    }

    [Fact]
    public void EnvironmentThatIsNull_IsRejected()
    {
        CelPolicy policy = Read(FirstMatch);

        Assert.Throws<ArgumentNullException>(() => policy.Compile<string>(null!));
    }

    [Fact]
    public void ExpectedTypeOfTheOptions_IsKept()
    {
        var options = new CelCompileOptions { ExpectedType = CelType.Int };

        CelPolicyCompileResult<CelValue> result = Read(FirstMatch).Compile<CelValue>(Env, options);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
    }

    [Fact]
    public void GetProgramOrThrow_ThrowsAllErrors()
    {
        CelPolicyCompileResult<CelValue> result = Compile<CelValue>("rule:\n  match:\n    - output: \"nope\"");

        CelException exception = Assert.Throws<CelException>(() => result.GetProgramOrThrow());

        Assert.Equal(result.Errors, exception.Errors);
    }
}
