using Great.Parts.Cel;
using Great.Parts.Cel.Values;

namespace SpeedBench.Adapters;

/// <summary>
/// The type context of Great.Parts.Cel: the source generator of the library describes Order and the types its members
/// reach (Customer, Line) at build time, so a field is read by a direct access, without reflection
/// (docs/type-mapping.md, "Types of a type context"). The names of the fields are the camelCase forms of the properties.
/// </summary>
[CelObject(typeof(Order))]
public partial class BenchTypes : CelTypeContext
{
}

/// <summary>
/// The result of the Great.Parts.Cel variants and its canonical text. CelValue is a structure: returning it as object would box it,
/// an allocation that the application does not make when it reads the value. A variant keeps the value in this field and returns a
/// marker; Canonical reads the field. The bench is single-threaded and Canonical is called right after the call that made the value.
/// </summary>
internal static class GreatPartsCelResult
{
    private static CelValue s_last;

    internal static readonly object Marker = new();

    internal static object Keep(CelValue value)
    {
        s_last = value;
        return Marker;
    }

    internal static string Canonical() => Canon.Format(ToNative(s_last));

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

/// <summary>
/// The expressions of the variants that read the fields of the order as variables of the top level: the text of a scenario with
/// the prefix <c>order.</c> removed. <c>has(order.discount)</c> is the one expression that is not a plain removal, because
/// <c>has</c> takes a selection: the presence of the optional field is its comparison with <c>null</c>.
/// </summary>
internal static class TopLevelText
{
    internal static string Of(string expression) =>
        expression.Replace("has(order.discount)", "discount != null", StringComparison.Ordinal)
            .Replace("order.", string.Empty, StringComparison.Ordinal);
}

/// <summary>
/// Great.Parts.Cel from the sources of this repository, the variant with bindings. Data: objects of the application described
/// by the generated type context, the recommended way for .NET classes; the library reads the instance of <see cref="Order"/>
/// itself, so the evaluation builds no structure from the graph, only the <see cref="CelBindings"/> of the call. Compilation is
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

    // The names of the variants are short: BenchmarkDotNet cuts a parameter "name:case" at 20 characters in its reports.
    public string Name => "GreatPartsCel";

    public object Compile(string text) => Env.Compile<CelValue>(text).GetProgramOrThrow();

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) =>
        GreatPartsCelResult.Keep(((CelProgram<CelValue>)compiled)
            .Evaluate(new CelBindings().Add("order", order).Add("now", now))
            .GetValueOrThrow());

    public object? FullCycle(string text, Order order, DateTimeOffset now) =>
        GreatPartsCelResult.Keep(Env.Evaluate<CelValue>(text, new CelBindings().Add("order", order).Add("now", now)).GetValueOrThrow());

    // An error of CEL is an exception of GetValueOrThrow (CelException); Verification records it.
    public string Canonical(object? result) => GreatPartsCelResult.Canonical();
}

/// <summary>
/// Great.Parts.Cel with the input of the environment: <c>UseInput&lt;Order&gt;()</c> makes the fields of the order variables of the top
/// level (<c>total &gt; 1000.0</c>), and the program evaluates on the instance of <see cref="Order"/> with
/// <c>EvaluateInput</c>, without a binding of the order by name; only <c>now</c> is bound. The full cycle is the one-step
/// <c>EvaluateInput</c> of the environment. The expressions are those of the scenarios without the prefix <c>order.</c>.
/// </summary>
public sealed class GreatPartsCelInputAdapter : ILibraryAdapter
{
    private static readonly CelEnvironment Env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(BenchTypes.Default)
        .UseInput<Order>()
        .AddVariable<DateTimeOffset>("now")
        .Build()
        .GetEnvironmentOrThrow();

    public string Name => "GPCel-Input";

    public string Rewrite(string expression) => TopLevelText.Of(expression);

    public object Compile(string text) => Env.Compile<CelValue>(text).GetProgramOrThrow();

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) =>
        GreatPartsCelResult.Keep(((CelProgram<CelValue>)compiled)
            .EvaluateInput(order, new CelBindings().Add("now", now))
            .GetValueOrThrow());

    public object? FullCycle(string text, Order order, DateTimeOffset now) =>
        GreatPartsCelResult.Keep(Env.EvaluateInput<CelValue>(text, order, new CelBindings().Add("now", now)).GetValueOrThrow());

    public string Canonical(object? result) => GreatPartsCelResult.Canonical();
}

/// <summary>
/// The simple entry <c>CelExpression.Evaluate</c>: no environment, the data are an anonymous object whose properties are the
/// variables. The simple entry does not read the classes of the application, so the order is turned into the data it reads on
/// every call, as the libraries that read protobuf messages do: the scalars are properties, the customer is a <see cref="CelMap"/>,
/// the lines are a list of <see cref="CelMap"/> values. The call compiles the text, or finds the program in the hidden cache of
/// the process by the text. There is no separate compilation: <c>Compile</c> returns the text, the rig does not measure it, the
/// evaluation is the call on a text that is already in the cache, and the full cycle is the call on a new text.
/// </summary>
public sealed class GreatPartsCelSimpleAdapter : ILibraryAdapter
{
    public string Name => "GPCel-Simple";

    public bool HasCompile => false;

    public string Rewrite(string expression) => TopLevelText.Of(expression);

    public object Compile(string text) => text;

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) => Run((string)compiled, order, now);

    public object? FullCycle(string text, Order order, DateTimeOffset now) => Run(text, order, now);

    public string Canonical(object? result) => GreatPartsCelResult.Canonical();

    // The anonymous object is written in the call: the source generator registers its type from the static type of the argument.
    private static object Run(string text, Order order, DateTimeOffset now) =>
        GreatPartsCelResult.Keep(CelExpression.Evaluate<CelValue>(
            text,
            new
            {
                id = order.Id,
                status = order.Status,
                total = order.Total,
                currency = order.Currency,
                discount = order.Discount,
                customer = CustomerMap(order.Customer),
                created = order.Created,
                lines = LineList(order.Lines),
                now,
            }));

    private static CelMap CustomerMap(Customer customer) => CelMap.Create(
    [
        Entry("name", CelValue.FromString(customer.Name)),
        Entry("email", CelValue.FromString(customer.Email)),
    ]);

    private static List<CelValue> LineList(IReadOnlyList<Line> lines)
    {
        var items = new List<CelValue>(lines.Count);
        foreach (Line line in lines)
        {
            items.Add(CelValue.FromMap(CelMap.Create(
            [
                Entry("sku", CelValue.FromString(line.Sku)),
                Entry("qty", CelValue.FromInt64(line.Qty)),
                Entry("price", CelValue.FromDouble(line.Price)),
            ])));
        }

        return items;
    }

    private static KeyValuePair<CelValue, CelValue> Entry(string key, CelValue value) => new(CelValue.FromString(key), value);
}
