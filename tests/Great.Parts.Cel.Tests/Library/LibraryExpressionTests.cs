using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The overloads of the standard library and its macros, compiled and evaluated: each overload through an
// expression, the errors with their codes, texts and places, and the costs of the overloads and the macros.
// Operands of the static type dyn reach the overloads that type checking allows only with dyn.
//
// Variables: n: int = 6, k: uint = 4u, r: double = 0.5, flag: bool = true, word: string = 'kiwi',
// raw: bytes = the bytes of 'kiwi', any: dyn = 3, texts: dyn = ['z'], numbers: dyn = [7u],
// items: list(int) = [1, 2, 3] and prices: map(string, int) = {'apple': 3, 'kiwi': 5}.
public sealed class LibraryExpressionTests
{
    // "expression → expected", where expected is an expression whose value the result equals, kind included.
    private const string Values = """
        true ? 'yes' : 'no' → 'yes'
        flag && !flag → false
        !flag || flag → true
        !false → true
        [1, 2].all(x, x > 0) → true
        [1, 2].exists(x, x == 2) → true
        n == 6 → true
        any == 3u → true
        any == 3.0 → true
        [any] == [3.0] → true
        n != 7 → true
        'kiwi' != word → false
        null == null → true
        b'ki' + b'wi' == raw → true
        r + 0.25 → 0.75
        n + 1 → 7
        [n] + [any] → [6, 3]
        word + '!' → 'kiwi!'
        k + 1u → 5u
        r / 4.0 → 0.125
        -n / 4 → -1
        k / 3u → 1u
        -n % 4 → -2
        k % 3u → 1u
        r * r → 0.25
        n * -n → -36
        k * k → 16u
        -r → -0.5
        -n → -6
        r - 1.0 → -0.5
        n - 10 → -4
        k - 4u → 0u
        false < flag → true
        n < 7 → true
        any < 3.5 → true
        any < 4u → true
        k < 5u → true
        dyn(k) < 4.5 → true
        dyn(k) < 5 → true
        r < 1.0 → true
        dyn(r) < 1 → true
        dyn(r) < 1u → true
        'apple' < word → true
        b'a' < raw → true
        flag <= true → true
        n <= 6 → true
        any <= 3.0 → true
        any <= 3u → true
        k <= 4u → true
        dyn(k) <= 4.0 → true
        dyn(k) <= 4 → true
        r <= 0.5 → true
        dyn(r) <= 0 → false
        dyn(r) <= 0u → false
        word <= 'kiwi' → true
        raw <= b'kiwi' → true
        flag > false → true
        n > 5 → true
        any > 2.5 → true
        any > 2u → true
        k > 3u → true
        dyn(k) > 3.5 → true
        dyn(k) > -1 → true
        r > 0.25 → true
        dyn(r) > 0 → true
        dyn(r) > 0u → true
        word > 'kiwa' → true
        raw > b'kiw' → true
        flag >= flag → true
        n >= 6 → true
        any >= 3.0 → true
        any >= 3u → true
        k >= 4u → true
        dyn(k) >= 4.0 → true
        dyn(k) >= 4 → true
        r >= 0.5 → true
        dyn(r) >= -1 → true
        dyn(r) >= 1u → false
        word >= 'kiwi' → true
        raw >= b'kiwi' → true
        type(n) == int → true
        type(texts) == list → true
        type(type(n)) == type → true
        dyn(word) → 'kiwi'
        bool(flag) → true
        bool('F') → false
        bytes(raw) → b'kiwi'
        bytes('ü') → b'\xc3\xbc'
        double(r) → 0.5
        double(n) → 6.0
        double('-1.25e1') → -12.5
        double(k) → 4.0
        int(n) → 6
        int(-r) → 0
        int('-9223372036854775808') == -9223372036854775807 - 1 → true
        int(k) → 4
        string(word) → 'kiwi'
        string(flag) → 'true'
        string(raw) → 'kiwi'
        string(r * 1e6) → '500000'
        string(-n) → '-6'
        string(k) → '4'
        uint(k) → 4u
        uint(r) → 0u
        uint(n) → 6u
        uint('18446744073709551615') → 18446744073709551615u
        items[0] → 1
        items[2] → 3
        [1, 2][dyn(1u)] → 2
        [1, 2][dyn(1.0)] → 2
        [1, 2][dyn(-0.0)] → 1
        dyn(items)[1] → 2
        prices['kiwi'] → 5
        dyn(prices)['apple'] → 3
        {1: 'a'}[dyn(1.0)] → 'a'
        {1: 'a'}[dyn(1u)] → 'a'
        {1u: 'a'}[dyn(1)] → 'a'
        {true: 1}[true] → 1
        2 in items → true
        4 in items → false
        dyn(2.0) in items → true
        dyn(3u) in items → true
        [] in [[], [1]] → true
        'apple' in prices → true
        'pear' in prices → false
        dyn(1.0) in {1: 'a'} → true
        dyn(1.5) in {1: 'a'} → false
        size(items) → 3
        items.size() → 3
        size(prices) → 2
        prices.size() → 2
        size(raw) → 4
        raw.size() → 4
        size(b'') → 0
        size(word) → 4
        word.size() → 4
        size('ü😀') → 2
        word.contains('iw') → true
        word.contains('') → true
        word.contains('kiwis') → false
        'abababc'.contains('ababc') → true
        word.startsWith('ki') → true
        word.startsWith('wi') → false
        word.endsWith('wi') → true
        word.endsWith('kiwis') → false
        ''.endsWith('') → true
        has(prices.kiwi) → true
        has(prices.pear) → false
        items.all(x, x > 0) → true
        items.all(x, x > 1) → false
        [0, -1].all(x, 1 / x > 0) → false
        items.exists(x, x == 3) → true
        [0, 1].exists(x, 1 / x > 0) → true
        items.exists_one(x, x > 1) → false
        items.exists_one(x, x > 2) → true
        items.map(x, x * 2) → [2, 4, 6]
        items.map(x, x > 1, x * 10) → [20, 30]
        items.filter(x, x != 2) → [1, 3]
        prices.all(key, size(key) > 3) → true
        prices.map(key, key) → ['apple', 'kiwi']
        prices.filter(key, prices[key] > 4) → ['kiwi']
        dyn(prices).exists(key, key == 'apple') → true
        """;

    // "expression → code | offset | message" of the single error of the evaluation. The place of a call is the
    // operator or the "(" after the name; a call inside a macro keeps its own place.
    private const string Errors = """
        n / 0 → DivisionByZero | 2 | division by zero
        k % 0u → ModulusByZero | 2 | modulus by zero
        9223372036854775807 + n → Overflow | 20 | integer overflow
        -9223372036854775807 - n → Overflow | 21 | integer overflow
        k - 5u → Overflow | 2 | unsigned integer overflow
        int(r * 4e19) → ConversionRange | 3 | 2e+19 is out of the range of int
        int(1e19 * r * 2.0) → ConversionRange | 3 | 1e+19 is out of the range of int
        uint(-r) → ConversionRange | 4 | -0.5 is out of the range of uint
        uint(-n) → ConversionRange | 4 | -6 is out of the range of uint
        int(18446744073709551615u) → ConversionRange | 3 | 18446744073709551615 is out of the range of int
        int(r / 0.0) → ConversionRange | 3 | +Inf is out of the range of int
        uint(0.0 / 0.0) → ConversionRange | 4 | NaN is out of the range of uint
        int('0x10') → InvalidArgument | 3 | invalid argument to 'int': cannot parse 0x10 as int
        uint('18446744073709551616') → ConversionRange | 4 | 18446744073709551616 is out of the range of uint
        double('2e308') → ConversionRange | 6 | 2e308 is out of the range of double
        bool('yes') → InvalidArgument | 4 | invalid argument to 'bool': cannot parse yes as bool
        string(b'\xc3') → InvalidArgument | 6 | invalid argument to 'string': invalid UTF-8 at byte 0
        !any → EvalNoMatchingOverload | 0 | no such overload: !_(int)
        any + 'a' → EvalNoMatchingOverload | 4 | no such overload: _+_(int, string)
        any < 'a' → EvalNoMatchingOverload | 4 | no such overload: _<_(int, string)
        dyn([1]) < dyn([2]) → EvalNoMatchingOverload | 9 | no such overload: _<_(list, list)
        int(dyn(null)) → EvalNoMatchingOverload | 3 | no such overload: int(null_type)
        items[3] → IndexOutOfRange | 5 | index 3 out of range [0, 3)
        items[-1] → IndexOutOfRange | 5 | index -1 out of range [0, 3)
        [1, 2, 3][5] → IndexOutOfRange | 9 | index 5 out of range [0, 3)
        items[dyn(3u)] → IndexOutOfRange | 5 | index 3 out of range [0, 3)
        items[dyn(-2.0)] → IndexOutOfRange | 5 | index -2 out of range [0, 3)
        items[dyn(1e19)] → IndexOutOfRange | 5 | index 1e+19 out of range [0, 3)
        items[dyn(1.5)] → InvalidArgument | 5 | invalid argument to '_[_]': index 1.5 is not an integer
        items[dyn(0.0 / 0.0)] → InvalidArgument | 5 | invalid argument to '_[_]': index NaN is not an integer
        items[dyn(-1.0 / 0.0)] → InvalidArgument | 5 | invalid argument to '_[_]': index -Inf is not an integer
        prices['pear'] → NoSuchKey | 6 | no such key: "pear"
        {1: 'a'}[2] → NoSuchKey | 8 | no such key: 2
        {1u: 'a'}[2u] → NoSuchKey | 9 | no such key: 2u
        {true: 'a'}[false] → NoSuchKey | 11 | no such key: false
        {1: 'a'}[dyn(1.5)] → NoSuchKey | 8 | no such key: 1.5
        {1: 'a'}[dyn([1])] → NoSuchKey | 8 | no such key: list
        dyn(1)[0] → EvalNoMatchingOverload | 6 | no such overload: _[_](int, int)
        dyn(items)['a'] → EvalNoMatchingOverload | 10 | no such overload: _[_](list, string)
        1 in dyn(2) → EvalNoMatchingOverload | 2 | no such overload: @in(int, int)
        size(any) → EvalNoMatchingOverload | 4 | no such overload: size(int)
        any.contains('a') → EvalNoMatchingOverload | 12 | no such overload: contains(int, string)
        [0, 1].all(x, 1 / x > 0) → DivisionByZero | 16 | division by zero
        [0, 2].exists_one(x, 2 / x == 1) → DivisionByZero | 23 | division by zero
        any.map(x, x) → EvalInvalidComprehensionRange | 0 | value of type 'int' cannot be the range of a comprehension (must be list or map)
        has(any.f) → FieldSelectionNotSupported | 3 | value of type 'int' does not support field selection of 'f'
        """;

    // A static type is no guarantee of the value: with texts: dyn bound to ['z'], [1] + texts has the type
    // list(int) and the element 'z' of the static type int reaches the implementations, which reject its kind.
    private const string WrongKinds = """
        ([1] + texts).map(e, e + 1) → 23 | no such overload: _+_(string, int)
        ([1] + texts).map(e, -e) → 21 | no such overload: -_(string)
        ([1] + texts).map(e, int(e)) → 24 | no such overload: int(string)
        ([1] + texts).map(e, double(e)) → 27 | no such overload: double(string)
        ([1] + texts).map(e, e < dyn(2)) → 23 | no such overload: _<_(string, int)
        ([true] + texts).map(e, !e) → 24 | no such overload: !_(string)
        ([1.5] + numbers).map(e, string(e)) → 31 | no such overload: string(uint)
        ([1.5] + numbers).map(e, int(e)) → 28 | no such overload: int(uint)
        ([b''] + texts).map(e, string(e)) → 29 | no such overload: string(string)
        ([1] + texts)[1] + 1 → 17 | no such overload: _+_(string, int)
        int(([1] + dyn(['a']))[1]) → 3 | no such overload: int(string)
        string(([1] + dyn([2u]))[1]) → 6 | no such overload: string(uint)
        !([true] + texts)[1] → 0 | no such overload: !_(string)
        ([[1]] + texts)[1][0] → 18 | no such overload: _[_](string, int)
        ([{'a': 1}] + texts)[1]['a'] → 23 | no such overload: _[_](string, string)
        2 in ([[1]] + texts)[1] → 2 | no such overload: @in(int, string)
        size(([[1]] + texts)[1]) → 4 | no such overload: size(string)
        ([b'a'] + texts)[1].size() → 24 | no such overload: size(string)
        (['a'] + numbers)[1].contains('a') → 29 | no such overload: contains(uint, string)
        (['a'] + numbers)[1].startsWith('a') → 31 | no such overload: startsWith(uint, string)
        (['a'] + numbers)[1].endsWith('a') → 29 | no such overload: endsWith(uint, string)
        """;

    // "expression → cost" of the whole evaluation: a variable costs 1, and n more for the check of a bound list or
    // map of n elements against its declared type — items 4, prices 3; a literal costs 0, and each call the row of
    // its overload — fixed units, or a part that grows with the arguments or the result. A macro costs its range,
    // 1 for each step and the expressions of its author; map and filter 1 for the new list and 1 for each element
    // they add.
    private const string Costs = """
        word == 'kiwi' → 3
        word == 'a long string' → 4
        [n, n] == [n, 6] → 12
        word + word → 5
        raw + raw → 5
        [n] + [n, n] → 12
        word < 'banana split' → 4
        raw >= raw → 4
        string(n) → 3
        string(n * 1000000000000) → 5
        string(1.25e-7 * r) → 4
        string(flag) → 3
        bytes(word) → 4
        bytes('ü ü ü ü ü') → 4
        string(raw) → 4
        int('12345678901') → 3
        bool('true') → 2
        !flag → 2
        type(n) == int → 4
        n + n → 3
        flag ? n : 0 → 2
        items[1] → 5
        [1, 2][0] → 4
        prices['kiwi'] → 5
        'a long key x' in prices → 6
        {'a long key x': 1}['a long key x'] → 8
        {1: 'a'}[1] → 4
        1 in items → 6
        3 in items → 8
        'kiwi' in ['apple', 'kiwi'] → 8
        'kiwi' in prices → 5
        size(items) → 5
        prices.size() → 4
        raw.size() → 2
        size(word) → 3
        size('abcdefghijk') → 3
        word.contains('iw') → 3
        word.startsWith('a long prefix') → 4
        word.endsWith('wi') → 3
        has(prices.kiwi) → 5
        items.all(x, x > 0) → 13
        items.exists(x, x == 1) → 7
        items.exists_one(x, x == 1) → 13
        items.map(x, x * 2) → 17
        items.filter(x, x > 1) → 16
        items.map(x, x > 1, x * 2) → 20
        prices.all(key, key != '') → 11
        """;

    // "<value of x> | <expression> → <expected>" with x: dyn, where expected is an expression whose value the result equals
    // or the code of the error. An index of an operand of the static type dyn is chosen by the kind of the value, as in
    // cel-go v0.32.0, though a uint or double key leaves type checking only index_map.
    private const string DynOperandIndexes = """
        [7] | x[0u] → 7
        [7] | x[0.0] → 7
        [7] | x[0.5] → InvalidArgument
        [7] | x[1u] → IndexOutOfRange
        [7] | x['a'] → EvalNoMatchingOverload
        {0: 1} | x[0u] → 1
        [7] | dyn([7])[0u] → 7
        """;

    private static readonly CelEnvironment DynOperandEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("x", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("n")
        .AddVariable<ulong>("k")
        .AddVariable<double>("r")
        .AddVariable<bool>("flag")
        .AddVariable<string>("word")
        .AddVariable<byte[]>("raw")
        .AddVariable("any", CelType.Dyn)
        .AddVariable("texts", CelType.Dyn)
        .AddVariable("numbers", CelType.Dyn)
        .AddVariable("items", CelType.ListOf(CelType.Int))
        .AddVariable("prices", CelType.MapOf(CelType.String, CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("n", 6L)
        .Add("k", 4UL)
        .Add("r", 0.5)
        .Add("flag", true)
        .Add("word", "kiwi")
        .Add("raw", Encoding.UTF8.GetBytes("kiwi"))
        .Add("any", 3L)
        .Add("texts", CelValue.FromList(CelList.Create(CelValue.FromString("z"))))
        .Add("numbers", CelValue.FromList(CelList.Create(CelValue.FromUInt64(7))))
        .Add("items", CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3))))
        .Add("prices", CelValue.FromMap(CelMap.Create([new(CelValue.FromString("apple"), CelValue.FromInt64(3)), new(CelValue.FromString("kiwi"), CelValue.FromInt64(5))])));

    public static TheoryData<string, string> ValueCases => CaseTable.Parse(Values);

    public static TheoryData<string, string> ErrorCases => CaseTable.Parse(Errors);

    public static TheoryData<string, string> WrongKindCases => CaseTable.Parse(WrongKinds);

    public static TheoryData<string, string> CostCases => CaseTable.Parse(Costs);

    public static TheoryData<string, string> DynOperandIndexCases => CaseTable.Parse(DynOperandIndexes);

    [Theory]
    [MemberData(nameof(ValueCases))]
    public static void Expression_HasItsValue(string expression, string expected)
    {
        CelValue value = Evaluate(expression, null).GetValueOrThrow();
        CelValue wanted = Evaluate(expected, null).GetValueOrThrow();

        Assert.Equal(wanted.Kind, value.Kind);
        Assert.True(CelValue.CelEquals(value, wanted).TryGetBoolean(out bool equal) && equal, expression);
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Expression_IsItsErrorAtItsPlace(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        string[] cells = expected.Split(" | ");
        CelError error = Assert.Single(Evaluate(expression, null).Errors);

        Assert.Equal(
            (Enum.Parse<CelErrorCode>(cells[0]), CelErrorStage.Evaluate, int.Parse(cells[1], CultureInfo.InvariantCulture), cells[2]),
            (error.Code, error.Stage, error.Location.Offset, error.Message));
    }

    [Theory]
    [MemberData(nameof(WrongKindCases))]
    public static void ValueOfAnotherKindUnderAStaticType_IsEvalNoMatchingOverload(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        string[] cells = expected.Split(" | ");
        CelError error = Assert.Single(Evaluate(expression, null).Errors);

        Assert.Equal(
            (CelErrorCode.EvalNoMatchingOverload, int.Parse(cells[0], CultureInfo.InvariantCulture), cells[1]),
            (error.Code, error.Location.Offset, error.Message));
    }

    // The cost is exact: the evaluation passes with it as the limit and stops before the operation one unit below.
    [Theory]
    [MemberData(nameof(CostCases))]
    public static void Expression_CostsItsUnits(string expression, string units)
    {
        ulong cost = ulong.Parse(units, CultureInfo.InvariantCulture);

        Assert.True(Evaluate(expression, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, cost - 1).Errors).Code);
    }

    // The same outcome with type checking and without it.
    [Theory]
    [MemberData(nameof(DynOperandIndexCases))]
    public static void IndexOfADynOperand_IsChosenByTheKindOfTheValue(string input, string expected)
    {
        ArgumentNullException.ThrowIfNull(input);
        string[] cells = input.Split(" | ");
        CelBindings bindings = new CelBindings().Add("x", DynOperandEnvironment.Evaluate<CelValue>(cells[0], new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
        CelEvalResult<CelValue> checkedResult = DynOperandEnvironment.Compile<CelValue>(cells[1]).GetProgramOrThrow()
            .Evaluate(bindings, null, CancellationToken.None);
        ParseResult parsed = DynOperandEnvironment.Parse(cells[1]);
        Assert.True(parsed.IsSuccess, input);
        CelEvalResult<CelValue> uncheckedResult = DynOperandEnvironment.EvaluateWithoutTypeCheck(parsed.Expression!, bindings);

        Assert.All([checkedResult, uncheckedResult], result =>
        {
            if (char.IsAsciiLetterUpper(expected[0]) && Enum.TryParse(expected, out CelErrorCode code))
            {
                Assert.Equal(code, Assert.Single(result.Errors).Code);
            }
            else
            {
                CelValue wanted = DynOperandEnvironment.Evaluate<CelValue>(expected, new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();
                Assert.Equal(wanted.Kind, result.Value.Kind);
                Assert.True(CelValue.CelEquals(result.Value, wanted).TryGetBoolean(out bool equal) && equal, input);
            }
        });
    }

    // Type checking is unchanged: a uint index of a list is no overload, as in cel-go v0.32.0.
    [Fact]
    public static void UintIndexOfAList_IsACheckError()
    {
        CelCompileResult<CelValue> compiled = DynOperandEnvironment.Compile<CelValue>("[7][0u]");

        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(compiled.Errors).Code);
    }

    // The index overload added to an operand of the static type dyn changes no cost.
    [Fact]
    public static void UintIndexOfADynOperand_CostsAsAnIntIndex()
    {
        CelBindings bindings = new CelBindings().Add("x", CelValue.FromList(CelList.Create(CelValue.FromInt64(7))));

        Assert.Equal(ChargedUnits("x[0]", bindings), ChargedUnits("x[0u]", bindings));
    }

    [Fact]
    public static void CrossNumericOrdering_WithoutDyn_IsACheckError()
    {
        CelCompileResult<CelValue> compiled = Environment.Compile<CelValue>("n < r");

        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(compiled.Errors).Code);
    }

    private static ulong ChargedUnits(string expression, CelBindings bindings)
    {
        Plan plan = DynOperandEnvironment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
        Assert.True(plan.Run(frame).TryGetInt64(out long value) && value == 7, expression);
        return frame.ChargedUnits;
    }

    private static CelEvalResult<CelValue> Evaluate(string expression, ulong? costLimit) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(Bindings, new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);
}
