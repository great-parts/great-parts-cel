using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost table of the documentation (the cost page), checked row by row against evaluation. Each row is
// "row | expression | cost": the total cost of the expression is exact, so the evaluation succeeds with
// that cost as the limit and fails with CostLimitExceeded at one unit less. The total is the cost of the
// row plus the nodes around it: a literal 0, a read of a variable 1 (the classes Literal and Read of the page).
//
// Variables: i = 41, j = 2, big = 1234567890123 (int); u = 7u, v = 2u (uint); x = 1.5 (double); b = true;
// s = 'abcdefghijk' (11 code units), t = 'xyz', digits = '12345678901', tstr = 'true' (string); bs = the
// 11 bytes of s; d = 1 (dyn); l = l2 = [1, 2, 3], lx = [9, 2, 3] (list(dyn)); m = m2 = {'abc': 1},
// mi = {1: 'a'} (map(dyn, dyn)); li = [1, 2, 3] (list(int)). Bindings of the types list(dyn) and map(dyn, dyn)
// are checked at the top level only, for no units; a binding of the type list(int) costs n more, the check
// against the declared type before it descends into n elements.
// f(int) is an application function whose cost function gives 5; g(int) has none.
[Trait("Requirement", "R-3")]
public sealed class CostTableTests
{
    private const string Rows = """
        literal | 1 | 0
        literal 'abc' | 'abc' | 0
        variable | i | 1
        type identifier | int | 1
        variable of the type list(dyn) | l | 1
        check against a declared type, list(int) of 3 | li | 4
        list literal of 2 | [i, j] | 5
        list literal, empty | [] | 1
        map literal, string key of 3 | {'abc': i} | 5
        map literal, int key | {1: i} | 4
        select on a map, key of 3 | m.abc | 3
        has() on a map, key of 3 | has(m.abc) | 3
        logical_not | !b | 2
        logical_and, both operands | b && !b | 3
        logical_and, the first decides | !b && b | 2
        logical_or, the first decides | b || !b | 1
        conditional | b ? i : j | 2
        equals int | i == j | 3
        equals string, 11 + 3 | s == t | 5
        equals list of 3 | l == l2 | 6
        equals list, first pair unequal | l == lx | 4
        equals map of 1, key of 3 | m == m2 | 6
        not_equals | i != j | 3
        add_int64 | i + j | 3
        subtract_int64 | i - j | 3
        multiply_int64 | i * j | 3
        divide_int64 | i / j | 3
        modulo_int64 | i % j | 3
        negate_int64 | -i | 2
        add_uint64 | u + v | 3
        subtract_uint64 | u - v | 3
        multiply_uint64 | u * v | 3
        divide_uint64 | u / v | 3
        modulo_uint64 | u % v | 3
        add_double | x + x | 3
        subtract_double | x - x | 3
        multiply_double | x * x | 3
        divide_double | x / x | 3
        negate_double | -x | 2
        add_string, 11 + 3 | s + t | 7
        add_bytes, 11 + 11 | bs + bs | 9
        add_list, result of 6 | l + l2 | 9
        less_int64 | i < j | 3
        less_equals_uint64 | u <= v | 3
        greater_double | x > x | 3
        greater_equals_bool | b >= b | 3
        less_int64_double | d < x | 3
        less_string, 11 + 3 | s < t | 5
        greater_equals_bytes, 11 + 11 | bs >= bs | 6
        index_list | l[1] | 2
        index_map, string key of 3 | m['abc'] | 3
        index_map, int key | mi[1] | 2
        in_map, string key of 3 | 'abc' in m | 3
        in_list, equal at the second | 2 in l | 4
        size_list | size(l) | 2
        list_size | l.size() | 2
        size_map | size(m) | 2
        map_size | m.size() | 2
        size_bytes | size(bs) | 2
        bytes_size | bs.size() | 2
        size_string, 11 | size(s) | 4
        string_size, 11 | s.size() | 4
        contains_string, 11 + 3 | s.contains(t) | 5
        starts_with_string, 11 + 3 | s.startsWith(t) | 5
        ends_with_string, 11 + 3 | s.endsWith(t) | 5
        type | type(i) | 2
        type and type identifier | type(i) == int | 4
        to_dyn | dyn(i) | 2
        int64_to_int64 | int(i) | 2
        uint64_to_uint64 | uint(u) | 2
        double_to_double | double(x) | 2
        bool_to_bool | bool(b) | 2
        string_to_string | string(s) | 2
        bytes_to_bytes | bytes(bs) | 2
        int64_to_double | double(i) | 2
        uint64_to_double | double(u) | 2
        double_to_int64 | int(x) | 2
        uint64_to_int64 | int(u) | 2
        double_to_uint64 | uint(x) | 2
        int64_to_uint64 | uint(i) | 2
        string_to_int64, 11 | int(digits) | 4
        string_to_uint64, 11 | uint(digits) | 4
        string_to_double, 11 | double(digits) | 4
        string_to_bool, 4 | bool(tstr) | 3
        int64_to_string, result of 13 | string(big) | 4
        uint64_to_string, result of 1 | string(u) | 3
        double_to_string, result of 3 | string(x) | 3
        bool_to_string, result of 4 | string(b) | 3
        string_to_bytes, 11 and 11 | bytes(s) | 6
        bytes_to_string, 11 and 11 | string(bs) | 6
        all, 3 steps | l.all(e, e > 0) | 10
        all, empty range | [].all(e, e > 0) | 1
        exists, stops after 2 steps | l.exists(e, e == 2) | 7
        exists_one, 3 steps | l.exists_one(e, e > 1) | 10
        map, 3 steps | l.map(e, e * 2) | 14
        map with a filter, 3 steps, 2 added | l.map(e, e > 1, e * 10) | 17
        filter, 3 steps, 2 added | l.filter(e, e > 1) | 13
        application function with a cost function of 5 | f(i) | 7
        application function without a cost function | g(i) | 2
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("i")
        .AddVariable<long>("j")
        .AddVariable<long>("big")
        .AddVariable<ulong>("u")
        .AddVariable<ulong>("v")
        .AddVariable<double>("x")
        .AddVariable<bool>("b")
        .AddVariable<string>("s")
        .AddVariable<string>("t")
        .AddVariable<string>("digits")
        .AddVariable<string>("tstr")
        .AddVariable<byte[]>("bs")
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .AddVariable("l2", CelType.ListOf(CelType.Dyn))
        .AddVariable("lx", CelType.ListOf(CelType.Dyn))
        .AddVariable("m", CelType.MapOf(CelType.Dyn, CelType.Dyn))
        .AddVariable("m2", CelType.MapOf(CelType.Dyn, CelType.Dyn))
        .AddVariable("mi", CelType.MapOf(CelType.Dyn, CelType.Dyn))
        .AddVariable("li", CelType.ListOf(CelType.Int))
        .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int).WithCost(_ => 5)))
        .AddFunctionImplementation("f_int", arguments => arguments[0])
        .AddFunction<long, long>("g", value => value)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("i", 41L)
        .Add("j", 2L)
        .Add("big", 1_234_567_890_123L)
        .Add("u", 7UL)
        .Add("v", 2UL)
        .Add("x", 1.5)
        .Add("b", true)
        .Add("s", "abcdefghijk")
        .Add("t", "xyz")
        .Add("digits", "12345678901")
        .Add("tstr", "true")
        .Add("bs", Encoding.UTF8.GetBytes("abcdefghijk"))
        .Add("d", 1L)
        .Add("l", Ints(1, 2, 3))
        .Add("l2", Ints(1, 2, 3))
        .Add("lx", Ints(9, 2, 3))
        .Add("m", Map(CelValue.FromString("abc"), CelValue.FromInt64(1)))
        .Add("m2", Map(CelValue.FromString("abc"), CelValue.FromInt64(1)))
        .Add("mi", Map(CelValue.FromInt64(1), CelValue.FromString("a")))
        .Add("li", Ints(1, 2, 3));

    [Fact]
    public void EveryRow_CostsWhatTheCostPageSays()
    {
        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            string? failure = Check(cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    private string? Check(string expression, ulong cost)
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

        if (cost == 0)
        {
            return null;
        }

        CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        return belowCost.Errors.Length == 1 && belowCost.Errors[0].Code == CelErrorCode.CostLimitExceeded
            ? null
            : "limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough";
    }

    private static CelList Ints(params long[] values) => CelList.Create(values.Select(CelValue.FromInt64).ToArray());

    private static CelMap Map(CelValue key, CelValue value) => CelMap.Create([new KeyValuePair<CelValue, CelValue>(key, value)]);
}
