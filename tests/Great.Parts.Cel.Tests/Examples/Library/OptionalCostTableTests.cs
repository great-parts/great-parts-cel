using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of the set optional: the rows of the table 6.8 of the cost page for the overloads of the set, the macros
// optMap and optFlatMap of the table 9, and the equality of two optionals of the table 4.4. The rows are checked as in
// ExtensionCostTableTests: "row | expression | cost", the total cost of the expression is exact, so the evaluation
// succeeds with that cost as the limit and fails with CostLimitExceeded at one unit less. The total is the cost of the row plus
// the nodes around it: a literal 0, a read of a variable 1, a list of n elements 1 + n, a map of n pairs 1 + 2n and the
// length of each string key over 10 rounded up, a message of n fields 1 + n.
//
// What the rows rest on, beyond the tables. or and orValue charge their own 1 and the left part, and the right part only
// when it is evaluated, that is when the left one is empty. The selection and the index of the set cost what the
// selection and the index of the value cost: 1 for a list, an int key and a message field, 1 + the length of the key
// over 10 rounded up for a string key of a map, wherever the key is present or not. optional.unwrap and unwrapOpt cost
// 1 + n for the list of n elements that they read, whether the elements have a value or not. The macros are charged by
// their expansion (the cost page, the rule 2.5): x.hasValue() ? <body with the name bound to x.value()> : optional.none(), where
// optMap puts the body in optional.of; the receiver that is an identifier is read once, by hasValue, and the read of
// the copy that the expansion makes for value() is the expansion's own and costs 0; a receiver that is not an identifier is evaluated
// once, into the variable that the expansion adds, and the read of that variable costs 0, but the expansion adds
// the empty list that is the range of the fold of that variable: 1; the fold that binds the name to x.value() has its empty
// list as well, 1, and no step. A macro that the receiver of has no value gives hasValue and optional.none(); it does
// not evaluate the body. A literal list, map or message that has "?" elements costs what the one without them costs
// for the positions that are stored: the list 1 + the elements stored, the map 1 + 2 for each entry stored and the length
// of each string key stored over 10 rounded up, the message 1 + the fields stored and the length of the strings and bytes
// stored over 10 rounded up; a position whose optional is empty adds nothing to that, and the expressions of every
// position, stored or not, are charged as the author wrote them. Variables: i = 41 (int); o = optional.of(5),
// n = optional.none() (optional_type(int)); l = [1, 2, 3] (list(int)), bound as a CelList, so its first read is checked
// against the declared type and costs 1 + 3 (the cost page, "Values checked against their declared type"); l.first() is 1 + 3 + 1.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-1")]
public sealed class OptionalCostTableTests
{
    // Table 6.8: the overloads that cost 1, with their operands.
    private const string Functions = """
        optional_of | optional.of(1) | 1
        optional_of, a variable | optional.of(i) | 2
        optional_ofNonZeroValue | optional.ofNonZeroValue(0) | 1
        optional_ofNonZeroValue, a variable | optional.ofNonZeroValue(i) | 2
        optional_ofNonZeroValue, an empty list | optional.ofNonZeroValue([]) | 2
        optional_ofNonZeroValue, a list of 2 | optional.ofNonZeroValue([1, 2]) | 4
        optional_none | optional.none() | 1
        optional_value | o.value() | 2
        optional_value, of a call | optional.of(1).value() | 2
        optional_hasValue, with a value | o.hasValue() | 2
        optional_hasValue, empty | n.hasValue() | 2
        list_first, 3 elements | [1, 2, 3].first() | 5
        list_first, a variable | l.first() | 5
        list_first, empty list | [].first() | 2
        list_last, 3 elements | [1, 2, 3].last() | 5
        list_last, empty list | [].last() | 2
        """;

    // or and orValue: the right part is evaluated only when the left one is empty.
    private const string OrAndOrValue = """
        optional_or_optional, the left one has a value | o.or(n) | 2
        optional_or_optional, the left one is empty | n.or(o) | 3
        optional_or_optional, a call on the right, not evaluated | optional.of(1).or(optional.of(i)) | 2
        optional_or_optional, a call on the right, evaluated | optional.none().or(optional.of(i)) | 4
        optional_or_optional, chain of two | n.or(n).or(o) | 5
        optional_orValue_value, the left one has a value | o.orValue(i) | 2
        optional_orValue_value, the left one is empty | n.orValue(i) | 3
        """;

    // select_optional_field: as a selection, 1, and 1 + the length of the key over 10 rounded up in a map with a string key. The
    // map of 1 pair is 1 + 2 + the key over 10 rounded up: 4 with a key of 1 unit and 5 with a key of 11.
    private const string Selection = """
        select_optional_field, a map, a key of 1 | {'a': 1}.?a | 6
        select_optional_field, a map, an absent key | {'a': 1}.?b | 6
        select_optional_field, a map, a key of 11 | {'abcdefghijk': 1}.?abcdefghijk | 8
        """;

    private const string MessageSelection = """
        select_optional_field, a field of a message | TestAllTypes{single_int32: 1}.?single_int32 | 3
        select_optional_field, a field that is not set | TestAllTypes{single_int32: 1}.?single_int64 | 3
        """;

    // The optional index and the index of an optional: a list or an int key 1, a string key of a map 1 + the key over 10 rounded
    // up; the optional that holds the list or the map costs its construction, here optional.of, 1.
    private const string Indexes = """
        list_optindex_optional_int | [1, 2][?0] | 4
        list_optindex_optional_int, out of range | [1, 2][?5] | 4
        optional_list_optindex_optional_int | optional.of([1, 2])[?1] | 5
        optional_list_index_int | optional.of([1, 2])[1] | 5
        optional_list_index_int, out of range | optional.of([1, 2])[5] | 5
        map_optindex_optional_value, an int key | {1: 2}[?1] | 4
        map_optindex_optional_value, a string key of 1 | {'a': 1}[?'a'] | 6
        map_optindex_optional_value, a string key of 11 | {'abcdefghijk': 1}[?'abcdefghijk'] | 8
        map_optindex_optional_value, an absent key | {'a': 1}[?'b'] | 6
        optional_map_optindex_optional_value | optional.of({1: 2})[?1] | 5
        optional_map_index_value | optional.of({1: 2})[1] | 5
        """;

    // optional.unwrap and unwrapOpt: 1 + n of the list that is read, the elements and the list charged as written.
    private const string Unwrap = """
        optional_unwrap, 2 elements | optional.unwrap([optional.of(1), optional.none()]) | 8
        optional_unwrapOpt, 1 element | [optional.of(1)].unwrapOpt() | 5
        optional_unwrapOpt, empty list | [].unwrapOpt() | 2
        optional_unwrap, empty list | optional.unwrap([]) | 2
        optional_unwrapOpt, all empty | [optional.none(), optional.none()].unwrapOpt() | 8
        """;

    // Table 9. optMap: a receiver that is an identifier is R + B + 4 with a value (R the receiver, B the body) and R + 2
    // without one; a receiver that is a call is R + B + 5 and R + 3. optFlatMap has no optional.of: R + B + 3 and R + 2 for an
    // identifier, R + B + 4 and R + 3 for a call.
    private const string Macros = """
        optMap, an identifier, with a value; R 1 and B 2 | o.optMap(y, y + 1) | 7
        optMap, an identifier, empty | n.optMap(y, y + 1) | 3
        optMap, an identifier, with a value; the body is the name | o.optMap(y, y) | 6
        optMap, a call, with a value; R 1 and B 2 | optional.of(1).optMap(y, y + 1) | 8
        optMap, a call, empty | optional.none().optMap(y, y + 1) | 4
        optMap, a call with a list, with a value; R 4 and B 1 | [1, 2].first().optMap(y, y) | 10
        optFlatMap, an identifier, with a value; R 1 and B 2 | o.optFlatMap(y, optional.of(y)) | 6
        optFlatMap, an identifier, empty | n.optFlatMap(y, optional.of(y)) | 3
        optFlatMap, a call, with a value; R 1 and B 2 | optional.of(1).optFlatMap(y, optional.of(y)) | 7
        optFlatMap, a call, empty | optional.none().optFlatMap(y, optional.of(y)) | 4
        optMap, the body is not evaluated when the receiver is empty | n.optMap(y, y / 0) | 3
        optMap in the body of optMap, with a value; R 1, B 8 | o.optMap(y, o.optMap(z, y + z)) | 13
        """;

    // The literals with "?" positions: the node charges 1 and what it stores, after its parts.
    private const string Literals = """
        list with an optional element, dropped | [?optional.none(), 1] | 3
        list, one stored of two | [?optional.of(1), ?optional.none()] | 4
        list, variables, one stored | [?o, ?n] | 4
        list, a plain element and a dropped one | [1, ?n] | 3
        list, all dropped | [?n, ?n] | 3
        map, one stored of two | {?'a': optional.of(1), ?'b': optional.none()} | 6
        map, dropped | {?'a': n} | 2
        map, a plain entry and a dropped one | {'a': 1, ?'b': n} | 5
        map, a string key of 11 stored | {?'abcdefghijk': o} | 6
        """;

    private const string MessageLiterals = """
        message, one stored of two | TestAllTypes{?single_int32: optional.of(1), ?single_int64: optional.none()} | 4
        message, a plain field and a dropped one | TestAllTypes{single_int32: 1, ?single_int64: optional.none()} | 3
        message, a string of 11 stored | TestAllTypes{?single_string: optional.of('abcdefghijk')} | 5
        """;

    // Table 4.4: two optionals with values cost 1 + the comparison of the values; two empty ones, or one empty and one with
    // a value, 1; the operands are charged as written.
    private const string Equality = """
        equality of optionals, both with a value | optional.of(1) == optional.of(1) | 4
        equality of optionals, both empty | optional.none() == optional.none() | 3
        equality of optionals, one empty | optional.of(1) == optional.none() | 3
        equality of optionals, strings of 2 | optional.of('ab') == optional.of('ab') | 5
        equality of optionals, lists | optional.of([1]) == optional.of([1]) | 9
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable<long>("i")
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("n", CelType.OptionalOf(CelType.Int))
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment messages = ProtobufExample.Builder3()
        .AddExtension(CelExtensions.Optional(2))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("i", 41L)
        .Add("o", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(5))))
        .Add("n", CelValue.FromOptional(default))
        .Add("l", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3)));

    [Fact]
    public void FunctionRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(environment, Functions));

    [Fact]
    public void OrAndOrValueRows_ChargeTheRightPartOnlyWhenItIsEvaluated() => Assert.Empty(CheckRows(environment, OrAndOrValue));

    [Fact]
    public void SelectionRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(environment, Selection));

    [Fact]
    public void SelectionOfAMessageRows_CostAField() => Assert.Empty(CheckRows(messages, MessageSelection));

    [Fact]
    public void IndexRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(environment, Indexes));

    [Fact]
    public void UnwrapRows_CostOnePlusTheSizeOfTheList() => Assert.Empty(CheckRows(environment, Unwrap));

    [Fact]
    public void MacroRows_CostTheirExpansion() => Assert.Empty(CheckRows(environment, Macros));

    [Fact]
    public void LiteralRows_ChargeWhatIsStored() => Assert.Empty(CheckRows(environment, Literals));

    [Fact]
    public void MessageLiteralRows_ChargeWhatIsStored() => Assert.Empty(CheckRows(messages, MessageLiterals));

    [Fact]
    public void EqualityRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(environment, Equality));

    private List<string> CheckRows(CelEnvironment target, string rows)
    {
        var failures = new List<string>();
        foreach (string row in ExtensionRows.Lines(rows))
        {
            string[] cells = row.Split(" | ");
            string? failure = Check(target, cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                failures.Add(row + ": " + failure);
            }
        }

        return failures;
    }

    private string? Check(CelEnvironment target, string expression, ulong cost)
    {
        CelCompileResult<CelValue> compiled = target.Compile<CelValue>(expression);
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
