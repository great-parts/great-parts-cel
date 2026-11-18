using Great.Parts.Cel;
using Great.Parts.Cel.Values;

namespace SpeedBench.Adapters;

/// <summary>
/// The type context of Great.Parts.Cel: the source generator of the package describes Order and the types its members
/// reach (Customer, Line) at build time, so a field is read by a direct access, without reflection
/// (docs/type-mapping.md, "Types of a type context"). The names of the fields are the camelCase forms of the properties.
/// </summary>
[CelObject(typeof(Order))]
public partial class BenchTypes : CelTypeContext
{
}

/// <summary>
/// Great.Parts.Cel 1.0.x (nuget.org). Data: objects of the application described by the generated type context, the
/// recommended way for .NET classes; the library reads the instance of <see cref="Order"/> itself, so the evaluation builds
/// no structure from the graph, only the <see cref="CelBindings"/> of the call. Compilation is
/// <c>Compile</c> (parse, check, plan); the full cycle is the one-step <c>Evaluate</c> from the text to the value.
/// The environment has no compilation cache (<see cref="CelCompilationCache"/> is a separate opt-in class).
/// The result type is <see cref="CelValue"/>, the result of any type as it is, with no conversion to .NET.
/// </summary>
public sealed class GreatPartsCelAdapter : ILibraryAdapter
{
    private static readonly CelEnvironment Env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(BenchTypes.Default)
        .AddVariable<Order>("order")
        .AddVariable<DateTimeOffset>("now")
        .Build()
        .GetEnvironmentOrThrow();

    // CelValue is a structure: returning it as object would box it, an allocation that the application does not make when it reads
    // the value. Evaluate keeps the value in this field and returns a marker; Canonical reads the field. The bench is
    // single-threaded and Canonical is called right after the call that made the value.
    private static CelValue _last;

    private static readonly object Marker = new();

    public string Name => "GreatPartsCel";

    public object Compile(string text) => Env.Compile<CelValue>(text).GetProgramOrThrow();

    public object? Evaluate(object compiled, Order order, DateTimeOffset now)
    {
        _last = ((CelProgram<CelValue>)compiled)
            .Evaluate(new CelBindings().Add("order", order).Add("now", now))
            .GetValueOrThrow();
        return Marker;
    }

    public object? FullCycle(string text, Order order, DateTimeOffset now)
    {
        _last = Env.Evaluate<CelValue>(text, new CelBindings().Add("order", order).Add("now", now)).GetValueOrThrow();
        return Marker;
    }

    // An error of CEL is an exception of GetValueOrThrow (CelException); Verification records it.
    public string Canonical(object? result) => Canon.Format(ToNative(_last));

    private static object? ToNative(CelValue value)
    {
        if (value.TryGetBoolean(out bool b))
        {
            return b;
        }

        if (value.TryGetInt64(out long i))
        {
            return i;
        }

        if (value.TryGetUInt64(out ulong u))
        {
            return u;
        }

        if (value.TryGetDouble(out double d))
        {
            return d;
        }

        if (value.TryGetString(out string? s))
        {
            return s;
        }

        if (value.TryGetList(out CelList? list))
        {
            var items = new List<object?>(list.Count);
            for (int k = 0; k < list.Count; k++)
            {
                items.Add(ToNative(list[k]));
            }

            return items;
        }

        return value.ToString();
    }
}
