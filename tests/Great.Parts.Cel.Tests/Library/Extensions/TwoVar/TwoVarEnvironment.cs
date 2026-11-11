using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Extensions.TwoVar;

// The environment of the tests of the set of two-variable comprehensions: the standard library with the set and a few variables —
// the integers i and j, a list of ints, a map of ints by strings, a list of 20 ints, and variables of the type dyn, whose values
// reach the overloads without the check of static types.
internal static class TwoVarEnvironment
{
    internal static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddVariable("i", CelType.Int)
        .AddVariable("j", CelType.Int)
        .AddVariable("ints", CelType.ListOf(CelType.Int))
        .AddVariable("counts", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("twenty", CelType.ListOf(CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // The same environment without the set, to show what the macros of three arguments are without it.
    internal static readonly CelEnvironment WithoutTheSet = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("ints", CelType.ListOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? Default(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // The bindings of the tests: i is 1 and j is 2, ints is [10, 20, 30], counts is {'a': 1, 'b': 2}, twenty is 1 to 20.
    internal static CelBindings Default()
    {
        CelValue[] ten = new CelValue[20];
        for (int index = 0; index < ten.Length; index++)
        {
            ten[index] = CelValue.FromInt64(index + 1);
        }

        return new CelBindings()
            .Add("i", 1L)
            .Add("j", 2L)
            .Add("ints", ListOf(CelValue.FromInt64(10), CelValue.FromInt64(20), CelValue.FromInt64(30)))
            .Add("counts", CelValue.FromMap(CelMap.Create(
                [
                    new(CelValue.FromString("a"), CelValue.FromInt64(1)),
                    new(CelValue.FromString("b"), CelValue.FromInt64(2)),
                ])))
            .Add("twenty", ListOf(ten));
    }

    // A list of values as a value.
    internal static CelValue ListOf(params CelValue[] items) => CelValue.FromList(CelList.Create(items));
}
