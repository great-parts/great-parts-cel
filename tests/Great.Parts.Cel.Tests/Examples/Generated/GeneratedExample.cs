using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// What the examples of the types of the generator share. The types and their contexts are those of
// tests/Great.Parts.Cel.Generator.Samples, which the generator describes when the project is built; the examples read them
// through an environment, the way an application does, and so run under the JIT and under Native AOT.
internal static class GeneratedExample
{
    internal const string Samples = "Great.Parts.Cel.Generator.Samples";

    internal static CelEnvironmentBuilder Builder(params CelTypeProvider[] providers)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        foreach (CelTypeProvider provider in providers)
        {
            builder.AddTypes(provider);
        }

        return builder;
    }

    internal static T Evaluate<T>(CelEnvironment environment, string text, CelBindings bindings) =>
        environment.Compile<T>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    // The evaluation fails with one error: its code and the offset of its place are the arguments, the text is checked when given.
    internal static CelError EvaluationError(CelEnvironment environment, string text, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    internal static void AssertError(CelError error, CelErrorCode code, int offset, string? message = null)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(offset, error.Location.Offset);
        if (message is not null)
        {
            Assert.Equal(message, error.Message);
        }
    }

    internal static CelError CompileError(CelEnvironment environment, string text)
    {
        CelCompileResult<CelValue> result = environment.Compile<CelValue>(text);

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    internal static string[] Codes(CelBuildResult result) => [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];
}
