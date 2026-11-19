using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Bindings by the run-time type: the indexer binds or replaces a value by its run-time type, lists and dictionaries
// of the closed list as list(E) and map(string, V), anything else by the row object of the type table, an object of the
// application described by a type source of the environment as that object. Clear() empties the bindings; the collection
// initializer is Add<T>, by the static type.
[Trait("Requirement", "F-12")]
public sealed class BindingIndexerTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .AddVariable<long>("x")
        .AddVariable("d", CelType.Dyn)
        .AddVariable("ids", CelType.ListOf(CelType.Int))
        .AddVariable("prices", CelType.MapOf(CelType.String, CelType.Double))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void Indexer_BindsByTheRunTimeType()
    {
        object boxed = 41L;
        var bindings = new CelBindings
        {
            ["x"] = boxed,
            ["ids"] = new List<long> { 1, 2, 3 },
            ["prices"] = new Dictionary<string, double> { ["a"] = 1.5 },
        };

        Assert.Equal(42L, Evaluate<long>("x + 1", bindings));
        Assert.Equal(3L, Evaluate<long>("size(ids)", bindings));
        Assert.Equal(1.5, Evaluate<double>("prices['a']", bindings));
    }

    [Fact]
    public void Indexer_ReplacesAValue()
    {
        var bindings = new CelBindings().Add("x", 1L);
        bindings["x"] = 2L;

        Assert.Equal(2L, Evaluate<long>("x", bindings));
    }

    // In an environment with a type source, an object of the application in a dyn place is read as that object.
    [Fact]
    public void Indexer_WithADescribedObject_InADynPlace()
    {
        var bindings = new CelBindings { ["d"] = new Order { Status = "approved" } };

        Assert.Equal("approved", Evaluate<string>("d.status", bindings));
    }

    // A .NET collection outside the lists and the dictionaries of the closed list is accepted and fails where it is read; the type is checked against the
    // declared type at evaluation.
    [Fact]
    public void Indexer_ValueOutsideTheRows_FailsWhereItIsRead()
    {
        var bindings = new CelBindings { ["d"] = new HashSet<long> { 1 }, ["x"] = "seven" };

        Assert.Equal(CelErrorCode.TypeNotAccessible, Error("size(d)", bindings).Code);
        Assert.Equal(CelErrorCode.BindingTypeMismatch, Error("x + 1", bindings).Code);
    }

    [Fact]
    public void Indexer_RefusesWhatAddRefuses()
    {
        var bindings = new CelBindings();

        Assert.Throws<ArgumentException>(() => bindings["d"] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local));
        Assert.Throws<ArgumentException>(() => bindings["d"] = default(System.Text.Json.JsonElement));
    }

    // The getter gives the value as a result with T = object gives it; a missing name is ArgumentException.
    [Fact]
    public void Getter_GivesTheValueAsObject()
    {
        var bindings = new CelBindings().Add("x", 5).Add("ids", new long[] { 7 });

        Assert.Equal(5L, bindings["x"]);
        Assert.Throws<ArgumentException>(() => bindings["missing"]);
    }

    [Fact]
    public void Clear_RemovesAllValues()
    {
        var bindings = new CelBindings().Add("x", 1L);
        bindings.Clear();

        Assert.Equal(CelErrorCode.Unbound, Error("x", bindings).Code);
        bindings["x"] = 3L;
        Assert.Equal(3L, Evaluate<long>("x", bindings));
    }

    // The collection initializer calls Add<T>: by the static type, and a repeated name is ArgumentException.
    [Fact]
    public void CollectionInitializer_IsAdd()
    {
        var bindings = new CelBindings { { "x", 1L }, { "d", "text" } };

        Assert.Equal("text", Evaluate<string>("d", bindings));
        Assert.Throws<ArgumentException>(() => new CelBindings { { "x", 1L }, { "x", 2L } });
    }

    private static T Evaluate<T>(string text, CelBindings bindings) =>
        Environment.Compile<T>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static CelError Error(string text, CelBindings bindings) =>
        Assert.Single(Environment.Compile<CelValue>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);
}
