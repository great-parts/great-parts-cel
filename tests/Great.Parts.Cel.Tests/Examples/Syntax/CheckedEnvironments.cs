using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The environments of the examples of the checked expression, the same for the checked expression and for its protobuf form.
internal static class CheckedEnvironments
{
    // The container acme with a variable acme.limit, a function acme.f and the global variable x.
    internal static CelEnvironment Container { get; } = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseContainer("acme")
        .AddVariable<long>("acme.limit")
        .AddVariable<long>("x")
        .AddFunction(new CelFunctionDecl("acme.f", CelOverloadDecl.Global("acme_f_int", [CelType.Int], CelType.Int)))
        .AddFunctionImplementation("acme_f_int", _ => CelValue.FromInt64(1))
        .Build()
        .GetEnvironmentOrThrow();

    // The protobuf messages of the test suite in the container of their package: an enumeration, a message literal.
    internal static CelEnvironment Proto { get; } = ProtobufExample.Builder3().Build().GetEnvironmentOrThrow();
}
