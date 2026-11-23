using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Literals;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Generated;

// Object literals of the .NET types that a type context lists with AllowObjectLiterals (the sample types Money, Price, Tagged, Basket,
// Ticket, Wide, Point, Pair, Boom and Locked, and Order, which does not allow them): the constructor, the set, init and required members,
// the members of collections and of other types, the checks of the fields a literal cannot set and must set, the values that do not fit,
// the cost and the use from many threads.
[Trait("Requirement", "F-26")]
public sealed class GeneratedLiteralTests
{
    private static readonly CelEnvironment Shop = Builder(ShopContext.Default)
        .AddExtension(CelExtensions.Decimal(1))
        .EnableDecimalLiterals()
        .AddExtension(CelExtensions.Calendar(1))
        .Build()
        .GetEnvironmentOrThrow();

    private static bool True(string expression) => Evaluate<bool>(Shop, expression, new CelBindings());

    [Fact]
    public static void Members_AreSetAndTheInitializersAreKept()
    {
        Assert.True(True("acme.Money{amount: 10.5m}.amount == 10.5m"));
        Assert.True(True("acme.Money{amount: 10.5m}.currency == 'RUB' && acme.Money{amount: 10.5m}.scale == 2"));
        Assert.True(True("acme.Money{amount: 1m, currency: 'USD'}.currency == 'USD' && acme.Money{scale: 3}.scale == 3"));
        Assert.True(True("acme.Money{amount: 1m} == acme.Money{amount: 1m} && acme.Money{amount: 1m} != acme.Money{amount: 2m}"));
        Assert.True(True("type(acme.Money{}) == acme.Money && acme.Money{amount: 1m, currency: 'USD'}.display == 'USD:2'"));
    }

    [Fact]
    public static void LaterEntryOfOneField_WinsOverTheEarlier()
    {
        Assert.True(True("acme.Money{currency: 'USD', currency: 'EUR'}.currency == 'EUR'"));
    }

    [Fact]
    public static void ResultOfALiteral_IsTheInstanceOfTheType()
    {
        Money money = Evaluate<Money>(Shop, "acme.Money{amount: 3m, currency: 'EUR'}", new CelBindings());
        object price = Evaluate<object>(Shop, "acme.Price{amount: 1m}", new CelBindings());

        Assert.Equal(3m, money.Amount);
        Assert.Equal("EUR", money.Currency);
        Assert.Equal(2, money.Scale);
        Assert.Equal(new Price(1m, "RUB"), price);
    }

    [Fact]
    public static void PositionalRecord_UsesTheDefaultOfAParameterTheLiteralDoesNotSet()
    {
        Assert.True(True("acme.Price{amount: 2m}.currency == 'RUB' && acme.Price{amount: 2m, currency: 'EUR'}.currency == 'EUR'"));

        CelError error = CompileError(Shop, "acme.Price{currency: 'EUR'}");

        Assert.Equal(CelErrorCode.RequiredFieldNotSet, error.Code);
        Assert.Equal("amount", error.Subject);
        Assert.Equal("object literal of type 'acme.Price' does not set the required field 'amount'", error.Message);
        Assert.Equal(error.Message.Length > 0 ? 10 : 10, error.Location.Offset);
    }

    [Fact]
    public static void RecordWithMembersBeyondItsParameters_SetsThemAndKeepsTheirInitializers()
    {
        Assert.True(True("acme.Tagged{name: 'a'}.weight == 5 && acme.Tagged{name: 'a'}.note == 'n'"));
        Assert.True(True("acme.Tagged{name: 'a', weight: 9, note: 'z'}.weight == 9 && acme.Tagged{name: 'a', weight: 9, note: 'z'}.note == 'z'"));
        Assert.Equal(CelErrorCode.RequiredFieldNotSet, CompileError(Shop, "acme.Tagged{weight: 1}").Code);
    }

    [Fact]
    public static void RequiredMembers_MustBeSet()
    {
        Ticket ticket = Evaluate<Ticket>(Shop, "acme.Ticket{id: 'a', seat: 3}", new CelBindings());

        Assert.Equal("a", ticket.Id);
        Assert.Equal(3, ticket.Seat);
        Assert.Equal("none", ticket.Note);
        Assert.Equal("z", Evaluate<Ticket>(Shop, "acme.Ticket{id: 'a', seat: 3, note: 'z'}", new CelBindings()).Note);

        CelError missingSeat = CompileError(Shop, "acme.Ticket{id: 'a'}");
        Assert.Equal(CelErrorCode.RequiredFieldNotSet, missingSeat.Code);
        Assert.Equal("seat", missingSeat.Subject);

        CelCompileResult<CelValue> neither = Shop.Compile<CelValue>("acme.Ticket{note: 'z'}");
        Assert.Equal(2, neither.Errors.Length);
        Assert.Equal("id", neither.Errors[0].Subject);
        Assert.Equal("seat", neither.Errors[1].Subject);
    }

    [Fact]
    public static void ConstructorWithParameters_TakesTheFieldsByName()
    {
        Pair pair = Evaluate<Pair>(Shop, "acme.Pair{left: 'l', right: 'r', extra: 4}", new CelBindings());

        Assert.Equal(("l", "r", 4), (pair.Left, pair.Right, pair.Extra));
        Assert.Equal(CelErrorCode.RequiredFieldNotSet, CompileError(Shop, "acme.Pair{left: 'l'}").Code);
    }

    [Fact]
    public static void Structure_SetsAFieldAndSetAndInitMembers()
    {
        Point point = Evaluate<Point>(Shop, "acme.Point{x: 1, y: 2, z: 3}", new CelBindings());
        Point partial = Evaluate<Point>(Shop, "acme.Point{z: 9}", new CelBindings());

        Assert.Equal((1L, 2L, 3L), (point.X, point.Y, point.Z));
        Assert.Equal((0L, 0L, 9L), (partial.X, partial.Y, partial.Z));
    }

    [Theory]
    [InlineData("acme.Locked{readOnly: 'x'}", "readOnly")]
    [InlineData("acme.Locked{privateSet: 'x'}", "privateSet")]
    [InlineData("acme.Locked{anything: 'x'}", "anything")]
    [InlineData("acme.Money{display: 'x'}", "display")]
    public static void FieldThatALiteralCannotSet_IsFieldNotAccessibleAtTheField(string expression, string field)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(field);
        CelError error = CompileError(Shop, expression);

        Assert.Equal(CelErrorCode.FieldNotAccessible, error.Code);
        Assert.Equal(field, error.Subject);
        Assert.Contains("cannot be set by an object literal", error.Message, StringComparison.Ordinal);
        Assert.Equal(expression.IndexOf(field + ":", StringComparison.Ordinal) + field.Length, error.Location.Offset);
    }

    [Fact]
    public static void FieldThatCanBeSet_OfATypeWithFieldsThatCannot_IsSet()
    {
        Assert.True(True("acme.Locked{name: 'n'}.name == 'n' && acme.Locked{}.readOnly == 'r'"));
    }

    [Fact]
    public static void TypeWithoutTheAllowance_IsTypeNotConstructible()
    {
        Assert.Equal(CelErrorCode.TypeNotConstructible, CompileError(Shop, "acme.Order{total: acme.Money{amount: 1m}}").Code);
        Assert.Equal(CelErrorCode.TypeNotConstructible, CompileError(Shop, "acme.Order{id: 'x'}").Code);
    }

    [Fact]
    public static void ValueOutsideTheMember_IsAnErrorOfTheField()
    {
        CelError scale = EvaluationError(Shop, "acme.Money{scale: 1099511627776}", new CelBindings());
        CelError small = EvaluationError(Shop, "acme.Wide{small: 300u}", new CelBindings());
        CelError medium = EvaluationError(Shop, "acme.Wide{medium: -40000}", new CelBindings());

        Assert.Equal(CelErrorCode.ConversionRange, scale.Code);
        Assert.Contains("1099511627776 is out of the range of int", scale.Message, StringComparison.Ordinal);
        Assert.Equal(CelErrorCode.ConversionRange, small.Code);
        Assert.Equal(CelErrorCode.ConversionRange, medium.Code);
        Assert.Contains("-40000 is out of the range of int", medium.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void ValuesOfTheTypeMapping_AreConvertedToTheirMembers()
    {
        Wide wide = Evaluate<Wide>(
            Shop,
            "acme.Wide{letter: 'x', big: -12, bigUnsigned: 13u, huge: -9223372036854775807, day: date('2026-11-23'), at: time_of_day('09:30'),"
            + " key: '00112233-4455-6677-8899-aabbccddeeff', small: 200u, medium: -300, unsigned: 4000000000u, ratio: 1.5,"
            + " when: timestamp('2026-11-23T10:00:00Z'), span: duration('90s'), maybe: 7, data: b'ab'}",
            new CelBindings());

        Assert.Equal('x', wide.Letter);
        Assert.Equal((Int128)(-12), wide.Big);
        Assert.Equal((UInt128)13, wide.BigUnsigned);
        Assert.Equal(new BigInteger(-9223372036854775807), wide.Huge);
        Assert.Equal(new DateOnly(2026, 11, 23), wide.Day);
        Assert.Equal(new TimeOnly(9, 30), wide.At);
        Assert.Equal(new Guid("00112233-4455-6677-8899-aabbccddeeff"), wide.Key);
        Assert.Equal((byte)200, wide.Small);
        Assert.Equal((short)(-300), wide.Medium);
        Assert.Equal(4000000000u, wide.Unsigned);
        Assert.Equal(1.5f, wide.Ratio);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero), wide.When);
        Assert.Equal(TimeSpan.FromSeconds(90), wide.Span);
        Assert.Equal(7, wide.Maybe);
        Assert.Equal(2, wide.Data.Length);
        Assert.Equal((byte)'b', wide.Data[1]);
    }

    [Theory]
    [InlineData("acme.Wide{letter: 'ab'}", CelErrorCode.InvalidArgument)]
    [InlineData("acme.Wide{letter: ''}", CelErrorCode.InvalidArgument)]
    [InlineData("acme.Wide{key: 'not-a-guid'}", CelErrorCode.InvalidArgument)]
    public static void ValueThatDoesNotFit_IsAnErrorValueOfTheField(string expression, CelErrorCode code)
    {
        Assert.Equal(code, EvaluationError(Shop, expression, new CelBindings()).Code);
    }

    [Fact]
    public static void Nullable_TakesTheNullAndAValue()
    {
        Wide unset = Evaluate<Wide>(Shop, "acme.Wide{}", new CelBindings());
        Wide cleared = Evaluate<Wide>(Shop, "acme.Wide{maybe: null}", new CelBindings());

        Assert.Null(unset.Maybe);
        Assert.Null(cleared.Maybe);
    }

    [Fact]
    public static void MembersOfCollectionsAndOfOtherTypes_AreCreatedOneLevelDeep()
    {
        Basket basket = Evaluate<Basket>(
            Shop,
            "acme.Basket{owner: 'o', main: acme.Money{amount: 5m}, items: [acme.Money{amount: 1m}, acme.Money{amount: 2m}],"
            + " lines: [acme.Money{amount: 3m}], frozen: [acme.Money{amount: 4m}, acme.Money{amount: 6m}],"
            + " byName: {'a': acme.Money{amount: 7m}}, numbers: [1, 2, 3], counts: {'a': 1, 'b': 2}}",
            new CelBindings());

        Assert.Equal("o", basket.Owner);
        Assert.Equal(5m, basket.Main!.Amount);
        Assert.Equal(2, basket.Items.Count);
        Assert.Equal(2m, basket.Items[1].Amount);
        Assert.Equal(3m, Assert.Single(basket.Lines).Amount);
        Assert.Equal(2, basket.Frozen.Length);
        Assert.Equal(6m, basket.Frozen[1].Amount);
        Assert.Equal(7m, basket.ByName["a"].Amount);
        Assert.Equal(3, basket.Numbers.Count);
        Assert.Equal(3L, basket.Numbers[2]);
        Assert.Equal(2L, basket.Counts["b"]);
    }

    [Fact]
    public static void NestedLiteral_IsTheValueOfAMemberOfAnotherLiteral()
    {
        Assert.True(True("acme.Basket{main: acme.Money{amount: 5m}}.main.amount == 5m && !has(acme.Basket{}.main)"));
    }

    [Fact]
    public static void ConstructorThatThrows_IsApplicationException()
    {
        CelError error = EvaluationError(Shop, "acme.Boom{value: 1}", new CelBindings());

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        InvalidOperationException original = Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal("boom", original.Message);
    }

    [Fact]
    public static void Cost_IsTheLiteralItsFieldsAndTheElementsItCopies()
    {
        (_, ulong money) = MessageSamples.Run(Shop, "acme.Money{amount: 1m, currency: 'USD'}");
        (_, ulong none) = MessageSamples.Run(Shop, "acme.Wide{letter: 'x'}");
        (_, ulong list) = MessageSamples.Run(Shop, "[1, 2, 3]");
        (_, ulong inBasket) = MessageSamples.Run(Shop, "acme.Basket{numbers: [1, 2, 3]}");
        (_, ulong map) = MessageSamples.Run(Shop, "{'a': 1, 'b': 2}");
        (_, ulong mapInBasket) = MessageSamples.Run(Shop, "acme.Basket{counts: {'a': 1, 'b': 2}}");

        Assert.Equal(3UL, money);
        Assert.Equal(2UL, none);

        // The literal and one field, and the three elements and the two pairs that the code copies.
        Assert.Equal(2UL + 3UL, inBasket - list);
        Assert.Equal(2UL + 2UL, mapInBasket - map);
    }

    [Fact]
    public static void LiteralsFromManyThreads_GiveTheValuesOfTheirOwn()
    {
        var program = Shop.Compile<Basket>("acme.Basket{owner: 'o', items: [acme.Money{amount: 1m}], numbers: [1, 2]}").GetProgramOrThrow();

        Parallel.For(0, 200, _ =>
        {
            Basket basket = program.Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
            Assert.Equal("o", basket.Owner);
            Assert.Equal(1m, Assert.Single(basket.Items).Amount);
            Assert.Equal(2, basket.Numbers.Count);
        });
    }
}
