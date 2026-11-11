using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Extensions.Network;

// The environment of the tests of the set network: the standard library with the set and three variables, a string and two of the
// type dyn, whose values reach the overloads without the check of static types.
internal static class NetworkEnvironment
{
    internal static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Network(1))
        .AddVariable("text", CelType.String)
        .AddVariable("x", CelType.Dyn)
        .AddVariable("y", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? new CelBindings(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // The value of an expression that has to succeed.
    internal static CelValue ValueOf(string expression, CelBindings? bindings = null) => Evaluate(expression, bindings).GetValueOrThrow();

    // Whether an expression that has to succeed is the bool true.
    internal static bool IsTrue(string expression, CelBindings? bindings = null) =>
        ValueOf(expression, bindings).TryGetBoolean(out bool value) && value;
}
