using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;

namespace Great.Parts.Cel.Tests.Evaluation;

// The nodes of the optional syntax (docs/design/evaluation.md, "Optional values and the optional syntax"): the optional selection
// over a map, a JSON object and an object of the application, the chains of selections, the literals with optional positions under
// macros and with keys that are refused, the fields of a message of each kind, the units and the place of a cost limit, an event of
// cancellation, and the same forms without type checking. The behavior that the examples fix is not repeated.
public sealed class OptionalSyntaxEvaluationTests : IDisposable
{
    private const string Chains = """
        {'a': {'b': {'c': 1}}}.?a.?b.?c => optional.of(1)
        {'a': {'b': {'c': 1}}}.?a.?x.?c => optional.none()
        {'a': {'b': {'c': 1}}}.?a.b.c => optional.of(1)
        {'a': {'b': {'c': 1}}}.?a.b.z => optional.none()
        {'a': {'b': {'c': 1}}}.?x.b.c => optional.none()
        {'a': {'b': {'c': 1}}}.?a.?b.?c.orValue(0) => 1
        {'a': {'b': {'c': 1}}}.?a.?b.?z.orValue(0) => 0
        has({'a': {'b': {'c': 1}}}.?a.b.c) => true
        has({'a': {'b': {'c': 1}}}.?a.b.z) => false
        has({'a': {'b': {'c': 1}}}.?x.b.c) => false
        """;

    private const string UnderMacros = """
        [1, 2, 3].map(x, [?optional.of(x), ?optional.none()]) => [[1], [2], [3]]
        [1, 2].map(x, {?'k': optional.of(x), ?'z': optional.none()}) => [{'k': 1}, {'k': 2}]
        [{'a': 1}, {}].map(m, m.?a) => [optional.of(1), optional.none()]
        [{'a': 1}, {'a': 0}, {}].map(m, m.?a.orValue(-1)) => [1, 0, -1]
        [1, 2, 3].filter(x, [?optional.ofNonZeroValue(x - 1)].size() == 1) => [2, 3]
        [{'a': [1, 2]}, {}].map(m, m.?a[?1]) => [optional.of(2), optional.none()]
        """;

    private const string Keys = """
        {?1.5: optional.none(), 'a': 1} => {'a': 1}
        {?true: optional.of(1), ?false: optional.none()} => {true: 1}
        {'a': 1, ?'b': optional.of(2), ?'a': optional.none()} => {'a': 1, 'b': 2}
        """;

    private const string KeyErrors = """
        Evaluate | {?^1.5: optional.of(1)} | UnsupportedMapKeyType | unsupported map key type 'double'
        Evaluate | {'a': 1, ?^1.5: optional.of(1)} | UnsupportedMapKeyType | unsupported map key type 'double'
        Evaluate | ^{'a': 1, ?'a': optional.of(2)} | DuplicateMapKey | duplicate map key: "a"
        Evaluate | ^{?'a': optional.of(1), ?'a': optional.of(2)} | DuplicateMapKey | duplicate map key: "a"
        """;

    private const string Json = """
        j.?a => optional.of(1.0)
        j.?z => optional.none()
        j.?b.c => optional.of(2.0)
        j.?b.?c => optional.of(2.0)
        j.?b.z => optional.none()
        has(j.?b.c) => true
        has(j.?b.z) => false
        j[?'a'] => optional.of(1.0)
        j[?'z'] => optional.none()
        j.?l[?1] => optional.of(20.0)
        j.?l[?5] => optional.none()
        optional.of(j)[?'a'] => optional.of(1.0)
        optional.of(j)['z'] => optional.none()
        """;

    private const string JsonErrors = """
        Evaluate | r^.?a | DuplicateMapKey | duplicate map key: "a"
        Evaluate | r^[?'a'] | DuplicateMapKey | duplicate map key: "a"
        Evaluate | optional.of(r)^.a | DuplicateMapKey | duplicate map key: "a"
        """;

    private const string Fields = """
        TestAllTypes{?single_int64_wrapper: optional.of(5)}.single_int64_wrapper => 5
        TestAllTypes{?single_int64_wrapper: optional.none()}.single_int64_wrapper => null
        TestAllTypes{?repeated_int64: optional.of([1, 2])}.repeated_int64 => [1, 2]
        TestAllTypes{?repeated_int64: optional.none()}.repeated_int64 => []
        TestAllTypes{?map_string_int64: optional.of({'a': 1})}.map_string_int64 => {'a': 1}
        TestAllTypes{?standalone_enum: optional.of(1)}.standalone_enum => 1
        TestAllTypes{?single_nested_enum: optional.of(2)}.single_nested_enum => 2
        TestAllTypes{?single_value: optional.of(1.5)}.single_value => 1.5
        TestAllTypes{?single_bytes: optional.of(b'ab')}.single_bytes => b'ab'
        TestAllTypes{?single_timestamp: optional.of(timestamp(0))}.single_timestamp == timestamp(0) => true
        TestAllTypes{?single_duration: optional.of(duration('1s'))}.single_duration == duration('1s') => true
        TestAllTypes{?single_int32: optional.of(1), ?single_int32: optional.of(2)}.single_int32 => 2
        TestAllTypes{?single_int32: optional.of(1), ?single_int32: optional.none()}.single_int32 => 1
        TestAllTypes{single_int32: 1, ?single_int32: optional.none()}.single_int32 => 1
        """;

    private const string FieldErrors = """
        Evaluate | TestAllTypes{?single_int32: ^d} | EvalNoMatchingOverload | no such overload: ?(string)
        Evaluate | TestAllTypes{?single_int32: optional.none(), ?single_string: ^d} | EvalNoMatchingOverload | no such overload: ?(string)
        Evaluate | TestAllTypes{?single_int32: dyn^(null)} | EvalNoMatchingOverload | no such overload: ?(null_type)
        """;

    private const string Objects = """
        c.?age => optional.of(30)
        lonely.?age => optional.none()
        c.?score => optional.of(1.5)
        c.?manager.age => optional.of(50)
        c.?manager.?age => optional.of(50)
        lonely.?manager.hasValue() => true
        lonely.?manager.value() => null
        optional.of(c).age => optional.of(30)
        optional.of(lonely).age => optional.none()
        has(optional.of(c).age) => true
        has(optional.of(lonely).age) => false
        has(c.?manager.age) => true
        has(optional.none().age) => false
        """;

    private const string ObjectErrors = """
        Evaluate | lonely.?manager^.age | FieldSelectionNotSupported | value of type 'null_type' does not support field selection of 'age'
        Evaluate | broken^.?age | ApplicationException | application code threw System.InvalidOperationException in field 'age' of type 'acme.crm.Contact'
        """;

    private const string WithoutTypeCheck = """
        {'a': 1}.?a => optional.of(1)
        {'a': 1}.?b => optional.none()
        optional.of({'a': 1}).a => optional.of(1)
        optional.of({'a': 1}).?b => optional.none()
        optional.none().?a => optional.none()
        [?optional.of(1), 2] => [1, 2]
        [1, ?optional.none()] => [1]
        {?'a': optional.none(), 'b': 1} => {'b': 1}
        {?'a': optional.of(1)} => {'a': 1}
        [1, 2][?5] => optional.none()
        [1, 2][?1] => optional.of(2)
        optional.of([1, 2])[1] => optional.of(2)
        {'a': 1}[?'a'] => optional.of(1)
        j.?a => optional.of(1.0)
        j.?b.c => optional.of(2.0)
        """;

    private static readonly CelEnvironment Maps = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("d", CelType.Dyn)
        .AddVariable("j", CelType.Dyn)
        .AddVariable("r", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment Messages = ProtobufExample.Builder3()
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment Described = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddTypes(new SampleProvider("crm", [ContactType], [Status]))
        .AddVariable("c", ContactType.Type)
        .AddVariable("lonely", ContactType.Type)
        .AddVariable("broken", ContactType.Type)
        .UseContainer("acme.crm")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly JsonDocument document = JsonDocument.Parse("""{"a": 1, "b": {"c": 2}, "l": [10, 20, 30]}""");
    private readonly JsonDocument repeated = JsonDocument.Parse("""{"a": 1, "a": 2}""");

    public void Dispose()
    {
        document.Dispose();
        repeated.Dispose();
    }

    [Fact]
    public void ChainsOfSelections_UnwrapOnceAndStayOptional() => Assert.Empty(ExtensionRows.CheckValues(Maps, Chains, MapBindings()));

    [Fact]
    public void OptionalFormsUnderMacros_AreEvaluatedAtEachStep() => Assert.Empty(ExtensionRows.CheckValues(Maps, UnderMacros, MapBindings()));

    [Fact]
    public void KeyOfADroppedEntry_IsNotLookedAt() => Assert.Empty(ExtensionRows.CheckValues(Maps, Keys, MapBindings()));

    [Fact]
    public void KeysOfStoredEntries_GetTheChecksOfAPlainMap_AndAValueThatIsNoOptionalIsAnError() =>
        Assert.Empty(ExtensionRows.CheckErrors(Maps, KeyErrors, MapBindings()));

    [Fact]
    public void JsonObject_IsSelectedAndIndexedAsAMap() => Assert.Empty(ExtensionRows.CheckValues(Maps, Json, MapBindings()));

    [Fact]
    public void JsonObjectThatRepeatsAName_IsDuplicateMapKeyWhereTheNameIsLookedUp() =>
        Assert.Empty(ExtensionRows.CheckErrors(Maps, JsonErrors, MapBindings()));

    [Fact]
    public void FieldsOfEveryKind_AreSetFromAnOptionalAndLeftUnsetByAnEmptyOne() =>
        Assert.Empty(ExtensionRows.CheckValues(Messages, Fields, new CelBindings()));

    [Fact]
    public void ValueOfAnOptionalField_ThatIsNoOptional_IsAnErrorAtTheValue() =>
        Assert.Empty(ExtensionRows.CheckErrors(Messages, FieldErrors, new CelBindings().Add("d", CelValue.FromString("x"))));

    [Fact]
    public void ObjectOfTheApplication_IsReadByTheDescriptionOfTheValue() =>
        Assert.Empty(ExtensionRows.CheckValues(Described, Objects, ContactBindings()));

    [Fact]
    public void HeldValueWithoutFields_AndFieldThatThrows_AreErrorsAtTheSelection() =>
        Assert.Empty(ExtensionRows.CheckErrors(Described, ObjectErrors, ContactBindings()));

    // The same text without type checking: the forms are chosen by the values, and the results are those of the checked plan.
    [Fact]
    public void OptionalForms_WorkWithoutTypeChecking()
    {
        var failures = new List<string>();
        foreach (string row in ExtensionRows.Lines(WithoutTypeCheck))
        {
            string[] sides = row.Split(" => ");
            ParseResult parsed = Maps.Parse(sides[0]);
            CelEvalResult<CelValue> actual = Maps.EvaluateWithoutTypeCheck(parsed.Expression!, MapBindings());
            CelValue wanted = Maps.Compile<CelValue>(sides[1]).GetProgramOrThrow().Evaluate(MapBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
            if (!actual.IsSuccess)
            {
                failures.Add(row + ": " + actual.Errors[0].Code);
            }
            else if (!CelValue.CelEquals(actual.GetValueOrThrow(), wanted).TryGetBoolean(out bool same) || !same)
            {
                failures.Add(row + ": another value");
            }
        }

        Assert.Empty(failures);
    }

    // A held value that is no map and no object, which the types of an expression without a check do not rule out: the error of the
    // selection of that value, at the selection.
    [Fact]
    public void WithoutTypeChecking_SelectionOfAHeldValueWithoutFields_IsFieldSelectionNotSupported()
    {
        ParseResult parsed = Maps.Parse("optional.of({'a': 1}).a.b");

        CelEvalResult<CelValue> result = Maps.EvaluateWithoutTypeCheck(parsed.Expression!, MapBindings());

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.FieldSelectionNotSupported, 23, "value of type 'int' does not support field selection of 'b'"),
            (error.Code, error.Location.Offset, error.Message));
    }

    // The selection of an optional is charged as the selection of the value: a JSON object walks all its properties, a JSON array
    // charges its length at an index, and the optional index charges what the plain one does.
    [Theory]
    [InlineData("j.?a", "j.a")]
    [InlineData("j.?b.c", "j.b.c")]
    [InlineData("j[?'a']", "j['a']")]
    [InlineData("j.l[?1]", "j.l[1]")]
    [InlineData("j.l[?7]", "j.l[1]")]
    public void SelectionAndIndexOfAnOptional_CostWhatThePlainFormCostsForAJsonValue(string optionalForm, string plainForm)
    {
        CelBindings bindings = MapBindings();
        ulong plain = Units(Maps, plainForm, bindings);
        ulong optional = Units(Maps, optionalForm, bindings);

        Assert.Equal(plain, optional);
        Assert.True(plain > 1);
    }

    // The node of a literal charges 1 and what it stores, after its parts and before it creates the value: the limit that is one
    // unit short stops it at the literal, which no part has charged more than it would.
    [Theory]
    [InlineData("[?optional.of(1), ?optional.of(2)]", 5UL)]
    [InlineData("[1, ?optional.of(2)]", 4UL)]
    [InlineData("[?optional.none()]", 2UL)]
    [InlineData("{?'a': optional.of(1)}", 5UL)]
    [InlineData("{?'a': optional.none()}", 2UL)]
    public static void LiteralWithOptionalPositions_ChargesWhatItStoresBeforeItCreates(string expression, ulong units)
    {
        Assert.Equal(units, Units(Maps, expression, new CelBindings()));

        CelEvalResult<CelValue> atLimit = Evaluate(Maps, expression, units);
        Assert.True(atLimit.IsSuccess);

        CelEvalResult<CelValue> below = Evaluate(Maps, expression, units - 1);
        CelError error = Assert.Single(below.Errors);
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
    }

    [Theory]
    [InlineData("[?optional.of(1), ?optional.of(2)]", 4UL)]
    [InlineData("{?'a': optional.of(1)}", 4UL)]
    public static void LimitThatTheLiteralItselfExceeds_IsCostLimitExceededAtTheLiteral(string expression, ulong limit)
    {
        CelError error = Assert.Single(Evaluate(Maps, expression, limit).Errors);

        Assert.Equal((CelErrorCode.CostLimitExceeded, 0), (error.Code, error.Location.Offset));
    }

    // A read of a field of an object is an event of cancellation, for the selection and for the presence test.
    [Theory]
    [InlineData("c.?age")]
    [InlineData("optional.of(c).age")]
    [InlineData("has(optional.of(c).age)")]
    public static void ReadOfAFieldOfAnObject_IsAnEventOfCancellation(string expression)
    {
        CelProgram<CelValue> program = Described.Compile<CelValue>(expression).GetProgramOrThrow();
        CelBindings bindings = ContactBindings();

        _ = Assert.ThrowsAny<OperationCanceledException>(() => program.Evaluate(bindings, cancellationToken: new CancellationToken(true)));
        Assert.True(program.Evaluate(bindings, cancellationToken: CancellationToken.None).IsSuccess);
    }

    // A map is no event: the selection of an optional of a map reads no application code.
    [Fact]
    public void SelectionOfAnOptionalMap_IsNoEventOfCancellation()
    {
        CelProgram<CelValue> program = Maps.Compile<CelValue>("optional.of({'a': 1}).a").GetProgramOrThrow();

        Assert.True(program.Evaluate(new CelBindings(), cancellationToken: new CancellationToken(true)).IsSuccess);
    }

    private CelBindings MapBindings() => new CelBindings()
        .Add("d", CelValue.FromInt64(7))
        .Add("j", document.RootElement)
        .Add("r", repeated.RootElement);

    private static CelBindings ContactBindings() => Bind(
        ("c", new Contact { Name = "Ann", Age = 30, Score = 1.5, Manager = new Contact { Name = "Bob", Age = 50 } }),
        ("lonely", new Contact { Age = -1 }),
        ("broken", new Contact { Throws = true }));

    private static ulong Units(CelEnvironment environment, string expression, CelBindings bindings)
    {
        Plan plan = environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
        CelValue value = plan.Run(frame);
        Assert.NotEqual(CelValueKind.Error, value.Kind);
        return frame.ChargedUnits;
    }

    private static CelEvalResult<CelValue> Evaluate(CelEnvironment environment, string expression, ulong limit) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
}
