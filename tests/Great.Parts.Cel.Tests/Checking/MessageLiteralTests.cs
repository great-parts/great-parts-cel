using System;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.MessageSamples;

namespace Great.Parts.Cel.Tests.Checking;

// Type checking of the literal of a constructible type, on a test description that stands for a protobuf message
// (docs/design/checking.md, "Message literals of constructible types"): the fields that the description has, the types of the
// values against the types of the fields, the type of the literal, the reference map for the planner, and the type Any of an
// operand, which the checker treats as dyn.
public sealed class MessageLiteralTests
{
    private static readonly MessageType Description = NewMessageType();
    private static readonly CelEnvironment WithMessages = CreateEnvironment(Description);
    private static readonly CelEnvironment InContainer = CreateEnvironment(Description, "acme");

    [Theory]
    [InlineData("acme.test.Message{}", "acme.test.Message")]
    [InlineData(".acme.test.Message{}", "acme.test.Message")]
    [InlineData("acme.test.Message{id: 1}.id", "int")]
    [InlineData("acme.test.Message{child: acme.test.Message{id: 2}}.child.id", "int")]
    [InlineData("acme.test.Message{id: 1, id: 2}.id", "int")]
    [InlineData("google.protobuf.Int32Value{value: 1}", "wrapper(int)")]
    [InlineData("google.protobuf.Int32Value{value: 1} == 1", "bool")]
    [InlineData("google.protobuf.Int32Value{value: 1} != null", "bool")]
    [InlineData("google.protobuf.Value{bool_value: true}", "dyn")]
    [InlineData("google.protobuf.Any{type_url: 'x', value: b'y'}", "any")]
    [InlineData("[google.protobuf.Any{}, 1]", "list(dyn)")]
    public static void LiteralOfAConstructibleType_HasTheTypeOfItsDescription(string expression, string type)
    {
        CheckResult result = Check(WithMessages, expression);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(type, result.Expression!.ResultType.ToString());
    }

    [Fact]
    public static void NameOfTheLiteral_ResolvesByTheContainer()
    {
        Assert.Equal("int", Check(InContainer, "test.Message{id: 1}.id").Expression!.ResultType.ToString());
        Assert.Equal("acme.test.Message", Check(InContainer, "test.Message{}").Expression!.ResultType.ToString());
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(Check(InContainer, "Message{}").Errors).Code);
    }

    [Theory]
    [InlineData("acme.test.Message{count: null}")]
    [InlineData("acme.test.Message{count: 1}")]
    [InlineData("acme.test.Message{child: null}")]
    [InlineData("acme.test.Message{payload: 1}")]
    [InlineData("acme.test.Message{payload: [1, 'a']}")]
    [InlineData("acme.test.Message{extra: 'x', id: dyn('not an int')}")]
    [InlineData("acme.test.Message{tags: [1, 'a']}")]
    [InlineData("acme.test.Message{tags: []}")]
    [InlineData("acme.test.Message{attributes: {}}")]
    [InlineData("acme.test.Message{attributes: {'a': dyn('x')}}")]
    [InlineData("acme.test.Message{small: 5, small: 6}")]
    public static void ValueThatFitsItsField_IsNotAnError(string expression)
    {
        CheckResult result = Check(WithMessages, expression);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
    }

    [Theory]
    [InlineData("acme.test.Message{id: 1.5}", "expected type 'int' but found 'double'")]
    [InlineData("acme.test.Message{id: 1u}", "expected type 'int' but found 'uint'")]
    [InlineData("acme.test.Message{id: null}", "expected type 'int' but found 'null_type'")]
    [InlineData("acme.test.Message{label: 1}", "expected type 'string' but found 'int'")]
    [InlineData("acme.test.Message{ratio: 1}", "expected type 'double' but found 'int'")]
    [InlineData("acme.test.Message{blob: 'a'}", "expected type 'bytes' but found 'string'")]
    [InlineData("acme.test.Message{tags: [1]}", "expected type 'list(string)' but found 'list(int)'")]
    [InlineData("acme.test.Message{tags: null}", "expected type 'list(string)' but found 'null_type'")]
    [InlineData("acme.test.Message{attributes: {'a': 'b'}}", "expected type 'map(string, int)' but found 'map(string, string)'")]
    [InlineData("acme.test.Message{child: 1}", "expected type 'acme.test.Message' but found 'int'")]
    [InlineData("acme.test.Message{count: 'a'}", "expected type 'wrapper(int)' but found 'string'")]
    [InlineData("google.protobuf.Int32Value{value: 'a'}", "expected type 'int' but found 'string'")]
    public static void ValueThatDoesNotFitItsField_IsTypeMismatchAtTheColonOfTheEntry(string expression, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelError error = Assert.Single(Check(WithMessages, expression).Errors);

        Assert.Equal((CelErrorCode.TypeMismatch, expression.IndexOf(':', StringComparison.Ordinal), message), (error.Code, error.Location.Offset, error.Message));
    }

    [Theory]
    [InlineData("acme.test.Message{nope: 1}", "undefined field 'nope' in type 'acme.test.Message'")]
    [InlineData("acme.test.Message{Id: 1}", "undefined field 'Id' in type 'acme.test.Message'")]
    [InlineData("google.protobuf.Int32Value{nope: 1}", "undefined field 'nope' in type 'google.protobuf.Int32Value'")]
    public static void FieldThatTheDescriptionLacks_IsNoSuchFieldAtTheColonOfTheEntry(string expression, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelError error = Assert.Single(Check(WithMessages, expression).Errors);

        Assert.Equal((CelErrorCode.NoSuchField, expression.IndexOf(':', StringComparison.Ordinal), message), (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public static void FieldThatTheDescriptionExcluded_IsFieldNotAccessibleWithItsReason()
    {
        const string Expression = "acme.test.Message{hidden: 1}";

        CelError error = Assert.Single(Check(WithMessages, Expression).Errors);

        Assert.Equal((CelErrorCode.FieldNotAccessible, Expression.IndexOf(':', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("field 'hidden' of type 'acme.test.Message' is not available to expressions: the member is not public", error.Message);
    }

    [Theory]
    [InlineData("acme.test.Message{}.hidden")]
    [InlineData("has(acme.test.Message{}.hidden)")]
    public static void SelectionOfAFieldThatTheDescriptionExcluded_IsFieldNotAccessibleWithItsReason(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelError error = Assert.Single(Check(WithMessages, expression).Errors);

        Assert.Equal(CelErrorCode.FieldNotAccessible, error.Code);
        Assert.Equal("field 'hidden' of type 'acme.test.Message' is not available to expressions: the member is not public", error.Message);
    }

    // The check goes on after an error: every faulty entry is an error, in the order of the text.
    [Fact]
    public static void EveryFaultyEntry_IsAnErrorInTheOrderOfTheText()
    {
        const string Expression = "acme.test.Message{nope: 1, id: 'a', label: 2, ratio: 1.5}";

        CelError[] errors = [.. Check(WithMessages, Expression).Errors];

        Assert.Equal(
            [
                (CelErrorCode.NoSuchField, Expression.IndexOf("nope:", StringComparison.Ordinal) + 4),
                (CelErrorCode.TypeMismatch, Expression.IndexOf("id:", StringComparison.Ordinal) + 2),
                (CelErrorCode.TypeMismatch, Expression.IndexOf("label:", StringComparison.Ordinal) + 5),
            ],
            errors.Select(error => (error.Code, error.Location.Offset)).ToArray());
    }

    // A value that has an error of its own is not also a mismatch: the type error fits every type.
    [Fact]
    public static void ValueWithAnError_IsNotAlsoAMismatch()
    {
        CelError error = Assert.Single(Check(WithMessages, "acme.test.Message{id: 1 + 'a'}").Errors);

        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
    }

    // The literal has the type of its description also when an entry has an error, so the rest of the expression is checked.
    [Fact]
    public static void LiteralWithAnError_StillHasItsType_SoTheRestIsChecked()
    {
        CelError[] errors = [.. Check(WithMessages, "acme.test.Message{nope: 1}.missing").Errors];

        Assert.Equal(
            [CelErrorCode.NoSuchField, CelErrorCode.NoSuchField],
            errors.Select(error => error.Code).ToArray());
        Assert.Equal(
            ["undefined field 'nope' in type 'acme.test.Message'", "undefined field 'missing' in type 'acme.test.Message'"],
            errors.Select(error => error.Message).ToArray());
    }

    [Fact]
    public static void CheckedLiteral_RecordsItsDescriptionAndTheFieldNumberOfEachEntry()
    {
        CheckedExpression expression = Check(WithMessages, "acme.test.Message{label: 'x', id: 2, id: 3}").Expression!;
        var literal = Assert.IsType<StructNode>(expression.Parsed.Root);

        Assert.Same(Description, expression.References[literal.Id].ObjectType);
        Assert.Equal(
            [Label, Id, Id],
            literal.Entries.Select(entry => expression.References[entry.Id].FieldNumber).ToArray());
        Assert.All(literal.Entries, entry => Assert.Same(Description, expression.References[entry.Id].ObjectType));
    }

    [Fact]
    public static void NameOfAKnownTypeWithoutADescription_IsUndeclared()
    {
        CelEnvironment bare = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        CelError error = Assert.Single(Check(bare, "google.protobuf.Any{}").Errors);

        Assert.Equal((CelErrorCode.UndeclaredReference, 19), (error.Code, error.Location.Offset));
    }

    [Theory]
    [InlineData("anything.field", "dyn")]
    [InlineData("anything.field.deeper", "dyn")]
    [InlineData("has(anything.field)", "bool")]
    [InlineData("anything + 1", "int")]
    [InlineData("anything + anything", "dyn")]
    [InlineData("anything == 1", "bool")]
    [InlineData("anything == anything", "bool")]
    [InlineData("anything ? 1 : 2", "int")]
    [InlineData("anything && true", "bool")]
    [InlineData("size(anything)", "int")]
    [InlineData("anything.all(x, x > 1)", "bool")]
    [InlineData("anything.map(x, x)", "list(dyn)")]
    [InlineData("[anything]", "list(any)")]
    [InlineData("[anything, 1]", "list(dyn)")]
    [InlineData("[anything, anything]", "list(any)")]
    [InlineData("{'k': anything}", "map(string, any)")]
    [InlineData("{'k': anything, 'j': 1}", "map(string, dyn)")]
    public static void OperandOfTheTypeAny_IsTreatedAsDyn(string expression, string type)
    {
        CheckResult result = Check(WithMessages, expression);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(type, result.Expression!.ResultType.ToString());
    }

    // A field of an object type that does not have it is an error, but a field of a value of the type Any is found by name at
    // evaluation, so no reference is recorded for it.
    [Fact]
    public static void SelectionOnAny_RecordsNoReference_AndSelectionOnAnObjectDoes()
    {
        CheckedExpression onAny = Check(WithMessages, "anything.id").Expression!;
        CheckedExpression onObject = Check(WithMessages, "msg.id").Expression!;

        Assert.False(onAny.References.ContainsKey(onAny.Parsed.Root.Id));
        Assert.Equal(Id, onObject.References[onObject.Parsed.Root.Id].FieldNumber);
        Assert.Equal(CelErrorCode.NoSuchField, Assert.Single(Check(WithMessages, "msg.nope").Errors).Code);
    }

    private static CheckResult Check(CelEnvironment environment, string expression)
    {
        ParseResult parsed = environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        return environment.Check(parsed.Expression);
    }
}
