using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Declarations;

// The order of the calls of the builder changes neither the environment nor what it does (docs/design/tests.md, "Permutations of
// the builder"): the same calls in the model order and in other orders, with repeats, give the same result of Build — success, or
// the same errors with the same texts and places — and, when it succeeds, the same results of a fixed set of texts: compile
// results, values with and without a cost limit, the units charged, and evaluation errors. The sets of the catalog are the test's
// own, since the sets of the library come with their parts; the pool has the conflicts that the property exists for.
public sealed class BuilderOrderTests
{
    private const ulong Seed = 20261112;
    private const int Cases = 300;

    // The texts of every environment: macros of the sets with valid and invalid arguments, calls of their functions with fitting
    // arguments and with others, qualified calls, the identifier of a type, the variables and functions of the model, and the same
    // names under the container.
    private static readonly string[] Texts =
    [
        "strs.succ(1)",
        "strs.succ(1, 2)",
        "strs.fail(x)",
        "other.succ(1)",
        "[1, 2].sortBy(v, v)",
        "[1].sortBy(1, v)",
        "'abc'.reverse()",
        "1.reverse()",
        "shout('a')",
        "shout(1)",
        "whisper('a')",
        "ip('1.2.3.4')",
        "ip(1)",
        "net.parse('1.2.3.4')",
        "net.parse(1)",
        "lst_sum([1, 2])",
        "lst_sum(['a'])",
        "net.IP",
        "type(1) == int",
        "math",
        "ip",
        "x",
        "x + 1",
        "f(1)",
        "f('a')",
        "g(2)",
        "appfn(1)",
        "parse('1.2.3.4')",
        "IP",
        "succ(1)",
    ];

    // The cost limits of the two evaluations of a text: none, and one that stops most calls.
    private static readonly ulong?[] Limits = [null, 3];

    private static readonly string[] SetNames = ["strs", "net", "lst"];

    private static readonly ExtensionCatalog Catalog = new(
    [
        new ExtensionFamily("strs", [1, 2], static version => Strs(version)),
        new ExtensionFamily("net", [1], static version => Net(version)),
        new ExtensionFamily("lst", [1], static version => Lst(version)),
    ]);

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void PermutationGivesSameEnvironment() =>
        PropertyLoop.Check(nameof(PermutationGivesSameEnvironment), OrderModel.Generator, Seed, Cases, Violation, model => model.ToString());

    // The first difference between the results of two orders of the calls, or null when every order agrees with the first.
    private static string? Violation(OrderModel model)
    {
        string[] first = Snapshot(model, model.Orders[0]);
        for (int i = 1; i < model.Orders.Count; i++)
        {
            string[] other = Snapshot(model, model.Orders[i]);
            int line = FirstDifference(first, other);
            if (line >= 0)
            {
                string left = line < first.Length ? first[line] : "<none>";
                string right = line < other.Length ? other[line] : "<none>";
                return $"order {i} differs at line {line}: model order '{left}', this order '{right}'; calls of the model order: "
                    + model.Describe(model.Orders[0]) + "; calls of order " + i + ": " + model.Describe(model.Orders[i]);
            }
        }

        return null;
    }

    private static int FirstDifference(string[] left, string[] right)
    {
        for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            if (i >= left.Length || i >= right.Length || !string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // What an order of the calls gives, as lines: the outcome of Build, then for each text the compile result and the evaluations.
    private static string[] Snapshot(OrderModel model, int[] order)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        foreach (int index in order)
        {
            model.Calls[index].Apply(builder);
        }

        CelBuildResult result = builder.Build();
        if (!result.IsSuccess)
        {
            return [.. result.Errors.Select(ShownBuildError)];
        }

        CelEnvironment environment = result.GetEnvironmentOrThrow();
        CelBindings bindings = Bindings(environment);
        List<string> lines = ["built"];
        foreach (string text in Texts)
        {
            lines.Add(Evaluated(environment, text, bindings));
        }

        return [.. lines];
    }

    // A variable of each declared int and string type, bound to a value of it; the others stay unbound.
    private static CelBindings Bindings(CelEnvironment environment)
    {
        var bindings = new CelBindings();
        foreach (CelVariableDecl variable in environment.VariableDeclarations)
        {
            if (variable.Type.Equals(CelType.Int))
            {
                bindings.Add(variable.Name, 1L);
            }
            else if (variable.Type.Equals(CelType.String))
            {
                bindings.Add(variable.Name, "s");
            }
        }

        return bindings;
    }

    private static string Evaluated(CelEnvironment environment, string text, CelBindings bindings)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(text);
        if (!compiled.IsSuccess)
        {
            return text + " => " + string.Join(" / ", compiled.Errors.Select(ShownError));
        }

        CelProgram<CelValue> program = compiled.GetProgramOrThrow();
        var line = new StringBuilder(text).Append(" : ").Append(program.ResultType);
        foreach (ulong? limit in Limits)
        {
            Plan plan = program.Plan;
            Frame frame = plan.CreateFrame(bindings.Values, limit, CancellationToken.None);
            CelValue value = plan.Run(frame);
            line.Append(CultureInfo.InvariantCulture, $" | {limit?.ToString(CultureInfo.InvariantCulture) ?? "-"}: {Shown(value)} units {frame.ChargedUnits}");
        }

        return line.ToString();
    }

    private static string ShownBuildError(CelError error) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)error.Code} {error.Stage} {error.Message} @{error.Location.Offset} #{error.SourceLocation?.Offset}");

    private static string ShownError(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)error.Code} {error.Stage} {error.Message} @{error.Location.Offset}");

    // The kind and the value of a value; an error by code, message and place.
    private static string Shown(CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long number);
                return "int " + number.ToString(CultureInfo.InvariantCulture);
            case CelValueKind.String:
                _ = value.TryGetString(out string? text);
                return "string " + text;
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool flag);
                return "bool " + flag;
            case CelValueKind.List:
                _ = value.TryGetList(out CelList? list);
                var elements = new List<string>();
                for (int i = 0; i < list!.Count; i++)
                {
                    elements.Add(Shown(list[i]));
                }

                return "[" + string.Join(", ", elements) + "]";
            case CelValueKind.Error:
                _ = value.TryGetError(out CelError? error);
                return "error " + ShownError(error!);
            default:
                return value.Kind.ToString();
        }
    }

    // The sets of the test catalog.
    private static CatalogExtension Strs(uint version)
    {
        string source = "extension strs v" + version.ToString(CultureInfo.InvariantCulture);
        List<OverloadBinding> overloads =
        [
            Receiver(source, "reverse", "strs_reverse", CelType.String, [], CelType.String, static (ReadOnlySpan<CelValue> arguments, in CallSite _) => Text(arguments[0], "reversed")),
            Global(source, "shout", "strs_shout", [CelType.String], CelType.String, static (ReadOnlySpan<CelValue> arguments, in CallSite _) => Text(arguments[0], "shout")),
        ];
        if (version == 2)
        {
            overloads.Add(Global(source, "whisper", "strs_whisper", [CelType.String], CelType.String, static (ReadOnlySpan<CelValue> arguments, in CallSite _) => Text(arguments[0], "whisper")));
        }

        Macro succ = new(
            "succ",
            "strs",
            1,
            isReceiver: true,
            static (_, _) => MacroCheck.Expand,
            static (nodes, _, arguments, offset) => nodes.Call("_+_", null, [arguments[0], nodes.Constant(CelValue.FromInt64(1), offset)], offset));
        Macro fail = new(
            "fail",
            "strs",
            1,
            isReceiver: true,
            static (_, arguments) => MacroCheck.Error("always fails", arguments[0]),
            static (nodes, _, _, offset) => nodes.Constant(CelValue.Null, offset));
        return new CatalogExtension("strs", version, [.. overloads], macros: [succ, fail]);
    }

    private static CatalogExtension Net(uint version)
    {
        string source = "extension net v" + version.ToString(CultureInfo.InvariantCulture);
        return new CatalogExtension(
            "net",
            version,
            [
                Global(source, "ip", "string_to_ip", [CelType.String], CelType.String, static (ReadOnlySpan<CelValue> arguments, in CallSite _) => Text(arguments[0], "ip")),
                Global(source, "net.parse", "net_parse", [CelType.String], CelType.String, static (ReadOnlySpan<CelValue> arguments, in CallSite _) => Text(arguments[0], "parsed")),
            ],
            abstractTypes: [new AbstractTypeDeclaration("net.IP", CelType.Bytes)]);
    }

    private static CatalogExtension Lst(uint version)
    {
        string source = "extension lst v" + version.ToString(CultureInfo.InvariantCulture);
        Macro sortBy = new(
            "sortBy",
            null,
            2,
            isReceiver: true,
            static (_, arguments) => arguments[0] is IdentNode ? MacroCheck.Expand : MacroCheck.Error("argument is not an identifier", arguments[0]),
            static (nodes, target, _, offset) => nodes.Call("size", null, [target!], offset));
        return new CatalogExtension(
            "lst",
            version,
            [
                Global(
                    source,
                    "lst_sum",
                    "lst_sum_list",
                    [CelType.ListOf(CelType.Int)],
                    CelType.Int,
                    static (ReadOnlySpan<CelValue> arguments, in CallSite _) => arguments[0].TryGetList(out CelList? list) ? CelValue.FromInt64(list.Count) : CelValue.Null,
                    shallow: true),
            ],
            macros: [sortBy]);
    }

    private static CelValue Text(CelValue argument, string prefix) =>
        argument.TryGetString(out string? text) ? CelValue.FromString(prefix + ":" + text) : CelValue.Null;

    private static OverloadBinding Global(
        string source,
        string function,
        string id,
        CelType[] parameters,
        CelType result,
        OverloadImplementation implementation,
        bool shallow = false) =>
        new(function, CelOverloadDecl.Global(id, [.. parameters], result), implementation, 1, null, source, shallowArguments: shallow);

    private static OverloadBinding Receiver(
        string source,
        string function,
        string id,
        CelType receiver,
        CelType[] parameters,
        CelType result,
        OverloadImplementation implementation) =>
        new(function, CelOverloadDecl.Receiver(id, receiver, [.. parameters], result), implementation, 1, null, source);

    // One call of the builder of a model: its text for a failure message and what it does to a builder.
    private sealed record Call(string Text, Action<CelEnvironmentBuilder> Apply);

    // A random model: 3 to 12 calls drawn from small pools so that conflicts occur, and the orders in which they are made — the
    // model order, then at least three random permutations, each with up to two repeated calls.
    private sealed class OrderModel
    {
        internal static readonly Gen<OrderModel> Generator = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed)));

        internal List<Call> Calls { get; } = [];

        internal List<int[]> Orders { get; } = [];

        internal string Describe(int[] order) => string.Join(", ", order.Select(index => Calls[index].Text));

        public override string ToString() =>
            "calls: " + Describe([.. Enumerable.Range(0, Calls.Count)]) + "; orders: " + string.Join(" | ", Orders.Select(order => string.Join(' ', order)));

        private static OrderModel Create(PCG random)
        {
            var model = new OrderModel();
            int count = 3 + (int)random.Next(10);
            for (int i = 0; i < count; i++)
            {
                model.AddCalls(random);
            }

            int[] modelOrder = [.. Enumerable.Range(0, model.Calls.Count)];
            model.Orders.Add(modelOrder);
            int others = 3 + (int)random.Next(3);
            for (int i = 0; i < others; i++)
            {
                model.Orders.Add(Repeated(random, Shuffled(random, modelOrder)));
            }

            return model;
        }

        private static int[] Shuffled(PCG random, int[] order)
        {
            int[] shuffled = [.. order];
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int other = (int)random.Next((uint)i + 1);
                (shuffled[i], shuffled[other]) = (shuffled[other], shuffled[i]);
            }

            return shuffled;
        }

        // Up to two calls made again at random places.
        private static int[] Repeated(PCG random, int[] order)
        {
            List<int> repeated = [.. order];
            int repeats = (int)random.Next(3);
            for (int i = 0; i < repeats; i++)
            {
                repeated.Insert((int)random.Next((uint)repeated.Count + 1), order[(int)random.Next((uint)order.Length)]);
            }

            return [.. repeated];
        }

        // One draw: one or two calls.
        private void AddCalls(PCG random)
        {
            uint kind = random.Next(13);
            if (kind < 6 || kind == 12)
            {
                AddExtensionCalls(random, kind);
                return;
            }

            switch (kind)
            {
                case 6:
                    AddApplicationSet(random);
                    break;
                case 7:
                    AddApplicationFunction(random);
                    break;
                case 8:
                    AddDelegateFunction(random);
                    break;
                case 9:
                    AddVariable(random);
                    break;
                case 10:
                    CelTypeProvider provider = CelTypeProvider.Create("types", CelObjectTypeBuilder.ForDictionary("net.IP").Build());
                    Add("AddTypes(net.IP)", builder => builder.AddTypes(provider));
                    break;
                default:
                    string container = random.Next(2) == 0 ? "net" : "strs";
                    Add($"UseContainer({container})", builder => builder.UseContainer(container));
                    break;
            }
        }

        // The calls of the pool that add a set of the catalog or a set of the application whose name is one of the catalog.
        private void AddExtensionCalls(PCG random, uint kind)
        {
            string name = SetNames[(int)random.Next((uint)SetNames.Length)];
            uint version = name == "strs" ? 1 + random.Next(2) : 1;
            switch (kind)
            {
                case 0:
                    Add($"AddExtension({name} {version})", builder => builder.AddExtension(Catalog.Get(name, version)));
                    break;
                case 1:
                    Add($"AddExtension({name} 9)", builder => builder.AddExtension(Catalog.Get(name, 9)));
                    break;
                case 2:
                    Add("AddExtension(strs 1)", builder => builder.AddExtension(Catalog.Get("strs", 1)));
                    Add("AddExtension(strs 2)", builder => builder.AddExtension(Catalog.Get("strs", 2)));
                    break;
                case 3:
                    Add($"AddExtension({name} {version})", builder => builder.AddExtension(Catalog.Get(name, version)));
                    Add($"AddExtension({name} {version}) again", builder => builder.AddExtension(Catalog.Get(name, version)));
                    break;
                case 4:
                    CelExtension same = CelExtension.Create("app" + random.Next(2), 1, [], new Dictionary<string, CelFunctionImplementation>());
                    Add($"AddExtension({same.Name} application)", builder => builder.AddExtension(same));
                    Add($"AddExtension({same.Name} application) same instance", builder => builder.AddExtension(same));
                    break;
                case 5:
                    CelExtension named = CelExtension.Create(name, version, [], new Dictionary<string, CelFunctionImplementation>());
                    Add($"AddExtension(application {name} {version})", builder => builder.AddExtension(named));
                    break;
                default:
                    CelExtension lstOrNet = Catalog.Get(random.Next(2) == 0 ? "lst" : "net", 1);
                    Add($"AddExtension({lstOrNet.Name} 1)", builder => builder.AddExtension(lstOrNet));
                    break;
            }
        }

        // An application set with dependencies drawn from the names of the pool, some of which are not in the model.
        private void AddApplicationSet(PCG random)
        {
            string[] pool = ["strs", "net", "lst", "zzz"];
            List<string> dependencies = [];
            int needed = (int)random.Next(3);
            for (int i = 0; i < needed; i++)
            {
                dependencies.Add(pool[(int)random.Next((uint)pool.Length)]);
            }

            string name = "app" + random.Next(3);
            uint version = 1 + random.Next(2);
            CelExtension application = CelExtension.Create(name, version, [], new Dictionary<string, CelFunctionImplementation>(), [.. dependencies]);
            Add($"AddExtension(application {name} {version} needs {string.Join('+', dependencies)})", builder => builder.AddExtension(application));
        }

        // An application set with a function that overlaps one of a catalog set, takes the identifier of one, has the form of a
        // macro, has a name that is not an identifier, or conflicts with nothing.
        private void AddApplicationFunction(PCG random)
        {
            (string Function, string Id, CelOverloadDecl Overload, CelFunctionImplementation Implementation)[] pool =
            [
                ("reverse", "app_reverse", CelOverloadDecl.Receiver("app_reverse", CelType.String, [], CelType.String), static _ => CelValue.FromString("r")),
                ("ip", "app_ip", CelOverloadDecl.Global("app_ip", [CelType.String], CelType.String), static _ => CelValue.FromString("i")),
                ("ipx", "string_to_ip", CelOverloadDecl.Global("string_to_ip", [CelType.Int], CelType.String), static _ => CelValue.FromString("i")),
                ("sortBy", "app_sort_by", CelOverloadDecl.Receiver("app_sort_by", CelType.ListOf(CelType.Int), [CelType.String, CelType.Int], CelType.Int), static _ => CelValue.FromInt64(1)),
                ("all", "app_all3", CelOverloadDecl.Receiver("app_all3", CelType.ListOf(CelType.Int), [CelType.String, CelType.String, CelType.Bool], CelType.Bool), static _ => CelValue.FromBoolean(true)),
                ("a b", "app_bad", CelOverloadDecl.Global("app_bad", [CelType.Int], CelType.Int), static _ => CelValue.FromInt64(1)),
                ("appfn", "app_appfn", CelOverloadDecl.Global("app_appfn", [CelType.Int], CelType.Int), static _ => CelValue.FromInt64(1)),
            ];
            var (function, id, overload, implementation) = pool[(int)random.Next((uint)pool.Length)];
            string name = "app" + random.Next(3);
            CelExtension application = CelExtension.Create(
                name,
                1,
                [new CelFunctionDecl(function, overload)],
                new Dictionary<string, CelFunctionImplementation> { [id] = implementation });
            Add($"AddExtension(application {name} 1 with {function} {id})", builder => builder.AddExtension(application));
        }

        // A function with a declaration and its implementation, or the delegate form.
        private void AddDelegateFunction(PCG random)
        {
            if (random.Next(2) == 0)
            {
                CelFunctionDecl declaration = new("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int));
                CelFunctionImplementation implementation = static _ => CelValue.FromInt64(7);
                Add("AddFunction(f int)", builder => builder.AddFunction(declaration));
                Add("AddFunctionImplementation(f_int)", builder => builder.AddFunctionImplementation("f_int", implementation));
            }
            else
            {
                Func<long, long> delegateForm = static value => value + 1;
                Add("AddFunction(g delegate)", builder => builder.AddFunction("g", delegateForm));
            }
        }

        private void AddVariable(PCG random)
        {
            string[] names = ["math", "ip", "net.IP", "x"];
            CelType[] types = [CelType.Int, CelType.String, CelType.ObjectNamed("net.IP")];
            string name = names[(int)random.Next((uint)names.Length)];
            CelType type = types[(int)random.Next((uint)types.Length)];
            Add($"AddVariable({name} {type})", builder => builder.AddVariable(name, type));
        }

        private void Add(string text, Action<CelEnvironmentBuilder> apply) => Calls.Add(new Call(text, apply));
    }
}
