using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.MessageSamples;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;

namespace Great.Parts.Cel.Tests.Evaluation;

// Evaluation of the literal of a constructible type, on a test description that stands for a protobuf message
// (docs/design/evaluation.md, "Message literals"): the values in the order of the text, the field numbers of the description, the
// last value of a field that is named twice, the charge 1 + n + ⌈L/10⌉, an exception and an error value of the description, the
// literal of a known type, and the literal planned without type checking.
public sealed class MessageLiteralPlanTests
{
    [Fact]
    public static void Literal_PassesTheNumbersAndTheValuesInTheOrderOfTheText()
    {
        MessageType type = NewMessageType();

        (CelValue value, _) = Run(CreateEnvironment(type), "acme.test.Message{label: 'x', id: 7, ratio: 1.5}.id");

        Assert.Equal(CelValue.FromInt64(7), value);
        (int[] numbers, CelValue[] values) = Assert.Single(type.Calls);
        Assert.Equal([Label, Id, Ratio], numbers);
        Assert.Equal([CelValue.FromString("x"), CelValue.FromInt64(7), CelValue.FromDouble(1.5)], values);
    }

    [Fact]
    public static void LiteralWithoutEntries_CreatesTheInstanceFromNothing()
    {
        MessageType type = NewMessageType();

        (CelValue value, _) = Run(CreateEnvironment(type), "acme.test.Message{}");

        Assert.Equal(CelValueKind.Object, value.Kind);
        (int[] numbers, CelValue[] values) = Assert.Single(type.Calls);
        Assert.Empty(numbers);
        Assert.Empty(values);
    }

    [Fact]
    public static void FieldNamedTwice_KeepsTheLastValueInTheOrderOfItsLastOccurrence()
    {
        MessageType type = NewMessageType();

        (CelValue value, _) = Run(CreateEnvironment(type), "acme.test.Message{id: 1, label: 'a', id: 2}.id");

        Assert.Equal(CelValue.FromInt64(2), value);
        (int[] numbers, CelValue[] values) = Assert.Single(type.Calls);
        Assert.Equal([Label, Id], numbers);
        Assert.Equal([CelValue.FromString("a"), CelValue.FromInt64(2)], values);
    }

    // More entries than a scan of the pairs is meant for: the last value still wins.
    [Fact]
    public static void ManyEntriesOfOneField_KeepTheLastValue()
    {
        MessageType type = NewMessageType();
        string entries = string.Join(", ", Enumerable.Range(0, 40).Select(index => "id: " + index));

        (CelValue value, ulong cost) = Run(CreateEnvironment(type), "acme.test.Message{" + entries + "}.id");

        Assert.Equal(CelValue.FromInt64(39), value);
        (int[] numbers, CelValue[] values) = Assert.Single(type.Calls);
        Assert.Equal([Id], numbers);
        Assert.Equal([CelValue.FromInt64(39)], values);
        Assert.True(cost >= 41, "a repeated name is charged each time");
    }

    // The first error value of the values is the result; the later values are not evaluated and nothing is created.
    [Fact]
    public static void ErrorValueOfAnEntry_IsTheResult_AndNothingIsCreated()
    {
        MessageType type = NewMessageType();
        const string Expression = "acme.test.Message{id: 1 / 0, small: 2 % 0}";

        CelError error = ErrorOf(Run(CreateEnvironment(type), Expression).Value);

        Assert.Equal((CelErrorCode.DivisionByZero, Expression.IndexOf('/', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Empty(type.Calls);
    }

    [Fact]
    public static void Charge_IsOnePlusTheNumberOfEntriesAsWritten()
    {
        CelEnvironment environment = CreateEnvironment(NewMessageType());

        Assert.Equal(1UL, Run(environment, "acme.test.Message{}").Cost);
        Assert.Equal(2UL, Run(environment, "acme.test.Message{id: 1}").Cost);
        Assert.Equal(3UL, Run(environment, "acme.test.Message{id: 1, label: ''}").Cost);
        Assert.Equal(3UL, Run(environment, "acme.test.Message{id: 1, id: 2}").Cost);
    }

    // ⌈L/10⌉ more, L the lengths of the strings and the bytes written directly in the fields, in UTF-16 units and in bytes.
    [Theory]
    [InlineData("acme.test.Message{label: 'abcdefghij'}", 3UL)]
    [InlineData("acme.test.Message{label: 'abcdefghijk'}", 4UL)]
    [InlineData("acme.test.Message{blob: b'abcdefghij'}", 3UL)]
    [InlineData("acme.test.Message{label: 'abcdefghij', blob: b'abcdefghijk'}", 6UL)]
    [InlineData("acme.test.Message{label: '\U0001F600\U0001F600\U0001F600'}", 3UL)]
    public static void Charge_AddsATenthOfTheLengthOfTheWrittenStringsAndBytes(string expression, ulong cost)
    {
        Assert.Equal(cost, Run(CreateEnvironment(NewMessageType()), expression).Cost);
    }

    // The elements of a list and the values of a map that a field gets are not counted: they were charged where they were made.
    [Fact]
    public static void Charge_DoesNotCountTheElementsOfAList()
    {
        CelEnvironment environment = CreateEnvironment(NewMessageType());

        ulong shortElement = Run(environment, "acme.test.Message{tags: ['a']}").Cost;
        ulong longElement = Run(environment, "acme.test.Message{tags: ['abcdefghijklmnopqrstuvwxyz0123456789']}").Cost;

        Assert.Equal(shortElement, longElement);
    }

    [Fact]
    public static void CostLimit_OfExactlyTheCharge_Passes_AndOneUnitLessFailsAtTheLiteral()
    {
        CelEnvironment environment = CreateEnvironment(NewMessageType());
        const string Expression = "acme.test.Message{label: 'abcdefghijk'}";

        Assert.Equal(CelValueKind.Object, RunWithLimit(environment, Expression, 4).Kind);
        CelError error = ErrorOf(RunWithLimit(environment, Expression, 3));

        Assert.Equal((CelErrorCode.CostLimitExceeded, Expression.IndexOf('{', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
    }

    [Fact]
    public static void ExceptionOfCreateInstance_IsApplicationExceptionAtTheLiteral()
    {
        var thrown = new InvalidOperationException("boom");
        MessageType type = NewMessageType((_, _, _) => throw thrown);
        const string Expression = "acme.test.Message{id: 1}";

        CelError error = ErrorOf(Run(CreateEnvironment(type), Expression).Value);

        Assert.Equal((CelErrorCode.ApplicationException, Expression.IndexOf('{', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("application code threw System.InvalidOperationException in the creation of an object of type 'acme.test.Message'", error.Message);
        Assert.Same(thrown, error.Exception);
    }

    [Fact]
    public static void ErrorValueOfTheDescription_KeepsItsTextAndGetsThePlaceOfTheLiteral()
    {
        MessageType type = NewMessageType();
        const string Expression = "acme.test.Message{small: 500}";

        CelError error = ErrorOf(Run(CreateEnvironment(type), Expression).Value);

        Assert.Equal((CelErrorCode.ConversionRange, Expression.IndexOf('{', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("500 is out of the range of small", error.Message);
        Assert.Equal(CelValueKind.Object, Run(CreateEnvironment(NewMessageType()), "acme.test.Message{small: 100}").Value.Kind);
    }

    [Fact]
    public static void ErrorValueOfTheDescription_IsNotChargedMoreThanTheLiteral()
    {
        CelEnvironment environment = CreateEnvironment(NewMessageType());

        Assert.Equal(
            Run(environment, "acme.test.Message{small: 500}").Cost,
            Run(environment, "acme.test.Message{small: 5}").Cost);
    }

    // The same instance is the value of a field of another literal, and the fields of the instance are read by the description.
    [Fact]
    public static void MessageInAField_IsTheInstanceThatTheOtherLiteralMade()
    {
        MessageType type = NewMessageType();

        (CelValue value, _) = Run(CreateEnvironment(type), "acme.test.Message{child: acme.test.Message{id: 5}}.child.id");

        Assert.Equal(CelValue.FromInt64(5), value);
        Assert.Equal(2, type.Calls.Count);
        Assert.Equal(CelValue.FromInt64(5), type.Calls[0].Values[0]);
    }

    [Fact]
    public static void FieldThatTheLiteralDidNotSet_IsTheDefaultOfTheDescription()
    {
        CelEnvironment environment = CreateEnvironment(NewMessageType());

        Assert.Equal(CelValue.FromInt64(0), Run(environment, "acme.test.Message{}.id").Value);
        Assert.Equal(CelValue.FromBoolean(false), Run(environment, "has(acme.test.Message{}.id)").Value);
        Assert.Equal(CelValue.FromBoolean(true), Run(environment, "has(acme.test.Message{id: 0}.id)").Value);
    }

    [Theory]
    [InlineData("google.protobuf.Int32Value{value: 5} + 1", 6L)]
    [InlineData("google.protobuf.Int32Value{} == 0", true)]
    [InlineData("google.protobuf.Int32Value{value: 5} == 5", true)]
    [InlineData("google.protobuf.Value{bool_value: true}", true)]
    [InlineData("google.protobuf.Value{} == null", true)]
    public static void LiteralOfAKnownType_IsTheValueThatItsDescriptionReturns(string expression, object expected)
    {
        (CelValue value, _) = Run(CreateEnvironment(NewMessageType()), expression);

        Assert.Equal(expected switch { long number => CelValue.FromInt64(number), bool flag => CelValue.FromBoolean(flag), _ => CelValue.Null }, value);
    }

    // Without type checking the name of the type resolves at evaluation, by the candidates of the container, and the fields by name.
    [Theory]
    [InlineData("acme.test.Message{id: 3}.id", "", 3L)]
    [InlineData(".acme.test.Message{id: 3}.id", "", 3L)]
    [InlineData("test.Message{id: 3}.id", "acme", 3L)]
    [InlineData("Message{id: 3}.id", "acme.test", 3L)]
    [InlineData("acme.test.Message{id: 1, id: 4}.id", "", 4L)]
    public static void LiteralWithoutTypeChecking_ResolvesTheNameAndTheFieldsAtEvaluation(string expression, string container, long id)
    {
        ArgumentNullException.ThrowIfNull(container);
        MessageType type = NewMessageType();

        CelValue value = RunWithoutTypeCheck(CreateEnvironment(type, container), expression);

        Assert.Equal(CelValue.FromInt64(id), value);
        Assert.Single(type.Calls);
    }

    [Fact]
    public static void LiteralWithoutTypeChecking_OfAFieldThatTheDescriptionLacks_IsEvalNoSuchFieldAtTheLiteral()
    {
        const string Expression = "acme.test.Message{nope: 1}";

        CelError error = ErrorOf(RunWithoutTypeCheck(CreateEnvironment(NewMessageType()), Expression));

        Assert.Equal((CelErrorCode.EvalNoSuchField, Expression.IndexOf('{', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("no such field 'nope' in acme.test.Message", error.Message);
    }

    [Theory]
    [InlineData("acme.test.Nothing{}")]
    [InlineData("acme.crm.Contact{}")]
    public static void LiteralWithoutTypeChecking_OfANameWithoutAConstructibleDescription_IsUnbound(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelEnvironment environment = CreateEnvironment(NewMessageType(), "", ContactType);

        CelError error = ErrorOf(RunWithoutTypeCheck(environment, expression));

        Assert.Equal((CelErrorCode.Unbound, expression.IndexOf('{', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("no value is bound to variable '" + expression[..expression.IndexOf('{', StringComparison.Ordinal)] + "'", error.Message);
    }

    // The error value of an entry is the result also without type checking, and the later values are not evaluated.
    [Fact]
    public static void LiteralWithoutTypeChecking_StopsAtTheFirstErrorValue()
    {
        MessageType type = NewMessageType();

        CelError error = ErrorOf(RunWithoutTypeCheck(CreateEnvironment(type), "acme.test.Message{id: 1 / 0, label: msg.label}"));

        Assert.Equal(CelErrorCode.DivisionByZero, error.Code);
        Assert.Empty(type.Calls);
    }

    private static CelValue RunWithoutTypeCheck(CelEnvironment environment, string expression)
    {
        ParseResult parsed = environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        Plan plan = Planner.PlanWithoutTypeCheck(parsed.Expression, environment.Declarations);
        return plan.Run(plan.CreateFrame(new Dictionary<string, CelValue>(), null, CancellationToken.None));
    }
}
