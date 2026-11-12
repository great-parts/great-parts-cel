using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Special;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// The place rule inside collections ("Place rule inside collections" of the page of the created code): an element of a list that the
// created code reads has the declared element type of the member, so List<Animal> is list(samples.Animal) and an instance of a derived class
// in it is an Animal, as type() and the fields say. The sample class Zoo has three lists: of the abstract class Animal, of the sealed
// class Dog and of the class Cat that is not sealed and has a sealed subclass Lion that no context lists. An abstract class and an
// interface are always paired with their description, a class that is not sealed only when the instance is of another type, a
// sealed class never. A list that the application builds itself has to hold values that are of the declared type.
public sealed class GeneratedPlaceTests
{
    private static readonly CelEnvironment Environment = Builder(PlaceContext.Default)
        .AddVariable<Zoo>("z")
        .AddVariable("animals", CelType.ListOf(CelType.ObjectNamed("samples.Animal")))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    public void ListOfAnAbstractClass_HoldsAnimalsWhateverTheirClass()
    {
        CelBindings bindings = new CelBindings().Add("z", Sample());

        Assert.True(Evaluate<bool>(Environment, "z.animals.size() == 3 && z.animals.all(a, type(a) == samples.Animal)", bindings));
        Assert.True(Evaluate<bool>(Environment, "z.animals.map(a, a.name) == ['rex', 'tom', 'leo']", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(Environment, "z.animals[0].tricks").Code);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void ListOfASealedClass_HoldsItsInstancesAsTheyAre()
    {
        CelBindings bindings = new CelBindings().Add("z", Sample());

        Assert.True(Evaluate<bool>(Environment, "z.dogs.size() == 1 && type(z.dogs[0]) == samples.Dog && z.dogs[0].tricks == 3 && z.dogs[0].name == 'rex'", bindings));
    }

    // A class that is not sealed: an instance of the declared class is read as it is, and an instance of a derived class — Lion, which no
    // context lists and the index does not describe — is read as a Cat: the type of the element is the declared one and its fields are
    // those of the Cat. Two such elements with equal fields are equal.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ListOfAClassThatIsNotSealed_HoldsDerivedInstancesAsTheDeclaredClass()
    {
        CelBindings bindings = new CelBindings().Add("z", Sample());

        Assert.True(Evaluate<bool>(Environment, "z.cats.size() == 2 && z.cats.all(c, type(c) == samples.Cat)", bindings));
        Assert.True(Evaluate<bool>(Environment, "z.cats[1].lives == 9 && z.cats[1].name == 'n'", bindings));
        Assert.True(Evaluate<bool>(Environment, "z.cats[0] == z.cats[1]", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(Environment, "z.cats[1].mane").Code);
    }

    // The type of the member as an expression sees it: a list of the declared element type, not of what the instances are.
    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("z.animals", "list(samples.Animal)")]
    [InlineData("z.dogs", "list(samples.Dog)")]
    [InlineData("z.cats", "list(samples.Cat)")]
    [InlineData("z.animals[0]", "samples.Animal")]
    public void ListMember_HasTheDeclaredElementType(string text, string type)
    {
        Assert.Equal(type, Environment.Compile<CelValue>(text).GetProgramOrThrow().ResultType.ToString());
    }

    // A list that the application builds itself and declares list(samples.Animal) has to hold values of that type: the description
    // is taken from the context and CelValue.FromObject pairs the instance with it. A bare Dog in such a list is described as Dog and
    // does not fit: the check against the declared type fails, and the library does not widen the fit.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ListBuiltByTheApplication_FitsWhenItsElementsArePairedWithTheDeclaredType()
    {
        CelObjectType animal = PlaceContext.Default.ObjectTypes.Single(type => type.Name == "samples.Animal");
        var dog = new Dog { Name = "rex", Tricks = 3 };
        CelBindings paired = new CelBindings().Add("animals", CelList.Create(CelValue.FromObject(animal, dog)));
        CelBindings bare = new CelBindings().Add("animals", new List<object> { dog });

        Assert.True(Evaluate<bool>(Environment, "animals[0].name == 'rex' && type(animals[0]) == samples.Animal", paired));
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>("animals[0].name").GetProgramOrThrow().Evaluate(bare, cancellationToken: System.Threading.CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(result.Errors).Code);
    }

    private static Zoo Sample() => new()
    {
        Animals = [new Dog { Name = "rex", Tricks = 3 }, new Cat { Name = "tom", Lives = 9 }, new Lion { Name = "leo", Lives = 7, Mane = 2.5 }],
        Dogs = [new Dog { Name = "rex", Tricks = 3 }],
        Cats = [new Cat { Name = "n", Lives = 9 }, new Lion { Name = "n", Lives = 9, Mane = 2.0 }],
    };
}
