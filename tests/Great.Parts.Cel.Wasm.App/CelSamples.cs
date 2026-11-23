using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Protobuf;

namespace Great.Parts.Cel.Wasm.App;

/// <summary>
/// The expressions of the application: the quick start of the README on <c>Great.Parts.Cel</c>, the example of
/// <c>docs/protobuf.md</c> on <c>Great.Parts.Cel.Protobuf</c>, and an asynchronous function with a partial evaluation, so that the
/// trimmer analyzes the asynchronous driver and the residual expression as well.
/// </summary>
internal static class CelSamples
{
    internal static long Arithmetic()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("(x + 2) / 3").GetProgramOrThrow();
        return program.Evaluate(new CelBindings().Add("x", 40L)).GetValueOrThrow();
    }

    // A function of the application that waits, called twice in a chain: the driver of asynchronous evaluation on the one thread.
    internal static async Task<long> AsynchronousCallAsync()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddAsyncFunction("inc", async (long value, CancellationToken token) =>
            {
                await Task.Yield();
                return value + 1;
            })
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("inc(inc(x))").GetProgramOrThrow();
        CelEvalResult<long> result = await program.EvaluateAsync(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None).ConfigureAwait(false);
        return result.GetValueOrThrow();
    }

    // Partial evaluation with an unknown variable, and the residual expression: the known part is replaced by its value.
    internal static string Residual()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddVariable<long>("y")
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<bool> program = environment.Compile<bool>("x + 1 > y", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();
        CelPartialEvalResult<bool> result = program.EvaluatePartial(new CelBindings().Add("x", 1L), [new CelAttributePattern("y")]);
        return CelSyntax.Format(result.GetResidual());
    }

    internal static string Message()
    {
        CelProtobufTypes types = new CelProtobufTypesBuilder("api")
            .AddFile(ApiReflection.Descriptor)
            .Build();
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(types)
            .UseContainer("google.protobuf")
            .AddVariable<Api>("api")
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<string> label = environment.Compile<string>("api.name + ' v' + api.version").GetProgramOrThrow();
        Api value = new() { Name = "orders", Version = "2" };
        return label.Evaluate(new CelBindings().Add("api", value)).GetValueOrThrow();
    }
}
