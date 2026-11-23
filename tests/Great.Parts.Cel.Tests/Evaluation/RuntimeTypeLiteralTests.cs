using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Object literals of the types described at run time: over a dictionary carrier (a read-only dictionary of the CelValue of each field
// the literal sets) and over a carrier class (a delegate of the application that makes the carrier). Without the call that allows
// them a literal is TypeNotConstructible.
[Trait("Requirement", "F-26")]
public sealed class RuntimeTypeLiteralTests
{
    private const string Name = "acme.crm.Deal";

    private static CelEnvironment EnvironmentOf(CelObjectType type) =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(CelTypeProvider.Create("deals", type))
            .Build()
            .GetEnvironmentOrThrow();

    private static CelObjectTypeBuilder DictionaryBuilder() =>
        CelObjectTypeBuilder.ForDictionary(Name).AddField("amount", CelType.Double).AddField("stage", CelType.String);

    private static bool True(CelEnvironment environment, string expression) =>
        environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static CelError EvaluationFailure(CelEnvironment environment, string expression)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public static void DictionaryType_CreatesAnInstanceThatReadsBackAndHasNoKeyForAFieldNotSet()
    {
        CelEnvironment environment = EnvironmentOf(DictionaryBuilder().AllowObjectLiterals().Build());

        Assert.True(True(environment, "acme.crm.Deal{amount: 10.0}.amount == 10.0"));
        Assert.True(True(environment, "!has(acme.crm.Deal{amount: 10.0}.stage) && has(acme.crm.Deal{amount: 10.0}.amount)"));
        Assert.True(True(environment, "acme.crm.Deal{amount: 1.0, stage: 'new'}.stage == 'new' && type(acme.crm.Deal{}) == acme.crm.Deal"));
    }

    [Fact]
    public static void DictionaryType_CreatesAReadOnlyDictionaryOfTheFieldsTheLiteralSets()
    {
        CelEnvironment environment = EnvironmentOf(DictionaryBuilder().AllowObjectLiterals().Build());

        var dictionary = environment.Compile<object>("acme.crm.Deal{amount: 10.0}").GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

        var entries = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(dictionary);
        KeyValuePair<string, object?> entry = Assert.Single(entries);
        Assert.Equal("amount", entry.Key);
        Assert.Equal(CelValue.FromDouble(10.0), Assert.IsType<CelValue>(entry.Value));
    }

    [Fact]
    public static void DictionaryType_WithoutTheCall_IsTypeNotConstructible()
    {
        CelEnvironment environment = EnvironmentOf(DictionaryBuilder().Build());

        CelCompileResult<CelValue> result = environment.Compile<CelValue>("acme.crm.Deal{amount: 10.0}");

        Assert.Equal(CelErrorCode.TypeNotConstructible, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public static void BuilderThatAllowsLiterals_BuildsADescriptionPerCallWithTheFlagItHadThen()
    {
        CelObjectTypeBuilder builder = DictionaryBuilder();
        CelObjectType before = builder.Build();
        CelObjectType after = builder.AllowObjectLiterals().Build();

        Assert.Equal(CelErrorCode.TypeNotConstructible, Assert.Single(EnvironmentOf(before).Compile<CelValue>("acme.crm.Deal{}").Errors).Code);
        Assert.True(EnvironmentOf(after).Compile<CelValue>("acme.crm.Deal{}").IsSuccess);
    }

    [Fact]
    public static void CarrierType_PassesADictionaryOfTheSetFieldsAndWrapsTheCarrier()
    {
        IReadOnlyDictionary<string, CelValue>? seen = null;
        CelObjectType type = CarrierBuilder()
            .AllowObjectLiterals(fields =>
            {
                seen = fields;
                return new Deal(fields);
            })
            .Build();
        CelEnvironment environment = EnvironmentOf(type);

        Assert.True(True(environment, "acme.crm.Deal{amount: 10.0}.amount == 10.0"));

        Assert.NotNull(seen);
        KeyValuePair<string, CelValue> entry = Assert.Single(seen);
        Assert.Equal("amount", entry.Key);
        Assert.Equal(CelValue.FromDouble(10.0), entry.Value);
    }

    [Fact]
    public static void CarrierType_WithoutTheCall_IsTypeNotConstructible()
    {
        CelEnvironment environment = EnvironmentOf(CarrierBuilder().Build());

        Assert.Equal(CelErrorCode.TypeNotConstructible, Assert.Single(environment.Compile<CelValue>("acme.crm.Deal{amount: 1.0}").Errors).Code);
    }

    [Fact]
    public static void CarrierType_WithANullCarrier_IsUnexpectedNull()
    {
        CelEnvironment environment = EnvironmentOf(CarrierBuilder().AllowObjectLiterals(static _ => null!).Build());

        CelError error = EvaluationFailure(environment, "acme.crm.Deal{amount: 10.0}");

        Assert.Equal(CelErrorCode.UnexpectedNull, error.Code);
        Assert.Equal(Name.Length, error.Location.Offset);
    }

    [Fact]
    public static void CarrierType_WhoseDelegateThrows_IsApplicationException()
    {
        CelEnvironment environment = EnvironmentOf(CarrierBuilder().AllowObjectLiterals(static _ => throw new InvalidOperationException("no deal")).Build());

        CelError error = EvaluationFailure(environment, "acme.crm.Deal{amount: 10.0}");

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("no deal", Assert.IsType<InvalidOperationException>(error.Exception).Message);
    }

    [Fact]
    public static void CarrierType_WithANullDelegate_IsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CarrierBuilder().AllowObjectLiterals(null!));
    }

    [Fact]
    public static void Cost_OfALiteralOverACarrier_IsTheLiteralAndItsFields()
    {
        CelEnvironment dictionary = EnvironmentOf(DictionaryBuilder().AllowObjectLiterals().Build());
        CelEnvironment carrier = EnvironmentOf(CarrierBuilder().AllowObjectLiterals(static fields => new Deal(fields)).Build());

        (_, ulong dictionaryCost) = MessageSamples.Run(dictionary, "acme.crm.Deal{amount: 10.0, stage: 'a long long stage name'}");
        (_, ulong carrierCost) = MessageSamples.Run(carrier, "acme.crm.Deal{amount: 10.0, stage: 'a long long stage name'}");

        Assert.Equal(3UL, dictionaryCost);
        Assert.Equal(3UL, carrierCost);
    }

    private static CelObjectTypeBuilder<Deal> CarrierBuilder() =>
        new CelObjectTypeBuilder<Deal>(Name)
            .AddField("amount", CelType.Double, static deal => deal.Fields.TryGetValue("amount", out CelValue value) ? value : CelValue.Null, static deal => deal.Fields.ContainsKey("amount"))
            .AddField("stage", CelType.String, static deal => deal.Fields.TryGetValue("stage", out CelValue value) ? value : CelValue.Null, static deal => deal.Fields.ContainsKey("stage"));

    // The carrier of a type described at run time: the values of the fields as they were set.
    private sealed class Deal(IReadOnlyDictionary<string, CelValue> fields)
    {
        internal IReadOnlyDictionary<string, CelValue> Fields { get; } = fields;
    }
}
