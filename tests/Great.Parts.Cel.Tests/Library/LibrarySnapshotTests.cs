using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The overloads of version 1 against the file that records them: the function, the identifier, the call style, the parameter and
// result types, whether the overload is a candidate only with a dyn argument, and what a call charges on probe arguments. A later
// change of an identifier, a signature or a cost shows as a line that differs.
public sealed class LibrarySnapshotTests
{
    // The sizes of the probe arguments of the kinds string, bytes, list and map, which the five costs of a line are for.
    private static readonly int[] Sizes = [0, 1, 10, 11, 100];

    private static readonly LineTable Lines = new("x");

    [Fact]
    public static void StandardLibraryV1()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Library", "Snapshots", "standard-library-v1.txt");
        string[] recorded = [.. File.ReadAllLines(path).Where(line => line.Length > 0 && line[0] != '#')];
        string[] actual = [.. Great.Parts.Cel.Library.StandardLibraryV1.Overloads.Select(Line)];

        var differences = new List<string>();
        Dictionary<string, string> recordedById = recorded.ToDictionary(Id, StringComparer.Ordinal);
        Dictionary<string, string> actualById = actual.ToDictionary(Id, StringComparer.Ordinal);
        foreach (string line in recorded.Where(line => !actualById.ContainsKey(Id(line))))
        {
            differences.Add("a line without an overload: " + line);
        }

        foreach (string line in actual)
        {
            if (!recordedById.TryGetValue(Id(line), out string? snapshot))
            {
                differences.Add("an overload without a line: " + line);
            }
            else if (!string.Equals(snapshot, line, StringComparison.Ordinal))
            {
                differences.Add("snapshot: " + snapshot + "\n  table:    " + line);
            }
        }

        if (differences.Count == 0 && !recorded.SequenceEqual(actual, StringComparer.Ordinal))
        {
            differences.Add("the lines are not in the order of the table");
        }

        Assert.True(differences.Count == 0, "The snapshot differs from the table of version 1:\n" + string.Join("\n", differences));
    }

// ---- The extension sets ----

    [Fact]
    public static void StringsV5() => SetSnapshot("strings", 5);

    [Fact]
    public static void MathV3() => SetSnapshot("math", 3);

    [Fact]
    public static void ListsV4() => SetSnapshot("lists", 4);

    [Fact]
    public static void SetsV0() => SetSnapshot("sets", 0);

    [Fact]
    public static void EncodersV1() => SetSnapshot("encoders", 1);

    [Fact]
    public static void BindingsV1() => SetSnapshot("bindings", 1);

    [Fact]
    public static void TwoVarComprehensionsV0() => SetSnapshot("two-var-comprehensions", 0);

    [Fact]
    public static void NetworkV1() => SetSnapshot("network", 1);

    [Fact]
    public static void ProtosV0() => SetSnapshot("protos", 0);

    // The declarations of a version of a set against the file that records them (docs/design/tests.md, "Snapshots of the extension
    // sets"): the lines that differ are named with both texts, in the order of the table of the set.
    private static void SetSnapshot(string name, uint version)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Library", "Snapshots", name + "-v" + version.ToString(CultureInfo.InvariantCulture) + ".txt");
        string[] recorded = [.. File.ReadAllLines(path).Where(line => line.Length > 0 && line[0] != '#')];
        string[] actual = SetLines(name, version);

        var differences = new List<string>();
        for (int i = 0; i < Math.Max(recorded.Length, actual.Length); i++)
        {
            string snapshot = i < recorded.Length ? recorded[i] : "(no line)";
            string table = i < actual.Length ? actual[i] : "(no declaration)";
            if (!string.Equals(snapshot, table, StringComparison.Ordinal))
            {
                differences.Add("line " + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n  snapshot: " + snapshot + "\n  table:    " + table);
            }
        }

        Assert.True(differences.Count == 0, "The snapshot of " + name + " " + version.ToString(CultureInfo.InvariantCulture) + " differs from the table:\n" + string.Join("\n", differences));
    }

    // The text of a version: a header, the dependencies, the abstract types, the macros, then one line for each overload.
    private static string[] SetLines(string name, uint version)
    {
        ExtensionDefinition definition = Great.Parts.Cel.Library.Extensions.ExtensionCatalog.Default.Get(name, version).Definition
            ?? throw new InvalidOperationException("the set " + name + " " + version.ToString(CultureInfo.InvariantCulture) + " has no definition");
        return
        [
            "set " + name + " " + version.ToString(CultureInfo.InvariantCulture),
            .. definition.Dependencies.Select(dependency => "depends " + dependency),
            .. definition.AbstractTypes.Select(type => "type " + type.Name),
            .. definition.MacroForms.Select(MacroLine),
            .. definition.Overloads.Select(SetOverloadLine),
        ];
    }

    private static string MacroLine(MacroForm form) =>
        "macro " + (form.Namespace is null ? string.Empty : form.Namespace + ".") + form.Name + " " + (form.IsReceiver ? "receiver" : "global") + " "
        + (form.ArgumentCount == MacroForm.AnyArgumentCount ? "*" : form.ArgumentCount.ToString(CultureInfo.InvariantCulture));

    private static string SetOverloadLine(OverloadBinding overload)
    {
        CelOverloadDecl declaration = overload.Declaration;
        string costs = string.Join(" ", Sizes.Select(size => SetCost(overload, size).ToString(CultureInfo.InvariantCulture)));
        return "overload " + overload.Function + " " + declaration.OverloadId + " " + (declaration.IsReceiverStyle ? "receiver" : "global")
            + " (" + string.Join(", ", declaration.ParameterTypes) + ") -> " + declaration.ResultType + (overload.DynOnly ? " dyn" : string.Empty) + " cost " + costs;
    }

    // What a call charges before it runs: the fixed units and the cost of the arguments. What an implementation charges while it
    // runs is in the rows of the cost tables and not in the snapshot.
    private static ulong SetCost(OverloadBinding overload, int size)
    {
        CelValue[] arguments = [.. overload.Declaration.ParameterTypes.Select(type => SetProbe(type, size))];
        return overload.FixedCost + (overload.ArgumentCost?.Invoke(arguments) ?? 0);
    }

    // A string or bytes of the size, a list of that many ones, a map of that many entries, an int equal to the size, a double of 1,
    // a net.IP or net.CIDR of a fixed value, and for every other kind one fixed value.
    private static CelValue SetProbe(CelType type, int size) => type.Kind switch
    {
        CelTypeKind.Int => CelValue.FromInt64(size),
        CelTypeKind.Double => CelValue.FromDouble(1),
        CelTypeKind.Abstract when type.Name == Great.Parts.Cel.Library.Extensions.Network.NetworkTypes.IpName =>
            Great.Parts.Cel.Library.Extensions.Network.NetworkFunctions.StringToIp([CelValue.FromString("10.0.0.1")], NetworkSite()),
        CelTypeKind.Abstract =>
            Great.Parts.Cel.Library.Extensions.Network.NetworkFunctions.StringToCidr([CelValue.FromString("10.0.0.0/8")], NetworkSite()),
        _ => Probe(type, size),
    };

    private static CallSite NetworkSite() => new("probe", 0, Lines, new RecordingMeter());

    // The overload identifier is the second cell of a line.
    private static string Id(string line) => line.Split(" | ")[1];

    private static string Line(OverloadBinding overload)
    {
        CelOverloadDecl declaration = overload.Declaration;
        string costs = string.Join(" ", Sizes.Select(size => Cost(overload, size).ToString(CultureInfo.InvariantCulture)));
        return string.Join(
            " | ",
            overload.Function,
            declaration.OverloadId,
            declaration.IsReceiverStyle ? "receiver" : "global",
            "(" + string.Join(", ", declaration.ParameterTypes) + ") -> " + declaration.ResultType,
            overload.DynOnly ? "dyn" : "-",
            costs);
    }

    // What a call charges: the fixed units and the cost of the arguments, which the call node charges, and what the implementation
    // charges through the call site.
    private static ulong Cost(OverloadBinding overload, int size)
    {
        CelValue[] arguments = [.. overload.Declaration.ParameterTypes.Select(type => Probe(type, size))];
        var meter = new RecordingMeter();
        if (overload.Implementation is { } implementation)
        {
            _ = implementation(arguments, new CallSite(overload.Function, 0, Lines, meter));
        }

        return overload.FixedCost + (overload.ArgumentCost?.Invoke(arguments) ?? 0) + meter.Total;
    }

    // A string or bytes of the size, a list of that many ones, a map of that many entries with the keys 0 to size - 1, every other
    // kind one fixed value (the moment 1970-01-01T00:00:00Z and the duration 1s for time); a type parameter takes an int.
    private static CelValue Probe(CelType type, int size) => type.Kind switch
    {
        CelTypeKind.Bool => CelValue.FromBoolean(true),
        CelTypeKind.UInt => CelValue.FromUInt64(7),
        CelTypeKind.Double => CelValue.FromDouble(2.5),
        CelTypeKind.Timestamp => CelValue.FromTimestamp(new CelTimestamp(0, 0)),
        CelTypeKind.Duration => CelValue.FromDuration(new CelDuration(1_000_000_000)),
        CelTypeKind.String => CelValue.FromString(new string('a', size)),
        CelTypeKind.Bytes => CelValue.FromBytes(Enumerable.Repeat((byte)'a', size).ToArray()),
        CelTypeKind.List => CelValue.FromList(CelList.Create([.. Enumerable.Repeat(CelValue.FromInt64(1), size)])),
        CelTypeKind.Map => CelValue.FromMap(CelMap.Create(Enumerable.Range(0, size).Select(key => new KeyValuePair<CelValue, CelValue>(CelValue.FromInt64(key), CelValue.FromInt64(1))))),
        _ => CelValue.FromInt64(7),
    };
}
