using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Values.SharedTypeSamples;

namespace Great.Parts.Cel.Tests.Declarations;

// Types that two generated contexts share (docs/design/objects.md, "Types that two generated contexts share"): descriptions of
// equal form of one .NET type, or enumerations with the same constants, from different type contexts are one declaration,
// whichever order the contexts are added in; everything else stays DuplicateType.
public sealed class SharedTypeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void EqualFormInTwoContexts_IsOneDeclarationAndTheSmallerSourceStaysInEitherOrder(bool zooFirst)
    {
        World world = World.Create(zooFirst);

        TypeIndex types = world.Environment.Declarations.Types;

        Assert.Single(types.ObjectTypes);
        Assert.True(types.TryGetObjectType("acme.Animal", out CelObjectType? byName));
        Assert.Same(world.Shelter, byName);
        Assert.True(types.TryGetByClrType(typeof(Animal), out CelObjectType? byClrType));
        Assert.Same(world.Shelter, byClrType);
        Assert.Same(world.Shelter, types.Describe(new Dog()));
        Assert.Same(world.Shelter, types.Resolve(world.Zoo));
        Assert.Same(world.Shelter, types.Resolve(world.Shelter));
    }

    [Fact]
    public static void EqualFormInThreeContexts_KeepsTheDescriptionOfTheSmallestSourceAndAliasesTheOthers()
    {
        var c = new AnimalType();
        var a = new AnimalType();
        var b = new AnimalType();

        CelBuildResult result = Build(new Context("C", [c], []), new Context("A", [a], []), new Context("B", [b], []));

        TypeIndex types = result.GetEnvironmentOrThrow().Declarations.Types;
        Assert.Same(a, Assert.Single(types.ObjectTypes).Value);
        Assert.Same(a, types.Resolve(b));
        Assert.Same(a, types.Resolve(c));
    }

    [Fact]
    public static void IndexOfOneContext_HasNoAliasAndDescribesADescriptionAsItself()
    {
        var animal = new AnimalType();

        TypeIndex types = Build(new Context("Zoo", [animal], [])).GetEnvironmentOrThrow().Declarations.Types;

        Assert.Same(animal, types.Resolve(animal));
        Assert.Same(animal, types.Describe(new Animal()));
        Assert.Same(animal, Assert.Single(types.ObjectTypes).Value);
    }

    [Theory]
    [InlineData("acme.Animal", "title", false)]
    [InlineData("acme.Animal", "name", true)]
    [InlineData("acme.Beast", "name", false)]
    public static void DescriptionsOfOneDotNetTypeThatDifferInAFieldNameATypeOrTheName_AreDuplicateType(string name, string field, bool isInt)
    {
        CelType? type = isInt ? CelType.Int : null;
        var zoo = new Context("Zoo", [new AnimalType()], []);
        var shelter = new Context("Shelter", [new AnimalType(name, field, type)], []);

        foreach (CelBuildResult result in new[] { Build(zoo, shelter), Build(shelter, zoo) })
        {
            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.Errors);
            Assert.All(result.Errors, static error => Assert.Equal(CelErrorCode.DuplicateType, error.Code));
        }
    }

    [Fact]
    public static void DescriptionOfASourceThatIsNoContext_IsDuplicateTypeWhateverItsForm()
    {
        var context = new Context("Zoo", [new AnimalType()], []);
        CelTypeProvider plain = CelTypeProvider.Create("Shelter", new AnimalType());
        CelTypeProvider otherPlain = CelTypeProvider.Create("Farm", new AnimalType());

        foreach (CelBuildResult result in new[] { Build(context, plain), Build(plain, context), Build(plain, otherPlain) })
        {
            Assert.False(result.IsSuccess);
            Assert.All(result.Errors, static error => Assert.Equal(CelErrorCode.DuplicateType, error.Code));
        }
    }

    [Fact]
    public static void TwoDescriptionsOfOneContext_AreDuplicateTypeNamingTheContextTwice()
    {
        var context = new Context("Zoo", [new AnimalType(), new AnimalType()], []);

        CelBuildResult result = Build(context);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            static error => error.Code == CelErrorCode.DuplicateType
                && error.Message == "type 'acme.Animal' is declared by both type source 'Zoo' and type source 'Zoo'");
    }

    [Fact]
    public static void OneNameForTwoDotNetTypes_IsDuplicateTypeWithTheSourcesInTheOrderOfTheirTexts()
    {
        var zoo = new Context("Zoo", [new AnimalType()], []);
        var shelter = new Context("Shelter", [new DogType("acme.Animal")], []);
        const string Message = "type 'acme.Animal' is declared by both type source 'Shelter' and type source 'Zoo'";

        foreach (CelBuildResult result in new[] { Build(zoo, shelter), Build(shelter, zoo) })
        {
            CelError error = Assert.Single(result.Errors);
            Assert.Equal((CelErrorCode.DuplicateType, Message), (error.Code, error.Message));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void EnumerationWithTheSameConstantsInTheSameOrder_IsOneDeclaration(bool zooFirst)
    {
        var zoo = new Context("Zoo", [], [new CelEnumType("acme.Kind", [new("Cat", 1), new("Dog", 2)])]);
        var shelter = new Context("Shelter", [], [new CelEnumType("acme.Kind", [new("Cat", 1), new("Dog", 2)])]);

        CelBuildResult result = zooFirst ? Build(zoo, shelter) : Build(shelter, zoo);

        TypeIndex types = result.GetEnvironmentOrThrow().Declarations.Types;
        Assert.True(types.TryGetEnumConstant("acme.Kind.Dog", out long dog));
        Assert.Equal(2, dog);
    }

    [Theory]
    [InlineData("Dog", 2, "Cat", 1)]
    [InlineData("Cat", 1, "Dog", 3)]
    [InlineData("Cat", 1, "Wolf", 2)]
    public static void EnumerationWithOtherConstantsOrAnotherOrder_IsDuplicateType(string first, long firstValue, string second, long secondValue)
    {
        var zoo = new Context("Zoo", [], [new CelEnumType("acme.Kind", [new("Cat", 1), new("Dog", 2)])]);
        var shelter = new Context("Shelter", [], [new CelEnumType("acme.Kind", [new(first, firstValue), new(second, secondValue)])]);

        CelBuildResult result = Build(zoo, shelter);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.DuplicateType, "type 'acme.Kind' is declared by both type source 'Shelter' and type source 'Zoo'"),
            (error.Code, error.Message));
    }

    [Fact]
    public static void EnumerationAndObjectTypeOfOneName_AreDuplicateTypeAcrossContexts()
    {
        var zoo = new Context("Zoo", [new AnimalType()], []);
        var shelter = new Context("Shelter", [], [new CelEnumType("acme.Animal", [new("Cat", 1)])]);

        CelBuildResult result = Build(zoo, shelter);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, static error => error.Code == CelErrorCode.DuplicateType);
    }

    private static CelBuildResult Build(params CelTypeProvider[] providers)
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        foreach (CelTypeProvider provider in providers)
        {
            builder = builder.AddTypes(provider);
        }

        return builder.Build();
    }
}
