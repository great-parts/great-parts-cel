using System;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// The rules per node of gradual type checking: type parameters and their substitution, overload
// candidates, receiver calls, field selection, indexes, comprehensions and their variables, and type
// identifiers.
public sealed class GradualTypingTests
{
    private static readonly CelType T = CelType.TypeParameter("T");

    // "<expression> → <type of the expression>" in the index Declared.
    private const string Types = """
        nums[1] → int
        counts['k'] → int
        counts.k → int
        {2u: 'v'}.missing → string
        has(counts.k) → bool
        has(any.k) → bool
        any.k → dyn
        any['k'] → dyn
        any[1].k → dyn
        'text'.size() → int
        size('text') → int
        size(nums) → int
        [] + nums → list(int)
        nums + [] → list(int)
        [[1], []] → list(list(int))
        [[], [], []] → list(list(dyn))
        [[], [1], []] → list(list(int))
        [[], ['a'], [1]] → list(dyn)
        [nums, [any]] → list(list(dyn))
        true ? n : 2 → int
        true ? w : 2 → wrapper(int)
        true ? null : w → wrapper(int)
        head(nums) → int
        head([]) → dyn
        head([[]]) → list(dyn)
        pick('x', int) → int
        pick('x', list) → list(dyn)
        int → type(int)
        list → type(list(dyn))
        type → type(type(dyn))
        type(n) == string → bool
        true ? type(1) : type('a') → type(dyn)
        [int, type([])] → list(type(dyn))
        nums.map(e, e * 2) → list(int)
        nums.map(e, e < 2, 'a') → list(string)
        nums.filter(e, e < 2) → list(int)
        counts.map(k, k) → list(string)
        nums.all(e, e < 2) → bool
        nums.exists(e, e == 2) → bool
        nums.exists_one(e, e == 2) → bool
        any.all(e, e) → bool
        [].map(x, x) → list(dyn)
        [].map(x, x.map(y, y)) → list(list(dyn))
        [].map(x, x.f) → list(dyn)
        [[1]].map(l, l.map(e, e + 1)) → list(list(int))
        [].exists(e, type(e) == int && e == 'a') → bool
        [].exists(e, e == 'a' && type(e) == int) → bool
        dyn(1) < 1.5 → bool
        1 < 2 → bool
        """;

    // "<expression> → <code> <offset> <message>" in the index Declared.
    private const string Errors = """
        true ? 1 : 'a' → 2001 5 found no matching overload for '_?_:_' applied to '(bool, int, string)'
        1 < 1.5 → 2001 2 found no matching overload for '_<_' applied to '(int, double)'
        nums[1u] → 2001 4 found no matching overload for '_[_]' applied to '(list(int), uint)'
        head(1) → 2001 4 found no matching overload for 'head' applied to '(int)'
        1.size() → 2001 6 found no matching overload for 'size' applied to '(int)'
        nums.size() → 2001 9 found no matching overload for 'size' applied to '(list(int))'
        n.f → 2004 1 undefined field 'f' in type 'int'
        nums.f → 2004 4 undefined field 'f' in type 'list(int)'
        w.f → 2004 1 undefined field 'f' in type 'wrapper(int)'
        has(n.f) → 2004 3 undefined field 'f' in type 'int'
        has(counts.k.f) → 2004 3 undefined field 'f' in type 'int'
        n.unknown() → 2000 9 undeclared reference to 'unknown' (in container '')
        Message{} → 2000 7 undeclared reference to 'Message' (in container '')
        .pkg.Message{f: n} → 2000 12 undeclared reference to '.pkg.Message' (in container '')
        """;

    private static readonly DeclarationIndex Declared = CheckingIndex.Create(
        [
            CheckingIndex.Global("head", "head_list", [CelType.ListOf(T)], T),
            CheckingIndex.Global("pick", "pick_type", [CelType.Dyn, CelType.TypeOf(T)], T),
        ],
        [
            new CelVariableDecl("n", CelType.Int),
            new CelVariableDecl("w", CelType.WrapperOf(CelType.Int)),
            new CelVariableDecl("any", CelType.Dyn),
            new CelVariableDecl("nums", CelType.ListOf(CelType.Int)),
            new CelVariableDecl("counts", CelType.MapOf(CelType.String, CelType.Int)),
        ]);

    public static TheoryData<string, string> TypeCases => CaseTable.Parse(Types);

    public static TheoryData<string, string> ErrorCases => CaseTable.Parse(Errors);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public static void Expression_HasItsStaticType(string expression, string type)
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), Declared);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_HasItsCodeOffsetAndMessage(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        CelError error = Assert.Single(Checker.Check(CheckingIndex.Parse(expression), Declared).Errors);

        string[] parts = expected.Split(' ', 3);
        Assert.Equal(
            (parts[0], CelErrorStage.Check, int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), parts[2]),
            (((int)error.Code).ToString(System.Globalization.CultureInfo.InvariantCulture), error.Stage, error.Location.Offset, error.Message));
    }

    // No type map entry keeps a type variable: the free ones are dyn, the bound ones their binding.
    [Theory]
    [InlineData("head([])")]
    [InlineData("[] + nums")]
    [InlineData("[].map(x, [x, {}])")]
    [InlineData("pick(any, type([]))")]
    public static void TypeMap_HasNoTypeParameters(string expression)
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), Declared);

        Assert.True(result.IsSuccess);
        Assert.All(result.Expression.Types.Values, type => Assert.False(HasTypeParameter(type), type.ToString()));
    }

    // Each empty list binds the variable of the elements so far to its own, a chain as long as the list; every element
    // takes the type at the end of the chain.
    [Theory]
    [InlineData("[[], [], []]", "list(dyn) list(dyn) list(dyn)")]
    [InlineData("[[], [1], []]", "list(int) list(int) list(int)")]
    [InlineData("[[], ['a'], [1]]", "list(string) list(string) list(int)")]
    public static void ElementsOfAListOfLists_TakeTheirTypesAfterTheSubstitution(string expression, string types)
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), Declared);

        Assert.True(result.IsSuccess);
        var list = (ListNode)result.Expression.Parsed.Root;
        Assert.Equal(types, string.Join(' ', list.Elements.Select(element => result.Expression.Types[element.Id].ToString())));
    }

    [Fact]
    public static void EmptyLiteral_TakesTheTypeThatTheExpressionBindsItsVariableTo()
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse("[] + nums"), Declared);

        Assert.True(result.IsSuccess);
        var call = (CallNode)result.Expression.Parsed.Root;
        Assert.Equal(CelType.ListOf(CelType.Int), result.Expression.Types[call.Arguments[0].Id]);
    }

    // A dyn operand matches every overload of the index; all are candidates, in the order of the index.
    [Theory]
    [InlineData("any[1]", "index_list index_map")]
    [InlineData("nums[1]", "index_list")]
    [InlineData("dyn(1) < 1.5", "less_int64_double")]
    [InlineData("any < 1", "less_int64")]
    [InlineData("any < any", "less_int64 less_int64_double")]
    public static void Candidates_AreTheAcceptingOverloadsInIndexOrder(string expression, string overloadIds)
    {
        ArgumentNullException.ThrowIfNull(overloadIds);

        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), Declared);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            overloadIds.Split(' '),
            result.Expression.References[result.Expression.Parsed.Root.Id].Candidates.Select(candidate => candidate.Declaration.OverloadId));
    }

    [Fact]
    public static void ReceiverCall_TakesTheReceiverOverloads()
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse("'text'.size()"), Declared);

        Assert.True(result.IsSuccess);
        Reference reference = result.Expression.References[result.Expression.Parsed.Root.Id];
        Assert.Equal("string_size", Assert.Single(reference.Candidates).Declaration.OverloadId);
    }

    // The variables of a comprehension are locals: the identifiers that read them refer to them by name.
    [Fact]
    public static void IdentifiersOfAComprehension_ReferToItsVariables()
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse("nums.all(e, e < 2)"), Declared);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["@result", "e"],
            result.Expression.References.Values.Select(reference => reference.Local).Where(local => local is not null).Distinct().Order(StringComparer.Ordinal));
        Assert.DoesNotContain(result.Expression.References.Values, reference => reference.Variable?.Name == "e");
    }

    [Fact]
    public static void TypeIdentifier_RefersToItsTypeValue()
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse("list"), Declared);

        Assert.True(result.IsSuccess);
        Reference reference = result.Expression.References[result.Expression.Parsed.Root.Id];
        Assert.Equal((CelType.ListOf(CelType.Dyn), null), (reference.TypeValue, reference.Variable));
    }

    // An invalid range is an error at the range, and its variable has the type error, so its uses make no
    // further error; the comprehension still has the type of its result.
    [Theory]
    [InlineData("1.all(x, true)", 0, "int")]
    [InlineData("'abc'.map(c, c + 1)", 0, "string")]
    [InlineData("true ? b'x'.exists(c, c) : false", 7, "bytes")]
    [InlineData("nums.all(x, 2u.exists(y, y))", 12, "uint")]
    public static void RangeThatIsNoListMapOrDyn_IsInvalidComprehensionRange(string expression, int offset, string type)
    {
        CelError error = Assert.Single(Checker.Check(CheckingIndex.Parse(expression), Declared).Errors);

        Assert.Equal(
            (CelErrorCode.InvalidComprehensionRange, offset, "expression of type '" + type + "' cannot be the range of a comprehension (must be list, map, or dyn)"),
            (error.Code, error.Location.Offset, error.Message));
    }

    // A free type parameter as a range or an operand of a selection is bound to dyn, so a later use
    // as another type takes dyn.
    [Theory]
    [InlineData("[].map(x, [x.f, x]) + [[1]]", "list(list(dyn))")]
    [InlineData("[].map(x, x.all(y, y))", "list(bool)")]
    public static void FreeTypeParameterAsRangeOrOperand_IsBoundToDyn(string expression, string type)
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), Declared);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    // With the optional syntax, an optional element or entry value is an optional_type or dyn; no value
    // has an optional type before the optional set, so any other type is TypeMismatch at the value.
    [Theory]
    [InlineData("[?any, 1]", "list(dyn)")]
    [InlineData("{?'k': any}", "map(string, dyn)")]
    public static void OptionalElementOfTypeDyn_IsChecked(string expression, string type)
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse(expression, OptionalSyntax), Declared);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    [Theory]
    [InlineData("[1, ?n]", 5, "int")]
    [InlineData("{'k': 1, ?'j': nums}", 15, "list(int)")]
    public static void OptionalElementOfAnotherType_IsTypeMismatch(string expression, int offset, string type)
    {
        CelError error = Assert.Single(Checker.Check(CheckingIndex.Parse(expression, OptionalSyntax), Declared).Errors);

        Assert.Equal(
            (CelErrorCode.TypeMismatch, offset, "expected type 'optional_type(" + type + ")' but found '" + type + "'"),
            (error.Code, error.Location.Offset, error.Message));
    }

    private static SyntaxOptions OptionalSyntax => new(OptionalSyntax: true, MacroTable.Standard);

    private static bool HasTypeParameter(CelType type) =>
        type.Kind == CelTypeKind.TypeParameter || type.Parameters.Any(HasTypeParameter);
}
