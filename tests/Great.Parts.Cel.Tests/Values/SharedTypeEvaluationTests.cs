using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Values.SharedTypeSamples;

namespace Great.Parts.Cel.Tests.Values;

// A value whose description the type index dropped as equal to another ("Types that two generated contexts share"): a pair of
// the description of the context Zoo, in an environment where the description of the context Shelter stays, in either order
// of the two AddTypes calls. It is described, fits, answers type() and compares as the one that stays.
public sealed class SharedTypeEvaluationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void PairOfTheDroppedDescription_FitsListOfTheTypeAndItsFieldIsRead(bool zooFirst)
    {
        World world = World.Create(zooFirst);
        CelValue animals = CelValue.FromList(CelList.Create(
            CelValue.FromObject(world.Zoo, new Dog { Name = "Rex" }),
            CelValue.FromObject(world.Shelter, new Dog { Name = "Mia" })));

        CelBindings bindings = new CelBindings().Add<CelValue>("animals", animals);

        Assert.Equal("Rex/Mia", world.Evaluate("animals[0].name + '/' + animals[1].name", bindings).GetValueOrThrow());
    }

    [Fact]
    public static void PairOfADescriptionThatIsNeitherInTheIndexNorDropped_DoesNotFitEvenWithTheSameName()
    {
        World world = World.Create(true);
        var stranger = new AnimalType();
        CelValue animals = CelValue.FromList(CelList.Create(CelValue.FromObject(stranger, new Dog())));

        CelEvalResult<object> result = world.Evaluate("size(animals)", new CelBindings().Add<CelValue>("animals", animals));

        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void TypeOfAPairOfTheDroppedDescription_IsTheNameThatStays(bool zooFirst)
    {
        World world = World.Create(zooFirst);
        CelBindings bindings = new CelBindings()
            .Add<CelValue>("a", CelValue.FromObject(world.Zoo, new Dog()))
            .Add<CelValue>("animals", CelValue.FromList(CelList.Create(CelValue.FromObject(world.Zoo, new Dog()))));

        Assert.Equal(true, world.Evaluate("type(a) == acme.Animal && type(animals[0]) == acme.Animal", bindings).GetValueOrThrow());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void PairOfTheDroppedDescription_IsEqualToAPairAndToAnInstanceOfTheOneThatStays(bool zooFirst)
    {
        World world = World.Create(zooFirst);
        CelValue animals = CelValue.FromList(CelList.Create(
            CelValue.FromObject(world.Zoo, new Dog { Name = "Rex" }),
            CelValue.FromObject(world.Shelter, new Dog { Name = "Rex" }),
            CelValue.FromObject(world.Zoo, new Dog { Name = "Tom" })));
        CelBindings bindings = new CelBindings()
            .Add<CelValue>("animals", animals)
            .Add<object>("a", new Dog { Name = "Rex" })
            .Add<object>("b", new Dog { Name = "Tom" });

        Assert.Equal(true, world.Evaluate("animals[0] == a && a == animals[0] && animals[0] == animals[1] && animals[1] == animals[0]", bindings).GetValueOrThrow());
        Assert.Equal(true, world.Evaluate("animals[2] == b && b == animals[2]", bindings).GetValueOrThrow());
        Assert.Equal(false, world.Evaluate("animals[0] == b || b == animals[0] || animals[0] == animals[2]", bindings).GetValueOrThrow());
    }

    // The pair of the description of the plan is read by the bound number, the pair of the dropped description by name through
    // the alias: the same value, the same error at the same place and the same charge.
    [Theory]
    [InlineData(true, "a.name", "Rex", "ok:Rex;")]
    [InlineData(false, "a.name", "Rex", "ok:Rex;")]
    [InlineData(true, "has(a.name)", "Rex", "ok:True;")]
    [InlineData(false, "has(a.name)", null, "ok:False;")]
    [InlineData(true, "a.name", null, "err:UnexpectedNull@1:")]
    [InlineData(false, "a.name", null, "err:UnexpectedNull@1:")]
    public static void SelectionThroughTheAlias_GivesTheValueTheErrorAndTheChargeOfTheBoundNumber(bool zooFirst, string expression, string? name, string expected)
    {
        World world = World.Create(zooFirst);
        var dog = new Dog { Name = name };
        CelBindings byNumber = new CelBindings().Add<CelValue>("a", CelValue.FromObject(world.Shelter, dog));
        CelBindings byAlias = new CelBindings().Add<CelValue>("a", CelValue.FromObject(world.Zoo, dog));

        string fast = Observe(world, expression, byNumber);
        string aliased = Observe(world, expression, byAlias);

        Assert.StartsWith(expected, aliased, StringComparison.Ordinal);
        Assert.Equal(fast, aliased);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void ObjectRules_TreatAPairOfTheDroppedDescriptionAsTheOneThatStaysOnlyWithTheIndex(bool zooFirst)
    {
        World world = World.Create(zooFirst);
        TypeIndex types = world.Environment.Declarations.Types;
        var stranger = new AnimalType();
        CelValue pair = CelValue.FromObject(world.Zoo, new Dog());
        CelValue strangerPair = CelValue.FromObject(stranger, new Dog());

        Assert.Same(world.Shelter, ObjectRules.Describe(pair, types));
        Assert.Same(world.Zoo, ObjectRules.Describe(pair, null));
        Assert.Same(stranger, ObjectRules.Describe(strangerPair, types));
        Assert.True(ObjectRules.TryDescribe(pair, types, out CelObjectType? description, out _));
        Assert.Same(world.Shelter, description);
        Assert.True(ObjectRules.Fits(pair, world.Zoo.Type, types));
        Assert.False(ObjectRules.Fits(pair, world.Zoo.Type, null));
        Assert.False(ObjectRules.Fits(strangerPair, world.Zoo.Type, types));
        Assert.True(ObjectRules.TryEnterPlace(pair, world.Zoo.Type, types, out CelValue placed, out string? actualType));
        Assert.Equal(pair, placed);
        Assert.Null(actualType);
        Assert.False(ObjectRules.TryEnterPlace(strangerPair, world.Zoo.Type, types, out _, out string? strangerType));
        Assert.Equal("acme.Animal", strangerType);
    }

    // The value or the first error with its place, then the units that the evaluation charged.
    private static string Observe(World world, string expression, CelBindings bindings)
    {
        CelEvalResult<object> result = world.Evaluate(expression, bindings);
        string outcome;
        if (result.IsSuccess)
        {
            outcome = "ok:" + result.GetValueOrThrow();
        }
        else
        {
            CelError error = Assert.Single(result.Errors);
            outcome = "err:" + error.Code + "@" + error.Location.Offset + ":" + error.Message;
        }

        return outcome + ";" + world.Cost(expression, bindings);
    }
}
