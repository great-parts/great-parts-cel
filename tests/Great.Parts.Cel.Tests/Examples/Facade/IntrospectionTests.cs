using System;
using System.Linq;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The description of an environment, read-only: variables with their types, the fields of the input included; functions that
// an expression calls by name, with overloads and descriptions, without operators and internal names; object and enumeration
// types; the input type; the limit. Every list is in ordinal order by name.
[Trait("Requirement", "F-13")]
public sealed class IntrospectionTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .UseInput<Order>()
        .AddVariable<long>("limit")
        .AddFunction<string, bool>("isKpp", s => s.Length == 9)
        .SetDefaultCostLimit(10_000)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void Variables_IncludeTheFieldsOfTheInput_InOrdinalOrder()
    {
        string[] names = [.. Environment.Variables.Select(variable => variable.Name)];

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Contains("limit", names);
        Assert.Contains("total", names);
        Assert.Contains("lines", names);
        Assert.Equal(CelType.Double, Environment.Variables.Single(variable => variable.Name == "total").Type);
        Assert.Equal(CelType.ListOf(CelType.ObjectNamed("shop.OrderLine")), Environment.Variables.Single(variable => variable.Name == "lines").Type);
    }

    [Fact]
    public void Functions_HaveNamesThatAreCalledByName()
    {
        string[] names = [.. Environment.Functions.Select(function => function.Name)];

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Contains("size", names);
        Assert.Contains("isKpp", names);
        Assert.DoesNotContain("_+_", names);
        Assert.DoesNotContain("@in", names);
        Assert.DoesNotContain(names, name => name[0] is '@' or '_');
    }

    [Fact]
    public void ObjectTypesInputTypeAndLimit()
    {
        string[] types = [.. Environment.ObjectTypes.Select(type => type.Name)];

        Assert.Equal(["shop.Address", "shop.Contact", "shop.Customer", "shop.Order", "shop.OrderLine"], types);
        Assert.Equal("shop.Order", Environment.InputType?.Name);
        Assert.Equal(10_000UL, Environment.DefaultCostLimit);
        Assert.Empty(Environment.EnumTypes);
    }

    [Fact]
    public void EnvironmentWithoutInputAndLimit()
    {
        CelEnvironment plain = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow();

        Assert.Null(plain.InputType);
        Assert.Null(plain.DefaultCostLimit);
        Assert.Empty(plain.Variables);
        Assert.Empty(plain.ObjectTypes);
    }
}
