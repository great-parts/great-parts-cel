using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;

namespace Great.Parts.Cel.Tests.Checking;

// The rules of the checker for the optional syntax and the optional types (docs/design/checking.md, "Optional syntax and optional
// types"): an optional operand is unwrapped once and the result wrapped, the selection in a presence test is never wrapped, the
// type of a selection that sits under a macro, the reference map of a selection over an optional and of the call _?._, the
// validator of select_optional_field on trees that the parser does not write, the entries of a message literal against
// optional_type of the field, the range and the operands that are optional, and the type of a literal that joins optional types.
public sealed class OptionalSyntaxCheckingTests
{
    private const string Types = """
        om.a => optional_type(int)
        om.?a => optional_type(int)
        [{'a': 1}].map(x, x.?a) => list(optional_type(int))
        [d].map(x, x.?a) => list(optional_type(dyn))
        [].map(x, x.?a) => list(optional_type(dyn))
        [om].map(x, x.a) => list(optional_type(int))
        has(om.a) => bool
        has(d.a) => bool
        has(optional.of(d).a) => bool
        optional.of(TestAllTypes{}).single_nested_message.bb => optional_type(int)
        optional.of(TestAllTypes{}).?single_nested_message.bb => optional_type(int)
        TestAllTypes{}.?single_nested_message.?bb => optional_type(int)
        [?o, ?optional.of(dyn(1))] => list(dyn)
        [?d] => list(dyn)
        [?o, 1] => list(int)
        {?'a': o, 'b': 2} => map(string, int)
        {?'a': d} => map(string, dyn)
        [null, optional.of(1)][0] => optional_type(int)
        [optional.of(1), null][0] => optional_type(int)
        [optional.none(), optional.of(1)] => list(optional_type(int))
        [optional.of(1), optional.none()] => list(optional_type(int))
        [optional.of(1), optional.of(dyn(1))] => list(optional_type(dyn))
        [optional.of(dyn(1)), optional.of(1)] => list(optional_type(dyn))
        true ? optional.of(dyn(1)) : optional.of(1) => optional_type(dyn)
        true ? optional.of(1) : optional.of(dyn(1)) => optional_type(dyn)
        [type(optional.of(1)), type(optional.of(2))] => list(type(optional_type(int)))
        [type(optional.of(1)), type(optional.of('a'))] => list(type(dyn))
        type(optional.of(1)) == type(optional.of('a')) => bool
        """;

    // An optional operand is unwrapped once: the head of what it holds decides, and an optional that holds an optional has no
    // fields. The place is the "." of the selection, the same for ".?" and for a plain selection.
    private const string Errors = """
        Check | oo^.?a | NoSuchField | undefined field 'a' in type 'optional_type(map(string, int))'
        Check | oo^.a | NoSuchField | undefined field 'a' in type 'optional_type(map(string, int))'
        Check | om.?a^.?b | NoSuchField | undefined field 'b' in type 'int'
        Check | om.a^.b | NoSuchField | undefined field 'b' in type 'int'
        Check | ^o.all(x, true) | InvalidComprehensionRange | expression of type 'optional_type(int)' cannot be the range of a comprehension (must be list, map, or dyn)
        Check | ^o && true | TypeMismatch | expected type 'bool' but found 'optional_type(int)'
        Check | true || ^o | TypeMismatch | expected type 'bool' but found 'optional_type(int)'
        """;

    private static readonly CelEnvironment Messages = ProtobufExample.Builder3()
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("om", CelType.OptionalOf(CelType.MapOf(CelType.String, CelType.Int)))
        .AddVariable("oo", CelType.OptionalOf(CelType.OptionalOf(CelType.MapOf(CelType.String, CelType.Int))))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("name", CelType.String)
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment Objects = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddTypes(new SampleProvider("crm", [ContactType], [Status]))
        .AddVariable("c", ContactType.Type)
        .UseContainer("acme.crm")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public static void TypesOfTheOptionalForms_FollowTheRulesOfTheTables()
    {
        var failures = new List<string>();
        foreach (string row in ExtensionRows.Lines(Types))
        {
            string[] sides = row.Split(" => ");
            CelCompileResult<CelValue> compiled = Messages.Compile<CelValue>(sides[0]);
            if (!compiled.IsSuccess)
            {
                failures.Add(row + ": " + compiled.Errors[0].Code + " " + compiled.Errors[0].Message);
            }
            else if (compiled.GetProgramOrThrow().ResultType.ToString() != sides[1])
            {
                failures.Add(row + ": " + compiled.GetProgramOrThrow().ResultType);
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public static void OptionalOperandAndOptionalMistakes_AreErrorsOfTheCheckAtTheirPlaces() =>
        Assert.Empty(ExtensionRows.CheckErrors(Messages, Errors));

    // A field of an object of the application: the type of the selection is that of the field, wrapped, for ".?" and for an optional
    // operand, and bool for a presence test.
    [Theory]
    [InlineData("c.?age", "optional_type(int)")]
    [InlineData("c.?manager", "optional_type(acme.crm.Contact)")]
    [InlineData("c.?manager.age", "optional_type(int)")]
    [InlineData("c.?manager.?manager.?score", "optional_type(double)")]
    [InlineData("optional.of(c).manager.name", "optional_type(string)")]
    [InlineData("has(optional.of(c).age)", "bool")]
    [InlineData("has(c.?manager.age)", "bool")]
    public static void SelectionOfAnObjectOfTheApplication_HasTheTypeOfTheField(string expression, string type)
    {
        CelCompileResult<CelValue> compiled = Objects.Compile<CelValue>(expression);

        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Errors.Select(error => error.Message)));
        Assert.Equal(type, compiled.GetProgramOrThrow().ResultType.ToString());
    }

    [Theory]
    [InlineData("c.?nothing", 1, "undefined field 'nothing' in type 'acme.crm.Contact'")]
    [InlineData("c.?manager.nothing", 10, "undefined field 'nothing' in type 'acme.crm.Contact'")]
    [InlineData("optional.of(c).nothing", 14, "undefined field 'nothing' in type 'acme.crm.Contact'")]
    public static void FieldThatTheObjectTypeDoesNotHave_IsNoSuchFieldAtTheSelectionOfAnOptional(string expression, int dot, string message)
    {
        CelError error = Assert.Single(Objects.Compile<CelValue>(expression).Errors);

        Assert.Equal((CelErrorCode.NoSuchField, dot, message), (error.Code, error.Location.Offset, error.Message));
    }

    // The reference map: a selection of an object that is not optional records the description and the field number, which the
    // planner binds; a selection over an optional operand records none, since the description of the value is known at evaluation,
    // and the call _?._ records its one candidate and no field.
    [Fact]
    public static void ReferenceOfASelection_IsRecordedOnlyForAnOperandThatIsNotOptional()
    {
        CheckedExpression plain = Messages.Check(Parse("TestAllTypes{}.single_int32")).Expression!;
        CheckedExpression overOptional = Messages.Check(Parse("optional.of(TestAllTypes{}).single_int32")).Expression!;
        CheckedExpression optionalSelection = Messages.Check(Parse("TestAllTypes{}.?single_int32")).Expression!;

        Reference field = plain.References[plain.Parsed.Root.Id];
        Assert.NotNull(field.ObjectType);
        Assert.False(overOptional.References.ContainsKey(overOptional.Parsed.Root.Id));

        Reference call = optionalSelection.References[optionalSelection.Parsed.Root.Id];
        Assert.Null(call.ObjectType);
        Assert.Equal("select_optional_field", Assert.Single(call.Candidates).Declaration.OverloadId);
    }

    // The validator of select_optional_field: the name of the field has to be a string literal, which the parser always writes. A
    // tree with another second argument is NoMatchingOverload with the usual text, at the call, after the traversal; one that has a
    // literal is typed by the rule of the selection.
    [Fact]
    public static void SecondArgumentThatIsNotAStringLiteral_IsNoMatchingOverloadAtTheCall()
    {
        var nodes = new NodeFactory();
        ExprNode call = nodes.Call("_?._", null, [nodes.Ident("m", 0), nodes.Ident("name", 5)], 1);

        CheckResult result = Messages.Check(nodes.Build(call, new LineTable("m.?name_")));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.NoMatchingOverload, 1, "found no matching overload for '_?._' applied to '(map(string, int), string)'"),
            (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public static void SecondArgumentThatIsNotAString_IsNoMatchingOverloadOfTheCandidates()
    {
        var nodes = new NodeFactory();
        ExprNode call = nodes.Call("_?._", null, [nodes.Ident("m", 0), nodes.Constant(CelValue.FromInt64(1), 5)], 1);

        CheckResult result = Messages.Check(nodes.Build(call, new LineTable("m.?name_")));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.NoMatchingOverload, 1), (error.Code, error.Location.Offset));
        Assert.Equal("found no matching overload for '_?._' applied to '(map(string, int), int)'", error.Message);
    }

    [Fact]
    public static void SecondArgumentThatIsAStringLiteral_IsTypedByTheRuleOfTheSelection()
    {
        var nodes = new NodeFactory();
        ExprNode call = nodes.Call("_?._", null, [nodes.Ident("m", 0), nodes.Constant(CelValue.FromString("a"), 5)], 1);

        CheckResult result = Messages.Check(nodes.Build(call, new LineTable("m.?a")));

        Assert.True(result.IsSuccess);
        Assert.Equal("optional_type(int)", result.Expression!.ResultType.ToString());
    }

    // A message literal of a type that no expression can create: the optional entry is no more checked than the plain ones, and its
    // field names are not looked up.
    [Fact]
    public static void OptionalEntryOfATypeThatIsNotConstructible_IsTypeNotConstructibleAlone()
    {
        CelError error = Assert.Single(Objects.Compile<CelValue>("Contact{?name: 1}").Errors);

        Assert.Equal((CelErrorCode.TypeNotConstructible, 7), (error.Code, error.Location.Offset));
    }

    // The value of an optional entry has to be assignable to optional_type of the field: a dyn passes the check and is answered by
    // the evaluation, as the optional elements of a list are.
    [Theory]
    [InlineData("TestAllTypes{?single_int32: d}")]
    [InlineData("TestAllTypes{?single_int64: optional.of(dyn(1))}")]
    [InlineData("TestAllTypes{?single_double_wrapper: optional.ofNonZeroValue(0.0)}")]
    [InlineData("TestAllTypes{?single_any: optional.of(1)}")]
    [InlineData("TestAllTypes{?single_value: optional.of('a')}")]
    [InlineData("TestAllTypes{?repeated_int64: optional.of([1, 2])}")]
    [InlineData("TestAllTypes{?map_string_int64: optional.of({'a': 1})}")]
    public static void OptionalEntryOfAMessage_AcceptsTheTypesThatAreAssignableToTheOptionalOfTheField(string expression)
    {
        CelCompileResult<CelValue> compiled = Messages.Compile<CelValue>(expression);

        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Errors.Select(error => error.Message)));
    }

    // A null is no optional: its static type is assignable to optional_type of the field by the legacy rule, yet the entry is a
    // mismatch at its colon, as [?null] is, before the field is looked at.
    [Fact]
    public static void OptionalEntryOfAMessageWithNull_IsTypeMismatchAtTheEntry()
    {
        CelError error = Assert.Single(Messages.Compile<CelValue>("TestAllTypes{?single_int32: null}").Errors);

        Assert.Equal(
            (CelErrorCode.TypeMismatch, 26, "expected type 'optional_type(int)' but found 'null_type'"),
            (error.Code, error.Location.Offset, error.Message));
    }

    // The four cases of type_deduction with tuple(T, U, V), whose types are the join and the deduction of an abstract type with
    // parameters: the first one in both orders. The functions are those of the cases, declared with an abstract type that the sets
    // make and the public API does not.
    [Theory]
    [InlineData("[tuple(1, 2u, 3.0), tuple(dyn(1), dyn(2u), dyn(3.0))][0]", "tuple(dyn, dyn, dyn)")]
    [InlineData("[tuple(dyn(1), dyn(2u), dyn(3.0)), tuple(1, 2u, 3.0)][0]", "tuple(dyn, dyn, dyn)")]
    [InlineData("sort(tuple(dyn(1), 2u, 3.0))", "tuple(dyn, dyn, dyn)")]
    [InlineData("tuple(1, 2u, 3.0) == tuple(1, dyn(2u), dyn(3.0))", "bool")]
    [InlineData("tuple(dyn(1), dyn(2u), 3.0) == tuple(1, 2u, 3.0)", "bool")]
    public static void TupleCasesOfTypeDeduction_HaveTheTypesOfTheTestData(string expression, string type)
    {
        CelType t = CelType.TypeParameter("T");
        CelType u = CelType.TypeParameter("U");
        CelType v = CelType.TypeParameter("V");
        var index = CheckingIndex.Create(
            [
                CheckingIndex.Global("tuple", "tuple_T_U_V", [t, u, v], CelType.AbstractNamed("tuple", [t, u, v])),
                CheckingIndex.Global("sort", "sort_tuple_T_T_T", [CelType.AbstractNamed("tuple", [t, t, t])], CelType.AbstractNamed("tuple", [t, t, t])),
            ],
            []);

        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), index);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(type, result.Expression!.ResultType.ToString());
    }

    private static ParsedExpression Parse(string expression)
    {
        ParseResult parsed = Messages.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        return parsed.Expression;
    }
}
