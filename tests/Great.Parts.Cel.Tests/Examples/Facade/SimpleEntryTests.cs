using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The simple entry: one call, a text and the data — a dictionary, a dictionary passed as object, or an anonymous object whose
// type is registered — the value of T or CelException with all errors. The built-in limit is 1 000 000 units; costLimit
// replaces it. The registration here is written by hand, the way the generated code of the call sites writes it, so these rows
// do not need the generator. Rows that pass data whose static type is object or a class show the run-time outcome of what the
// generator warns about (GPCEL010).
#pragma warning disable GPCEL010 // the rows pass data of the static type object or a class on purpose, to show the run-time outcome
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryTests
{
    private static readonly string[] Tags = ["a", "b"];

    static SimpleEntryTests()
    {
        CelAnonymousData.Register(
            new { x = default(long) },
            static builder => builder.AddVariable<long>("x"),
            static (data, bindings) => bindings.Add("x", data.x));
        CelAnonymousData.Register(
            new { name = default(string), tags = default(string[]) },
            static builder => builder.AddVariable<string>("name").AddVariable<string[]>("tags"),
            static (data, bindings) => bindings.Add("name", data.name).Add("tags", data.tags));
    }

    [Fact]
    public void AnonymousObject_OfARegisteredType()
    {
        Assert.Equal(14L, CelExpression.Evaluate<long>("(x + 2) / 3", new { x = 40L }, cancellationToken: CancellationToken.None));
        Assert.True(CelExpression.Evaluate<bool>("name.startsWith('a') && 'b' in tags", new { name = "abc", tags = Tags }, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void Dictionary_DeclaresTheVariablesByTheValues()
    {
        var variables = new Dictionary<string, object?> { ["x"] = 40L, ["name"] = "abc", ["when"] = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), ["none"] = null };

        Assert.Equal(14L, CelExpression.Evaluate<long>("(x + 2) / 3", variables, cancellationToken: CancellationToken.None));
        Assert.Equal(2026L, CelExpression.Evaluate<long>("when.getFullYear()", variables, cancellationToken: CancellationToken.None));
        Assert.True(CelExpression.Evaluate<bool>("none == null && size(name) == 3", variables, cancellationToken: CancellationToken.None));
        Assert.Equal(3L, CelExpression.Evaluate<long>("1 + 2", cancellationToken: CancellationToken.None));
    }

    // A dictionary passed as object is a dictionary; a typed dictionary gives one type to all its variables.
    [Fact]
    public void DictionaryAsObject_IsADictionary()
    {
        object data = new Dictionary<string, long> { ["a"] = 1, ["b"] = 2 };

        Assert.Equal(3L, CelExpression.Evaluate<long>("a + b", data, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void OwnLimit_ReplacesTheBuiltInOne()
    {
        Assert.Equal(1_000_000UL, CelExpression.DefaultCostLimit);
        Assert.Equal(14L, CelExpression.Evaluate<long>("(x + 2) / 3", new { x = 40L }, costLimit: 50_000, cancellationToken: CancellationToken.None));

        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<long>("(x + 2) / 3", new { x = 40L }, costLimit: 1, cancellationToken: CancellationToken.None));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(exception.Errors).Code);
    }

    // The built-in limit stops an expression that grows without a limit of its own.
    [Fact]
    public void BuiltInLimit_StopsAnExpensiveExpression()
    {
        const string Text = "[1,2,3,4,5,6,7,8,9,10].map(a, [1,2,3,4,5,6,7,8,9,10].map(b, [1,2,3,4,5,6,7,8,9,10].map(c, [1,2,3,4,5,6,7,8,9,10].map(d, [1,2,3,4,5,6,7,8,9,10].map(e, [1,2,3,4,5,6,7,8,9,10].map(f, a + b + c + d + e + f))))))";

        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<CelValue>(Text, new { x = 1L }, cancellationToken: CancellationToken.None));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Errors_AreCelExceptionWithAllOfThem()
    {
        CelException compile = Assert.Throws<CelException>(() => CelExpression.Evaluate<long>("a + b", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None));
        Assert.Equal(2, compile.Errors.Length);
        Assert.All(compile.Errors, error => Assert.Equal(CelErrorCode.UndeclaredReference, error.Code));

        CelException evaluate = Assert.Throws<CelException>(() => CelExpression.Evaluate<long>("x / 0", new { x = 1L }, cancellationToken: CancellationToken.None));
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(evaluate.Errors).Code);

        CelException extension = Assert.Throws<CelException>(() => CelExpression.Evaluate<string>("name.upperAscii()", new Dictionary<string, object?> { ["name"] = "a" }, cancellationToken: CancellationToken.None));
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(extension.Errors).Code);
    }

    // A type outside the closed list of values is TypeNotDeclared with its .NET name, not an evaluation on part of the data.
    [Fact]
    public void TypeOutsideTheList_IsTypeNotDeclared()
    {
        CelError order = Single(() => CelExpression.Evaluate<bool>("true", new Order(), cancellationToken: CancellationToken.None));
        Assert.Equal(CelErrorCode.TypeNotDeclared, order.Code);
        Assert.Equal(typeof(Order).FullName, order.Subject);

        CelError price = Single(() => CelExpression.Evaluate<bool>("true", new Dictionary<string, object?> { ["price"] = 1.5m }, cancellationToken: CancellationToken.None));
        Assert.Equal(CelErrorCode.TypeNotDeclared, price.Code);
        Assert.Equal(typeof(decimal).FullName, price.Subject);

        CelError list = Single(() => CelExpression.Evaluate<bool>("true", new Dictionary<string, object?> { ["orders"] = new List<Order>() }, cancellationToken: CancellationToken.None));
        Assert.Equal(typeof(List<Order>).FullName, list.Subject);

        CelError keys = Single(() => CelExpression.Evaluate<bool>("true", (object)new Dictionary<int, long> { [1] = 1 }, cancellationToken: CancellationToken.None));
        Assert.Equal(typeof(Dictionary<int, long>).FullName, keys.Subject);
    }

    // The anonymous type that no call site registered: the name the compiler gave and the advice to pass it directly.
    [Fact]
    public void UnregisteredAnonymousType_IsTypeNotDeclaredWithItsCompilerName()
    {
        object data = new { unregistered = 1L, other = "x" };

        CelError error = Single(() => CelExpression.Evaluate<long>("unregistered", data, cancellationToken: CancellationToken.None));

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Equal(data.GetType().FullName, error.Subject);
        Assert.StartsWith("<>f__AnonymousType", error.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyThatIsNotAName_IsInvalidVariableName()
    {
        Assert.Equal(CelErrorCode.InvalidVariableName, Single(() => CelExpression.Evaluate<long>("1", new Dictionary<string, object?> { ["a-b"] = 1L }, cancellationToken: CancellationToken.None)).Code);
    }

    [Fact]
    public void ArgumentsAndCancellation()
    {
        Assert.Throws<ArgumentNullException>("expression", () => CelExpression.Evaluate<long>(null!, new { x = 1L }, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentNullException>("data", () => CelExpression.Evaluate<long>("1", (object)null!, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("1", new Dictionary<string, object?> { ["t"] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) }, cancellationToken: CancellationToken.None));

        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => CelExpression.Evaluate<long>("x", new { x = 1L }, cancellationToken: source.Token));
    }

    private static CelError Single(Action call) => Assert.Single(Assert.Throws<CelException>(call).Errors);
}
