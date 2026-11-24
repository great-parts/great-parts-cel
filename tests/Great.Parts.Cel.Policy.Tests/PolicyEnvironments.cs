using System;
using System.Threading;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Policy.Tests;

// What the tests of the compilation of policies share: the environments, and reading, compiling and evaluating a policy.
internal static class PolicyEnvironments
{
    internal const string Source = "p.yaml";

    // x and y of int, match_id, name and s of string, items and values of list(int) and the dotted variable acme.limit, with the sets
    // optional, strings and bindings.
    internal static CelEnvironment Env { get; } = Builder(withBindings: true)
        .AddExtension(CelExtensions.Optional(2))
        .Build()
        .GetEnvironmentOrThrow();

    // The same without the set optional.
    internal static CelEnvironment EnvNoOptional { get; } = Builder(withBindings: true).Build().GetEnvironmentOrThrow();

    // The same without the set bindings.
    internal static CelEnvironment EnvNoBindings { get; } = Builder(withBindings: false)
        .AddExtension(CelExtensions.Optional(2))
        .Build()
        .GetEnvironmentOrThrow();

    private static CelEnvironmentBuilder Builder(bool withBindings)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddVariable<long>("y")
            .AddVariable<string>("match_id")
            .AddVariable<string>("name")
            .AddVariable<string>("s")
            .AddVariable("items", CelType.ListOf(CelType.Int))
            .AddVariable("values", CelType.ListOf(CelType.Int))
            .AddVariable("acme.limit", CelType.Int)
            .AddExtension(CelExtensions.Strings(5));
        return withBindings ? builder.AddExtension(CelExtensions.Bindings(1)) : builder;
    }

    // The policy of a document that must be one.
    internal static CelPolicy Read(string document) => CelPolicy.Parse(document, Source).GetPolicyOrThrow();

    // The result of compiling a document in an environment, Env by default.
    internal static CelPolicyCompileResult<T> Compile<T>(string document, CelEnvironment? environment = null) =>
        Read(document).Compile<T>(environment ?? Env);

    // The value of a program on the value of x.
    internal static T Run<T>(CelPolicyCompileResult<T> result, long x) =>
        result.GetProgramOrThrow().Evaluate(new CelBindings { ["x"] = x }, cancellationToken: CancellationToken.None).GetValueOrThrow();

    // The expression of a successful result as text that has the same form for the same tree: the names that only the library writes,
    // cel.@block and @index<N>, are given names of the syntax, and the text is parsed and printed again, so that texts that differ in
    // their parentheses and blanks only are equal.
    internal static string Canonical(CelParsedExpr expression) => Canonical(CelSyntax.Format(expression));

    internal static string Canonical(string text)
    {
        string plain = text.Replace("cel.@block", "cel.block_", StringComparison.Ordinal).Replace("@index", "index_", StringComparison.Ordinal);
        return CelSyntax.Format(Env.Parse(plain).GetParsedExprOrThrow());
    }

    // The lines of an expected expression joined by one blank, as the texts of cel-go are written over several lines.
    internal static string Joined(string text) => string.Join(' ', text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    internal static void AssertEqualExpression(string expected, CelParsedExpr actual) =>
        Assert.Equal(Canonical(Joined(expected)), Canonical(actual));
}
