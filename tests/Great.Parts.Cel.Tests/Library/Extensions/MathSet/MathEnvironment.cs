using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Extensions.MathSet;

// The environment of the tests of the sets math and bindings: the standard library with both sets and a few variables, among them
// variables of the type dyn, whose values reach the overloads without the check of static types, and the integers i and j that
// the cost rows read.
internal static class MathEnvironment
{
    internal static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Bindings(1))
        .AddVariable("x", CelType.Dyn)
        .AddVariable("y", CelType.Dyn)
        .AddVariable("i", CelType.Int)
        .AddVariable("j", CelType.Int)
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one. The integers i and j are 1 and 2 unless the
    // bindings set them.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? Default(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // The bindings that the cost rows use.
    internal static CelBindings Default() => new CelBindings().Add("i", 1L).Add("j", 2L);

    // A list of values as a value.
    internal static CelValue ListOf(params CelValue[] items) => CelValue.FromList(CelList.Create(items));
}
