using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using PolicyDocument = Great.Parts.Cel.Policy.CelPolicy;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>
/// A category prepared for its tests: the environment of its configuration with the sets <c>optional</c> and <c>bindings</c>, and the policy
/// compiled in it, once. A failure of the environment, of the parse or of the compilation is kept as text for the tests.
/// </summary>
internal sealed class PreparedCategory
{
    /// <summary>The name of the policy document in the positions of its errors.</summary>
    internal const string PolicySource = "policy.yaml";

    private PreparedCategory(CelEnvironment? environment, string? environmentProblem, CelProgram<CelValue>? program, IReadOnlyList<string> compileErrors)
    {
        Environment = environment;
        EnvironmentProblem = environmentProblem;
        Program = program;
        CompileErrors = compileErrors;
    }

    /// <summary>The environment, or <see langword="null"/> when it could not be built.</summary>
    internal CelEnvironment? Environment { get; }

    /// <summary>Why the environment could not be built, or <see langword="null"/>.</summary>
    internal string? EnvironmentProblem { get; }

    /// <summary>The program of the policy, or <see langword="null"/> when the policy does not compile.</summary>
    internal CelProgram<CelValue>? Program { get; }

    /// <summary>The messages of the errors of the parse or of the compilation; empty when the policy compiles.</summary>
    internal IReadOnlyList<string> CompileErrors { get; }

    /// <summary>Builds the environment of the category and compiles its policy.</summary>
    /// <param name="category">The category.</param>
    internal static PreparedCategory Prepare(PolicyCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        CelEnvironment environment;
        try
        {
            CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
                .UseTimeZones(ZoneInfoFile.Database())
                .AddTypes(ConformanceMessages.Provider);
            if (category.Config is { } config)
            {
                ImmutableArray<CelError> read = CelEnvironmentJson.Apply(builder, PolicyConfig.ToJson(config));
                if (read.Length > 0)
                {
                    return Failed("the configuration is not read: " + Messages(read));
                }

                foreach (string id in PolicyConfig.OverloadIds(config))
                {
                    PolicyFunctions.Register(builder, id);
                }
            }

            builder.AddExtension(CelExtensions.Optional(2)).AddExtension(CelExtensions.Bindings(1));
            CelBuildResult built = builder.Build();
            if (!built.IsSuccess)
            {
                return Failed("the environment is not built: " + Messages(built.Errors));
            }

            environment = built.Environment;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Failed($"the environment threw {exception.GetType().Name}: {exception.Message}");
        }

        ParsedPolicy parsed = Parse(category.PolicyText);
        if (parsed.Errors.Count > 0)
        {
            return new PreparedCategory(environment, null, null, parsed.Errors);
        }

        Policy.CelPolicyCompileResult<CelValue> compiled = parsed.Policy!.Compile<CelValue>(environment);
        return compiled.IsSuccess
            ? new PreparedCategory(environment, null, compiled.Program, [])
            : new PreparedCategory(environment, null, null, [.. compiled.Errors.Select(error => error.Message)]);
    }

    private static PreparedCategory Failed(string problem) => new(null, problem, null, []);

    private static ParsedPolicy Parse(string text)
    {
        Policy.CelPolicyParseResult parsed = PolicyDocument.Parse(text, PolicySource);
        return parsed.IsSuccess
            ? new ParsedPolicy(parsed.Policy, [])
            : new ParsedPolicy(null, [.. parsed.Errors.Select(error => error.Message)]);
    }

    private static string Messages(ImmutableArray<CelError> errors) => string.Join("; ", errors.Select(error => error.Message));

    private sealed record ParsedPolicy(PolicyDocument? Policy, IReadOnlyList<string> Errors);
}

/// <summary>
/// Judges a test of a category: the policy of the category compiled, the inputs evaluated and bound, the program evaluated, and the result
/// compared with the expected output as the runner of cel-go compares it, or, for <c>error_set</c>, a failed compilation of which every
/// string of the set is a substring of the message of some error.
/// </summary>
internal static class PolicyJudge
{
    /// <summary>Judges a test.</summary>
    /// <param name="category">The prepared category of the test.</param>
    /// <param name="test">The test.</param>
    internal static Judgement Judge(PreparedCategory category, PolicyTest test)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(test);
        try
        {
            return JudgeTest(category, test);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Judgement.Fail($"the test threw {exception.GetType().Name}: {ExprText.Quote(exception.Message)}");
        }
    }

    private static Judgement JudgeTest(PreparedCategory category, PolicyTest test)
    {
        if (category.Environment is null)
        {
            return Judgement.Fail(category.EnvironmentProblem ?? "the environment is missing");
        }

        if (test.Output.Kind == PolicyOutputKind.ErrorSet)
        {
            return ErrorSet(category, test.Output.Errors);
        }

        if (category.Program is null)
        {
            return Judgement.Fail("the policy does not compile: " + string.Join("; ", category.CompileErrors.Select(ExprText.Quote)));
        }

        if (test.Output.Kind == PolicyOutputKind.Other)
        {
            return Judgement.Fail($"the output '{test.Output.Text}' is not supported");
        }

        if (test.ContextExpression is not null)
        {
            return Judgement.Fail("context_expr needs the fields of a message as variables; the library has no such input");
        }

        CelBindings bindings = new();
        foreach ((string name, PolicyInput input) in test.Inputs)
        {
            bindings.Add(name, input.Expression is { } expression ? Evaluate(category.Environment, expression) : ValueConversion.ToCelValue(PolicyValues.ToValue(input.Value!)));
        }

        CelEvalResult<CelValue> result = category.Program.Evaluate(bindings, null, CancellationToken.None);
        CelValue expected = test.Output.Kind == PolicyOutputKind.Expression
            ? Evaluate(category.Environment, test.Output.Text)
            : ValueConversion.ToCelValue(PolicyValues.ToValue(test.Output.Value!));
        if (!result.IsSuccess)
        {
            return Judgement.Fail($"expected {expected}, got the evaluation error {string.Join("; ", result.Errors.Select(error => ExprText.Quote(error.Message)))}");
        }

        return Matches(result.Value, expected) ? Judgement.Pass : Judgement.Fail($"expected {expected}, got {result.Value}");
    }

    private static Judgement ErrorSet(PreparedCategory category, IReadOnlyList<string> wanted)
    {
        if (category.CompileErrors.Count == 0)
        {
            return Judgement.Fail("the policy compiles; the test expects a compilation error");
        }

        string? missing = wanted.FirstOrDefault(text => !category.CompileErrors.Any(message => message.Contains(text, StringComparison.Ordinal)));
        return missing is null
            ? Judgement.Pass
            : Judgement.Fail($"no error contains {ExprText.Quote(missing)}; the errors are {string.Join("; ", category.CompileErrors.Select(ExprText.Quote))}");
    }

    // The value of an expression of a test in the environment of the category: an input or an expected output.
    private static CelValue Evaluate(CelEnvironment environment, string expression)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            throw new InvalidOperationException($"the expression {ExprText.Quote(expression)} does not compile: {string.Join("; ", compiled.Errors.Select(error => error.Message))}");
        }

        CelEvalResult<CelValue> result = compiled.Program.Evaluate(new CelBindings(), null, CancellationToken.None);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"the expression {ExprText.Quote(expression)} fails: {string.Join("; ", result.Errors.Select(error => error.Message))}");
    }

    // The celtest rule: the value equals the expected one by the equality of CEL, or it is an optional that holds a value equal to it.
    private static bool Matches(CelValue actual, CelValue expected)
    {
        if (IsTrue(CelValue.CelEquals(actual, expected)) || actual.Equals(expected))
        {
            return true;
        }

        return actual.TryGetOptional(out CelOptional<CelValue> held) && held.HasValue && IsTrue(CelValue.CelEquals(held.Value, expected));
    }

    private static bool IsTrue(CelValue value) => value.TryGetBoolean(out bool result) && result;
}
