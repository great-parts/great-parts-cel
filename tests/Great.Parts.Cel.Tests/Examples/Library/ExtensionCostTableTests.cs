using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of the functions and macros of the extension sets, the rows of the cost page for the sets (tables 6.1 and
// 6.3 to 6.9, and the macros of table 9), checked as the rows of CostTableTests are: "row | expression | cost",
// the total cost of the expression is exact, so the evaluation succeeds with that cost as the limit and fails with
// CostLimitExceeded at one unit less. The total is the cost of the row plus the nodes around it: a literal 0, a read
// of a variable 1, a list of n elements 1 + n, a map of n pairs 1 + 2n and the length of each string key over 10 rounded
// up. A macro costs its steps (1 each, and the body that the author wrote), the first value of its result, and what it
// adds to the result; the reads that the expansion makes of the accumulator and of the variables of the loop cost 0.
// Strings count in UTF-16 code units; a result that is built is charged as it is built, and the sum is the same.
// Every table has a row for a call whose list, string or map is empty (n = 0).
//
// Variables: i = 41, j = 2 (int); u = 7u (uint); x = 1.5 (double); s = 'abcdefghijk' (11 code units), t = 'xyz',
// q = 'abcdefghi' (9) (string); bs = the 11 bytes of s; l = [1, 2, 3] (list(dyn)). The row of the table 6.7 for
// cel_block_list has no example here: the overload cannot be called from the text of an expression, and its cost is
// that of the macro cel.block of the internal set of the conformance runner, with the set block.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-1")]
public sealed class ExtensionCostTableTests
{
    private const string Strings = """
        string_char_at_int | s.charAt(2) | 5
        string_substring_int | s.substring(3) | 5
        string_substring_int_int | s.substring(2, 5) | 5
        string_trim, result of 11 | s.trim() | 6
        string_lower_ascii | s.lowerAscii() | 6
        string_upper_ascii | s.upperAscii() | 6
        string_reverse | s.reverse() | 6
        strings_quote, 9 and result of 11 | strings.quote(q) | 5
        string_index_of_string, 11 + 3 | s.indexOf(t) | 5
        string_index_of_string_int | s.indexOf(t, 0) | 5
        string_last_index_of_string | s.lastIndexOf(t) | 5
        string_last_index_of_string_int | s.lastIndexOf(t, 5) | 5
        string_replace_string_string, 14 and result of 8 | s.replace('abc', '') | 5
        string_replace_string_string_int, 14 and result of 12 | s.replace('a', 'zz', 1) | 6
        string_split_string, 12 and 2 parts of 10 | s.split('c') | 7
        string_split_string_int, 12 and 1 part of 11 | s.split('c', 1) | 7
        string_split_string_int, no part | s.split('c', 0) | 4
        string_split_string, one empty part | ''.split(',') | 3
        list_join, 3 elements | ['a', 'b', 'c'].join() | 10
        list_join_string, 3 elements | ['a', 'b', 'c'].join(',') | 10
        list_join, empty list | [].join() | 2
        string_format, format of 5, 2 arguments | '%s-%d'.format(['a', 1]) | 8
        string_format, empty list | 'abc'.format([]) | 4
        """;

    private const string Math = """
        math_@max_int | math.greatest(i) | 2
        math_@min_double | math.least(x) | 2
        math_@max_int_int | math.greatest(i, j) | 3
        math_@min_uint_uint | math.least(u, u) | 3
        math_@max_int_double | math.greatest(i, x) | 3
        math_@max_list_int, three arguments | math.greatest(i, j, 3) | 10
        math_@min_list_double, a list literal of 2 | math.least([x, x]) | 8
        math_@max_list_uint, a list literal of 4 | math.greatest([1u, 2u, 3u, 4u]) | 10
        math_abs_int | math.abs(i) | 2
        math_sign_double | math.sign(x) | 2
        math_bitShiftLeft_int_int | math.bitShiftLeft(i, 2) | 2
        math_bitAnd_uint_uint | math.bitAnd(u, u) | 3
        math_sqrt_int | math.sqrt(i) | 2
        math_sqrt_uint | math.sqrt(u) | 2
        math_sqrt_double | math.sqrt(x) | 2
        math_isNaN_double | math.isNaN(x) | 2
        math_ceil_double | math.ceil(x) | 2
        """;

    private const string Lists = """
        list_slice, 2 elements | l.slice(1, 3) | 4
        list_slice, no element | l.slice(1, 1) | 2
        lists_range, 5 | lists.range(5) | 6
        lists_range, 0 | lists.range(0) | 1
        list_reverse, 3 elements | l.reverse() | 5
        list_reverse, empty list | [].reverse() | 2
        list_flatten, v = 2 + 2 + 1 | [[1, 2], [3]].flatten() | 14
        list_flatten, empty list | [].flatten() | 2
        list_flatten_int, depth 2, v = 2 + 2 + 1 + 2 | [[1, [2, 3]], [4]].flatten(2) | 19
        list_int_sort, 3 elements | [3, 1, 2].sort() | 14
        list_double_sort, 2 elements | [2.5, 1.5].sort() | 10
        list_string_sort, Lmax 6 | ['bbbbbb', 'a', 'c'].sort() | 26
        list_bytes_sort, Lmax 1 | [b'b', b'a'].sort() | 14
        list_int_sort, empty list | [].sort() | 2
        list_int_sort, 1 element | [5].sort() | 5
        list_int_sortByAssociatedKeys, sortBy | [3, 1, 2].sortBy(e, e) | 25
        list_string_sortByAssociatedKeys, Lmax 3 | ['bb', 'a', 'ccc'].sortBy(e, e) | 31
        sortBy, empty list | [].sortBy(e, e) | 4
        list_distinct, 1 and 2 compared | [1, 2, 1].distinct() | 10
        list_distinct, all different | [1, 2, 3].distinct() | 11
        list_distinct, empty list | [].distinct() | 2
        """;

    private const string Sets = """
        list_sets_contains_list, 1 + (1 + 1) + (1 + 2) | sets.contains([1, 2, 3], [1, 2]) | 13
        list_sets_contains_list, the first element absent | sets.contains([1, 2], [3, 1]) | 10
        list_sets_contains_list, empty lists | sets.contains([], []) | 3
        list_sets_intersects_list, found at the second | sets.intersects([1, 2], [3, 2]) | 13
        list_sets_intersects_list, found at the first | sets.intersects([1, 2], [1, 2]) | 9
        list_sets_intersects_list, empty first list | sets.intersects([], [1]) | 4
        list_sets_equivalent_list, 1 + 5 + 5 | sets.equivalent([1, 2], [2, 1]) | 17
        list_sets_equivalent_list, the first contains is false | sets.equivalent([1, 2], [3]) | 9
        list_sets_equivalent_list, empty lists | sets.equivalent([], []) | 3
        """;

    private const string Encoders = """
        base64_encode_bytes, 3 and 4 | base64.encode(b'abc') | 3
        base64_encode_bytes, 11 and 16 | base64.encode(bs) | 6
        base64_decode_string, 4 and 3 | base64.decode('YWJj') | 3
        base64_decode_string, empty | base64.decode('') | 1
        json_encode_dyn, result of 7 | json.encode({'a': 1}) | 6
        json_encode_dyn, empty list | json.encode([]) | 3
        json_encode_dyn, result of 22 | json.encode([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]) | 15
        """;

    // The macro cel.bind: the empty list that is its range, 1, and the expressions of the author.
    private const string Bindings = """
        cel.bind, literals | cel.bind(y, 1, y + 1) | 3
        cel.bind, a variable | cel.bind(y, i, y + y) | 5
        cel.bind, nested | cel.bind(a, 1, cel.bind(b, 2, a + b)) | 5
        """;

    // Table 6.8 for the two-variable macros: the pair that cel.@mapInsert adds is 2, and ⌈L/10⌉ for the
    // string key of L code units ('a' 1, 'ax' 1) of the pair that a map result adds; transformMap and transformMapEntry
    // begin with the empty map, 1.
    private const string MapInsert = """
        @mapInsert_map_key_value, int keys | [10, 20].transformMap(k, e, e + 1) | 14
        @mapInsert_map_key_value, string keys of 1 | {'a': 1, 'b': 2}.transformMap(k, e, e * 2) | 20
        @mapInsert_map_key_value, with a filter | [10, 20].transformMap(k, e, e > 10, e + 1) | 14
        @mapInsert_map_map, int keys | [10, 20].transformMapEntry(k, e, {k: e}) | 20
        @mapInsert_map_map, string key of 2 | {'a': 1}.transformMapEntry(k, e, {k + 'x': e}) | 18
        @mapInsert_map_map, with a filter | [10, 20].transformMapEntry(k, e, e > 10, {k: e}) | 17
        """;

    private const string Network = """
        string_to_ip, 11 | ip('192.168.0.1') | 3
        string_to_cidr, 10 | cidr('10.0.0.0/8') | 2
        is_ip, 7 | isIP('1.2.3.4') | 2
        is_ip, empty | isIP('') | 1
        is_cidr, 10 | isCIDR('10.0.0.0/8') | 2
        ip_is_canonical, 7 | ip.isCanonical('1.2.3.4') | 2
        cidr_contains_ip_string, 8 | cidr('10.0.0.0/8').containsIP('10.1.2.3') | 4
        cidr_contains_cidr_string, 11 | cidr('10.0.0.0/8').containsCIDR('10.1.0.0/16') | 5
        ip_to_string, result of 7 | string(ip('1.2.3.4')) | 4
        cidr_to_string, result of 10 | string(cidr('10.0.0.0/8')) | 4
        ip_family | ip('1.2.3.4').family() | 3
        cidr_ip | cidr('10.0.0.0/8').ip() | 3
        ip_is_loopback | ip('127.0.0.1').isLoopback() | 3
        ip_is_global_unicast | ip('8.8.8.8').isGlobalUnicast() | 3
        ip_is_link_local_multicast | ip('224.0.0.1').isLinkLocalMulticast() | 3
        ip_is_link_local_unicast, an address of 11 | ip('169.254.1.1').isLinkLocalUnicast() | 4
        ip_is_unspecified | ip('0.0.0.0').isUnspecified() | 3
        cidr_contains_ip_ip | cidr('10.0.0.0/8').containsIP(ip('10.1.2.3')) | 5
        cidr_contains_cidr | cidr('10.0.0.0/8').containsCIDR(cidr('10.1.0.0/16')) | 6
        cidr_is_mask | cidr('10.0.0.0/8').isMask() | 3
        cidr_masked | cidr('10.1.2.3/8').masked() | 3
        cidr_prefix_length | cidr('10.0.0.0/8').prefixLength() | 3
        """;

    // Table 9, the macros of the sets: one step is 1, the range and the body are the expressions of the author.
    private const string Macros = """
        all, 3 steps | [1, 2, 3].all(k, e, e > 0) | 13
        all, stops after 1 step | [1, 2, 3].all(k, e, e > 1) | 7
        all over a map, 2 steps | {'a': 1, 'b': 2}.all(k, e, e > 0) | 13
        all, empty range | [].all(k, e, e > 0) | 1
        exists, stops after 2 steps | [1, 2, 3].exists(k, e, e == 2) | 10
        existsOne, 3 steps | [1, 2, 3].existsOne(k, e, e > 1) | 13
        exists_one, 3 steps | [1, 2, 3].exists_one(k, e, e > 1) | 13
        transformList, 3 steps, 3 added | [1, 2, 3].transformList(k, e, e * 2) | 17
        transformList with a filter, 3 steps, 2 added | [1, 2, 3].transformList(k, e, e > 1, e * 10) | 20
        transformList, empty range | [].transformList(k, e, e) | 2
        transformMap, empty range | [].transformMap(k, e, e) | 2
        transformMapEntry, empty range | {}.transformMapEntry(k, e, {k: e}) | 2
        transformMapEntry, 2 pairs at each of 2 steps | [1, 2].transformMapEntry(k, e, {k: e, k + 10: e}) | 34
        transformMapEntry with a filter, 2 steps pass, an empty map added | [1, 2, 3].transformMapEntry(k, e, e > 1, {}) | 16
        transformList, a macro in the body | [1, 2].transformList(k, e, [3, 4].all(m, w, w > m)) | 30
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddExtension(CelExtensions.Encoders(1))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Network(1))
        .AddVariable<long>("i")
        .AddVariable<long>("j")
        .AddVariable<ulong>("u")
        .AddVariable<double>("x")
        .AddVariable<string>("s")
        .AddVariable<string>("t")
        .AddVariable<string>("q")
        .AddVariable<byte[]>("bs")
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("i", 41L)
        .Add("j", 2L)
        .Add("u", 7UL)
        .Add("x", 1.5)
        .Add("s", "abcdefghijk")
        .Add("t", "xyz")
        .Add("q", "abcdefghi")
        .Add("bs", Encoding.UTF8.GetBytes("abcdefghijk"))
        .Add("l", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3)));

    [Fact]
    public void StringsRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Strings));

    [Fact]
    public void MathRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Math));

    [Fact]
    public void ListsRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Lists));

    [Fact]
    public void SetsRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Sets));

    [Fact]
    public void EncodersRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Encoders));

    [Fact]
    public void BindingsRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Bindings));

    [Fact]
    public void MapInsertRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(MapInsert));

    [Fact]
    public void NetworkRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Network));

    [Fact]
    public void MacroRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Macros));

    private List<string> CheckRows(string rows)
    {
        var failures = new List<string>();
        foreach (string row in ExtensionRows.Lines(rows))
        {
            string[] cells = row.Split(" | ");
            string? failure = Check(cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                failures.Add(row + ": " + failure);
            }
        }

        return failures;
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

        CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        return belowCost.Errors.Length == 1 && belowCost.Errors[0].Code == CelErrorCode.CostLimitExceeded
            ? null
            : "limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough";
    }
}
