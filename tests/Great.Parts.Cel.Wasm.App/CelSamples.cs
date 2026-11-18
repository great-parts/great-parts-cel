using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Protobuf;

namespace Great.Parts.Cel.Wasm.App;

/// <summary>
/// The two expressions of the application: the quick start of the README on <c>Great.Parts.Cel</c> and the example of
/// <c>docs/protobuf.md</c> on <c>Great.Parts.Cel.Protobuf</c>.
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
