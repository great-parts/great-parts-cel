using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The cost of reading application data (the cost page, application data), row by row. Each row is
// "row | expression | cost": the total is exact, so the evaluation succeeds with that cost as the limit and
// fails with CostLimitExceeded at one unit less. The total is the row plus the nodes around it: a read of a
// variable 1, a literal 0, a step of a comprehension 1.
//
// deal, deal2 = {amount: 5, title: 'ab', lines: [1, 2, 3]} and deal3 = {amount: 6, …} are acme.crm.Deal
// over a carrier class. lv is list(int) bound to a CelList.CreateView of 3 elements, checked against its
// declared type by reading them; li is IReadOnlyList<long> of 3, a typed view that already fits. The map
// views have 3 entries: md over a Dictionary (source order), mi, ms and mg over immutable dictionaries
// (ascending keys) with int keys, the string keys 'a', 'bb' and 'ccccccccccc' (Lmax = 11) and Guid keys
// (Lmax = 36). j = {"abc": 1, "b": 2, "s": "abcdefghijk"} (names of 3, 1 and 1 bytes), j1 = j2 = {"a": 1},
// ja = [1, 2, 3]. fs, fss and fq are application functions without a cost function that return a set of 3
// ints, a set of the strings 'a', 'bb', 'ccccccccccc' and a queue of 3 ints.
//
// The equality of two JSON objects charges the check for repeats of each object (the row of size and
// enumeration, which also gives its size), then the row of two maps with the JSON lookup in the second.
// The index of a JSON array charges its row instead of the index of a list, as the rows of a JSON object
// replace the rows of a map.
[Trait("Requirement", "R-3")]
public sealed class ApplicationDataCostTests
{
    private const string Rows = """
        field of a type described at run time | deal.amount | 2
        field holding a list of 3, not checked at depth | deal.lines | 2
        has() of a field | has(deal.amount) | 2
        equality of two objects, three fields compared | deal == deal2 | 16
        equality of two objects, the first field unequal | deal == deal3 | 6
        check against a declared type, a view with a converter, list(int) of 3 | lv | 4
        check against a declared type, a typed view that already fits | li | 1
        enumeration of a map view in source order | md.all(k, true) | 4
        enumeration of a map view by ascending int key | mi.all(k, true) | 11
        enumeration of a map view by ascending string key, Lmax 11 | ms.all(k, true) | 29
        enumeration of a map view by ascending Guid key | mg.all(k, true) | 59
        JSON key lookup, key of 3 in an object of 3 | j.abc | 8
        JSON key lookup, key of 1 | j.b | 8
        JSON index with a string key | j['abc'] | 8
        JSON has() | has(j.abc) | 8
        JSON string of 11 read after its lookup | j.s | 11
        JSON size | size(j) | 8
        JSON size as a receiver | j.size() | 8
        JSON in, key of 3 | 'abc' in j | 9
        JSON enumeration | j.all(k, true) | 11
        JSON equality, one property each | j1 == j2 | 13
        JSON array index, 3 elements | ja[1] | 5
        set of 3 ints returned by an application function | fs() | 8
        set of 3 strings returned by an application function, Lmax 11 | fss() | 26
        collection without an index of 3 returned by an application function | fq() | 5
        """;

    private static readonly CelObjectType Deal = new CelObjectTypeBuilder<DealCarrier>("acme.crm.Deal")
        .AddField("amount", CelType.Int, carrier => CelValue.FromInt64(carrier.Amount))
        .AddField("title", CelType.String, carrier => CelValue.FromString(carrier.Title))
        .AddField("lines", CelType.ListOf(CelType.Int), carrier => CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3))))
        .Build();

    private static readonly string[] Strings = ["a", "bb", "ccccccccccc"];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("crm", Deal))
        .AddVariable("deal", Deal.Type)
        .AddVariable("deal2", Deal.Type)
        .AddVariable("deal3", Deal.Type)
        .AddVariable("lv", CelType.ListOf(CelType.Int))
        .AddVariable<IReadOnlyList<long>>("li")
        .AddVariable<CelMap>("md")
        .AddVariable<CelMap>("mi")
        .AddVariable<CelMap>("ms")
        .AddVariable<CelMap>("mg")
        .AddVariable<JsonElement>("j")
        .AddVariable<JsonElement>("j1")
        .AddVariable<JsonElement>("j2")
        .AddVariable<JsonElement>("ja")
        .AddFunction<ISet<long>>("fs", () => new HashSet<long> { 3, 1, 2 })
        .AddFunction<ISet<string>>("fss", () => new HashSet<string>(Strings))
        .AddFunction<IReadOnlyCollection<long>>("fq", () => new Queue<long>([3, 1, 2]))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_CostsWhatTheCostPageSays()
    {
        using JsonDocument j = JsonDocument.Parse("""{"abc": 1, "b": 2, "s": "abcdefghijk"}""");
        using JsonDocument j1 = JsonDocument.Parse("""{"a": 1}""");
        using JsonDocument j2 = JsonDocument.Parse("""{"a": 1}""");
        using JsonDocument ja = JsonDocument.Parse("""[1, 2, 3]""");
        long[] numbers = [1, 2, 3];
        CelBindings bindings = new CelBindings()
            .Add("deal", new DealCarrier { Amount = 5, Title = "ab" })
            .Add("deal2", new DealCarrier { Amount = 5, Title = "ab" })
            .Add("deal3", new DealCarrier { Amount = 6, Title = "ab" })
            .Add("lv", CelList.CreateView(numbers, CelValue.FromInt64))
            .Add<IReadOnlyList<long>>("li", numbers)
            .Add("md", CelMap.CreateView(new Dictionary<long, long> { [3] = 3, [1] = 1, [2] = 2 }, CelValue.FromInt64, keepSourceOrder: false))
            .Add("mi", CelMap.CreateView(ImmutableDictionary.CreateRange(new Dictionary<long, long> { [3] = 3, [1] = 1, [2] = 2 }), CelValue.FromInt64, keepSourceOrder: false))
            .Add("ms", CelMap.CreateView(ImmutableDictionary.CreateRange(new Dictionary<string, long> { ["ccccccccccc"] = 3, ["a"] = 1, ["bb"] = 2 }), CelValue.FromInt64, keepSourceOrder: false))
            .Add("mg", CelMap.CreateView(ImmutableDictionary.CreateRange(new Dictionary<Guid, long> { [Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")] = 1, [Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7")] = 2, [Guid.Parse("00000000-0000-0000-0000-000000000001")] = 3 }), CelValue.FromInt64, keepSourceOrder: false))
            .Add("j", j.RootElement)
            .Add("j1", j1.RootElement)
            .Add("j2", j2.RootElement)
            .Add("ja", ja.RootElement);

        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            string? failure = Check(cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture), bindings);
            if (failure is not null)
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    private string? Check(string expression, ulong cost, CelBindings bindings)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelProgram<CelValue> program = compiled.GetProgramOrThrow();
        CelEvalResult<CelValue> atCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None);
        if (!atCost.IsSuccess)
        {
            return "limit " + cost.ToString(CultureInfo.InvariantCulture) + ": " + atCost.Errors[0].Code + " at " + atCost.Errors[0].Location.Offset.ToString(CultureInfo.InvariantCulture);
        }

        CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        return belowCost.Errors.Length == 1 && belowCost.Errors[0].Code == CelErrorCode.CostLimitExceeded
            ? null
            : "limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough";
    }

    private sealed class DealCarrier
    {
        public long Amount { get; init; }

        public string Title { get; init; } = string.Empty;
    }
}
