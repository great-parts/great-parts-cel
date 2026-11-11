using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The environment of the tests of format: the standard library with the strings set and the network set, whose abstract types are values
// that %s does not take, and variables that the format string and the list of arguments can come from, so that a call is not
// checked at compilation and evaluation meets every kind of value.
internal static class FormatEnvironment
{
    // The static types that the check at compilation tells apart, one variable of each: the name says the type.
    internal static readonly CelEnvironment Setup = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Network(1))
        .AddVariable("f", CelType.String)
        .AddVariable("items", CelType.ListOf(CelType.Dyn))
        .AddVariable("v", CelType.Dyn)
        .AddVariable("m", CelType.Dyn)
        .AddVariable("v_null", CelType.Null)
        .AddVariable("v_bool", CelType.Bool)
        .AddVariable("v_int", CelType.Int)
        .AddVariable("v_uint", CelType.UInt)
        .AddVariable("v_double", CelType.Double)
        .AddVariable("v_string", CelType.String)
        .AddVariable("v_bytes", CelType.Bytes)
        .AddVariable("v_list", CelType.ListOf(CelType.Int))
        .AddVariable("v_map", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("v_type", CelType.TypeOf(CelType.Int))
        .AddVariable("v_timestamp", CelType.Timestamp)
        .AddVariable("v_duration", CelType.Duration)
        .AddVariable("v_dyn", CelType.Dyn)
        .AddVariable("v_wrapped_int", CelType.WrapperOf(CelType.Int))
        .AddVariable("v_wrapped_string", CelType.WrapperOf(CelType.String))
        .AddVariable("v_wrapped_double", CelType.WrapperOf(CelType.Double))
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Setup.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? new CelBindings(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // The bindings of a format string and the values that fill its clauses.
    internal static CelBindings Bindings(string format, params CelValue[] values) =>
        new CelBindings().Add("f", format).Add("items", CelValue.FromList(CelList.Create(values)));

    // The result of the format string on the values, which has to be a string.
    internal static string Format(string format, params CelValue[] values)
    {
        CelEvalResult<CelValue> result = Evaluate("f.format(items)", Bindings(format, values));
        return result.GetValueOrThrow().TryGetString(out string? text)
            ? text
            : throw new System.InvalidOperationException("The result is not a string: " + result.GetValueOrThrow().Kind);
    }
}
