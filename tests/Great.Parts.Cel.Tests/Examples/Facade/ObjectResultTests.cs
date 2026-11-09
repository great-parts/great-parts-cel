using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The result rows for objects and for the collections of the application. An object value converts to T
// by an instance test: the instance itself for a carrier, a base class or an interface that it is an
// instance of, for object and as a CelValue; the description is not read, so an instance that no type
// source describes converts too. Any other T is ResultConversion at the root, never TypeNotAccessible. A
// list or a map of the application is copied at the end of the evaluation, never returned as a lazy view
// for IReadOnlyList<X>, IReadOnlyDictionary<XK, XV> and object. deal is acme.crm.Deal over DealCarrier, x is
// dyn, lv is a list of dyn, mv a map of dyn.
[Trait("Requirement", "+R-10")]
public sealed class ObjectResultTests
{
    private static readonly CelObjectType Deal = new CelObjectTypeBuilder<DealCarrier>("acme.crm.Deal")
        .AddField("amount", CelType.Int, carrier => CelValue.FromInt64(carrier.Amount))
        .Build();

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("crm", Deal))
        .AddVariable("deal", Deal.Type)
        .AddVariable("x", CelType.Dyn)
        .AddVariable<CelList>("lv")
        .AddVariable<CelMap>("mv")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    public void ObjectResult_IsTheInstanceItself()
    {
        var carrier = new DealCarrier { Amount = 5 };
        var bindings = new CelBindings().Add("deal", carrier);

        Assert.Same(carrier, Evaluate<DealCarrier>("deal", bindings).GetValueOrThrow());
        Assert.Same(carrier, Evaluate<ICarrier>("deal", bindings).GetValueOrThrow());
        Assert.Same(carrier, Evaluate<object>("deal", bindings).GetValueOrThrow());
        Assert.True(Evaluate<CelValue>("deal", bindings).GetValueOrThrow().TryGetObject(out object? instance));
        Assert.Same(carrier, instance);
        Assert.Same(carrier, Evaluate<DealCarrier>("[deal][0]", new CelBindings().Add("deal", CelValue.FromObject(Deal, carrier))).GetValueOrThrow());
    }

    // The instance test reads no description: an instance that no type source describes converts to its
    // own class and fails for another.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ObjectResultOfAnotherClass_IsResultConversionAtTheRoot()
    {
        var unknown = new Unknown();

        AssertConversion(
            Evaluate<OtherCarrier>("deal", new CelBindings().Add("deal", new DealCarrier())),
            0,
            "result of type 'acme.crm.Deal' cannot be converted to .NET type 'Great.Parts.Cel.Tests.Examples.Facade.ObjectResultTests+OtherCarrier'");
        AssertConversion(
            Evaluate<OtherCarrier>("x", new CelBindings().Add("x", unknown)),
            0,
            "result of type 'Great.Parts.Cel.Tests.Examples.Facade.ObjectResultTests+Unknown' cannot be converted to .NET type 'Great.Parts.Cel.Tests.Examples.Facade.ObjectResultTests+OtherCarrier'");
        AssertConversion(
            Evaluate<DealCarrier>("x", new CelBindings().Add("x", 1L)),
            0,
            "result of type 'int' cannot be converted to .NET type 'Great.Parts.Cel.Tests.Examples.Facade.ObjectResultTests+DealCarrier'");
        Assert.Same(unknown, Evaluate<Unknown>("x", new CelBindings().Add("x", unknown)).GetValueOrThrow());
        var other = new OtherCarrier();
        Assert.Same(other, Evaluate<OtherCarrier>("x", new CelBindings().Add("x", other)).GetValueOrThrow());
    }

    // The copy is made at the end of the evaluation: an exception of an element read then is
    // ApplicationException at the root, and a later change of the source does not change the result.
    // CelList and CelMap return the view as it is.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-9")]
    public void CollectionOfTheApplication_IsCopiedForTypedResults()
    {
        var source = new List<long> { 1, 2, 3 };
        CelList view = CelList.CreateView(source, CelValue.FromInt64);
        CelList throwing = CelList.CreateView(source, value => value == 2 ? throw new InvalidOperationException() : CelValue.FromInt64(value));

        IReadOnlyList<long> copy = Evaluate<IReadOnlyList<long>>("lv", new CelBindings().Add("lv", view)).GetValueOrThrow();
        source[0] = 10;
        CelEvalResult<IReadOnlyList<long>> failed = Evaluate<IReadOnlyList<long>>("lv", new CelBindings().Add("lv", throwing));
        CelList asItIs = Evaluate<CelList>("lv", new CelBindings().Add("lv", throwing)).GetValueOrThrow();

        Assert.Equal(new long[] { 1, 2, 3 }, copy);
        CelError error = Assert.Single(failed.Errors);
        Assert.Equal((CelErrorCode.ApplicationException, 0), (error.Code, error.Location.Offset));
        Assert.Equal("application code threw System.InvalidOperationException in a list of the application", error.Message);
        Assert.Equal(3, asItIs.Count);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void MapOfTheApplication_IsCopiedForTypedResults()
    {
        var source = new Dictionary<string, long> { ["a"] = 1 };
        CelMap view = CelMap.CreateView(source, CelValue.FromInt64, keepSourceOrder: false);

        IReadOnlyDictionary<string, long> copy = Evaluate<IReadOnlyDictionary<string, long>>("mv", new CelBindings().Add("mv", view)).GetValueOrThrow();
        source["a"] = 2;

        Assert.Equal(1L, copy["a"]);
        Assert.Single(copy);
    }

    private CelEvalResult<T> Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    private static void AssertConversion<T>(CelEvalResult<T> result, int offset, string message)
    {
        CelError error = Assert.Single(result.Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(message, error.Message);
    }

    private interface ICarrier
    {
    }

    private sealed class DealCarrier : ICarrier
    {
        public long Amount { get; init; }
    }

    private sealed class OtherCarrier
    {
    }

    private sealed class Unknown
    {
    }
}
