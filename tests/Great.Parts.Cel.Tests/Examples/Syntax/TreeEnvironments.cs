using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The environments of the examples of the public syntax tree: x, a, b and c of int, items of list(int), name and s of string,
// d of dyn and m of map(string, dyn), with the sets optional and bindings. EnvNoOptional lacks the set optional; EnvMath
// adds the set math.
internal static class TreeEnvironments
{
    internal static CelEnvironment Env { get; } = Builder().AddExtension(CelExtensions.Optional(2)).Build().GetEnvironmentOrThrow();

    internal static CelEnvironment EnvNoOptional { get; } = Builder().Build().GetEnvironmentOrThrow();

    internal static CelEnvironment EnvMath { get; } = Builder()
        .AddExtension(CelExtensions.Optional(2))
        .AddExtension(CelExtensions.Math(3))
        .Build()
        .GetEnvironmentOrThrow();

    private static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable<long>("a")
        .AddVariable<long>("b")
        .AddVariable<long>("c")
        .AddVariable("items", CelType.ListOf(CelType.Int))
        .AddVariable<string>("name")
        .AddVariable<string>("s")
        .AddVariable("d", CelType.Dyn)
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddExtension(CelExtensions.Bindings(1));
}
