using Cel;
using Cel.Checker;
using Cel.Common.Types;
using Cel.Common.Types.Pb;
using Cel.Common.Types.Ref;
using Cel.Common.Types.Traits;
using Google.Protobuf.WellKnownTypes;

namespace SpeedBench.Adapters;

/// <summary>
/// Cel.NET 2.3.1. Data: protobuf messages, "supported out of the box" (README); the JSON
/// registry for plain objects cannot describe DateTimeOffset and IReadOnlyList&lt;T&gt; of Model.cs, so the whole
/// order graph is translated into Proto/order.proto messages on every evaluation, and now into a protobuf
/// Timestamp. Compilation is what ScriptHost.BuildScript(...).Build() does after creating its environment:
/// Parse, Check, Program with OptOptimize; the environment (standard library, declarations, the Order type)
/// is built once per process. Cel.NET has no compilation cache.
/// </summary>
public sealed class CelNetAdapter : ILibraryAdapter
{
    private static readonly Env Env = Env.NewCustomEnv(
        ProtoTypeRegistry.NewRegistry(),
        [
            LibraryOptions.StdLib(),
            EnvOptions.Declarations(
                Decls.NewVar("order", Decls.NewObjectType(Proto.Order.Descriptor.FullName)),
                Decls.NewVar("now", Decls.Timestamp)),
            EnvOptions.Types(new Proto.Order()),
        ]);

    public string Name => "Cel.NET";

    public object Compile(string text)
    {
        var parsed = Env.Parse(text);
        if (parsed.HasIssues())
        {
            throw new InvalidOperationException("parse: " + parsed.Issues);
        }

        var checkedAst = Env.Check(parsed.Ast!);
        if (checkedAst.HasIssues())
        {
            throw new InvalidOperationException("check: " + checkedAst.Issues);
        }

        return Env.Program(checkedAst.Ast!, ProgramOptions.EvalOptions(EvalOption.OptOptimize));
    }

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) =>
        ((IProgram)compiled).Eval(new Dictionary<string, object>
        {
            ["order"] = ProtoData.From(order),
            ["now"] = Timestamp.FromDateTimeOffset(now),
        }).Val;

    public object? FullCycle(string text, Order order, DateTimeOffset now) => Evaluate(Compile(text), order, now);

    public string Canonical(object? result) =>
        result is Err e ? "ERROR(" + e + ")" : Canon.Format(ToNative((IVal)result!));

    // Lists are converted element by element: Value() of some Cel.NET list kinds is not implemented.
    private static object? ToNative(IVal value)
    {
        if (value is ILister list)
        {
            var size = list.Size().IntValue();
            var items = new List<object?>((int)size);
            for (var i = 0L; i < size; i++)
            {
                items.Add(ToNative(list.Get(IntT.IntOf(i))));
            }

            return items;
        }

        return value.Value();
    }
}
