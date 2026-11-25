using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// Classes of the application in the properties of an anonymous object, in the forms that the examples do not use: nested collections,
// a nullable structure, an enumeration, an interface and an abstract class in a list, null values in a dictionary, several forms that
// reach one class, a class without members. The registrations and the descriptions are created in this assembly by the generator
// of call sites, so the values are the same under the JIT and under Native AOT.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "S-1")]
public sealed class GeneratedClassPropertyTests
{
    [Fact]
    public void NestedCollections_AreReadByTheirElements()
    {
        var data = new
        {
            rows = new List<List<Tag>> { new() { new Tag { Name = "a" } }, new() },
            groups = new Dictionary<string, Tag[]> { ["x"] = [new Tag { Name = "b" }] },
        };

        Assert.True(CelExpression.Evaluate<bool>(
            "rows.size() == 2 && rows[0][0].name == 'a' && rows[1].size() == 0 && groups['x'][0].name == 'b'",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NullableStructure_IsTheStructureOrNull()
    {
        var data = new { one = (Pair?)new Pair { V = 5 }, none = (Pair?)null };

        Assert.True(CelExpression.Evaluate<bool>("one.v == 5 && none == null", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ListAndDictionaryOfNullableStructures_ReadNoValueAsNull()
    {
        var data = new
        {
            maybe = new List<Pair?> { null, new Pair { V = 3 } },
            byName = new Dictionary<string, Pair?> { ["a"] = null, ["b"] = new Pair { V = 4 } },
        };

        Assert.True(CelExpression.Evaluate<bool>(
            "maybe[0] == null && maybe[1].v == 3 && byName['a'] == null && byName['b'].v == 4",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void EnumerationMember_IsItsNumber()
    {
        var data = new { ranked = new List<Ranked> { new() { Level = Level.High, Title = "top" } } };

        Assert.True(CelExpression.Evaluate<bool>("ranked[0].level == 1 && ranked[0].title == 'top'", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InterfaceAndAbstractClassInLists_AreReadByTheMembersOfTheDeclaredType()
    {
        var data = new
        {
            animals = new List<IAnimal> { new Dog { Name = "rex", Tricks = 3 } },
            vehicles = new Vehicle[] { new Truck { Plate = "T-1", Load = 9 } },
        };

        Assert.True(CelExpression.Evaluate<bool>("animals[0].name == 'rex' && vehicles[0].plate == 'T-1'", data, cancellationToken: TestContext.Current.CancellationToken));

        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>(
            "vehicles[0].load == 9",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CelErrorCode.NoSuchField, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void DictionaryOfClasses_HasNullValuesByTheAnnotation()
    {
        var data = new { byName = (IReadOnlyDictionary<string, Tag?>)new Dictionary<string, Tag?> { ["a"] = null, ["b"] = new Tag { Name = "t" } } };

        Assert.True(CelExpression.Evaluate<bool>("byName['a'] == null && byName['b'].name == 't'", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NullDictionaryAndNullArray_AreUnexpectedNull()
    {
        var dictionary = new { byName = (Dictionary<string, Tag>)null! };
        var array = new { tags = (Tag[])null! };

        CelException first = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>("byName.size() == 0", dictionary, cancellationToken: TestContext.Current.CancellationToken));
        CelException second = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>("tags.size() == 0", array, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(CelErrorCode.UnexpectedNull, Assert.Single(first.Errors).Code);
        Assert.Equal(CelErrorCode.UnexpectedNull, Assert.Single(second.Errors).Code);
    }

    // Two forms that reach one class: each has its environment, and both read the same description.
    [Fact]
    public void TwoFormsOfOneClass_AreBothRead()
    {
        Tag tag = new() { Name = "same" };

        Assert.True(CelExpression.Evaluate<bool>("tag.name == 'same'", new { tag }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("tag.name == 'same' && n == 1", new { tag, n = 1L }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("tags[0].name == 'same'", new { tags = ImmutableArray.Create(tag) }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ClassWithoutMembers_IsAnObject()
    {
        var data = new { blank = new Blank(), blanks = new List<Blank> { new() } };

        Assert.True(CelExpression.Evaluate<bool>("blank != null && blanks.size() == 1", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    // The list is not copied: each call reads the source as it is, so an element added between two calls is seen by the second.
    [Fact]
    public void ListOfClasses_IsReadFromTheSourceAtEachCall()
    {
        List<Tag> tags = [new() { Name = "a" }];
        var data = new { tags };

        Assert.Equal(1L, CelExpression.Evaluate<long>("tags.size()", data, cancellationToken: TestContext.Current.CancellationToken));
        tags.Add(new Tag { Name = "b" });
        Assert.Equal(2L, CelExpression.Evaluate<long>("tags.size()", data, cancellationToken: TestContext.Current.CancellationToken));
    }
}

internal sealed class Tag
{
    public string Name { get; init; } = string.Empty;
}

internal struct Pair
{
    public long V { get; init; }
}

internal enum Level
{
    Low,
    High,
}

internal sealed class Ranked
{
    public Level Level { get; init; }

    public string Title { get; init; } = string.Empty;
}

internal interface IAnimal
{
    string Name { get; }
}

internal sealed class Dog : IAnimal
{
    public string Name { get; init; } = string.Empty;

    public long Tricks { get; init; }
}

internal abstract class Vehicle
{
    public string Plate { get; init; } = string.Empty;
}

internal sealed class Truck : Vehicle
{
    public long Load { get; init; }
}

internal sealed class Blank;
