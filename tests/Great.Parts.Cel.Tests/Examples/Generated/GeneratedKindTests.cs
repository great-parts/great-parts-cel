using System.Linq;
using Google.Protobuf;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Kinds;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// The kinds of types that a context describes (the object rows of the type mapping, the sample class Holder): a class, a record class, a
// structure, a record struct, an interface, a closed generic type, a nested type, a type of another assembly. A value of the
// declared type of a member is read by the description of that type; the names of types are the default names unless Name is given.
public sealed class GeneratedKindTests
{
    private const string PageName = "Great.Parts.Cel.Generator.Samples.Kinds.Page_Great_Parts_Cel_Generator_Samples_Kinds_Item";

    private const string InnerName = "Great.Parts.Cel.Generator.Samples.Kinds.Outer.Inner";

    private static readonly CelEnvironment Environment = Builder(KindsContext.Default)
        .AddVariable<Holder>("h")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    public void ClassesRecordsAndStructures_AreReadByTheirMembers()
    {
        CelBindings bindings = new CelBindings().Add("h", Holder());

        Assert.True(Evaluate<bool>(Environment, "h.cls.id == 1 && h.cls.name == 'n'", bindings));
        Assert.True(Evaluate<bool>(Environment, "h.rec.id == 2 && h.rec.name == 'r'", bindings));
        Assert.True(Evaluate<bool>(Environment, "h.str.id == 3 && h.str.name == 's'", bindings));
        Assert.True(Evaluate<bool>(Environment, "h.pnt.x == 4 && h.pnt.y == 5", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(h.cls) == " + Samples + ".Kinds.ClassKind && type(h) == samples.Holder", bindings));
    }

    // An interface is read by the properties that the interface and its base interfaces declare, whatever class the instance
    // is; a member of the class that the interface lacks is not a field, and the type of the value is the declared one.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void Interface_IsReadByItsOwnAndInheritedProperties()
    {
        CelBindings bindings = new CelBindings().Add("h", Holder());

        Assert.True(Evaluate<bool>(Environment, "h.shape.label == 'c' && h.shape.area == 3.0 && h.shape.id == 6", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(h.shape) == Great.Parts.Cel.Generator.Samples.Kinds.IShape", bindings));
        CelError error = CompileError(Environment, "h.shape.radius");
        Assert.Equal(CelErrorCode.NoSuchField, error.Code);
    }

    // The default name of a closed generic type is the name of the definition and the names of the arguments with their dots
    // written as _, and a nested type's is the name with the plus written as a dot. An expression can write either.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GenericAndNestedTypes_HaveTheirDefaultNames()
    {
        CelBindings bindings = new CelBindings().Add("h", Holder());

        Assert.Contains(PageName, KindsContext.Default.ObjectTypes.Select(type => type.Name));
        Assert.Contains(InnerName, KindsContext.Default.ObjectTypes.Select(type => type.Name));
        Assert.True(Evaluate<bool>(Environment, "type(h.page) == " + PageName + " && h.page.total == 1 && h.page.rows[0].sku == 's'", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(h.nested) == " + InnerName + " && h.nested.depth == 2", bindings));
    }

    // A type of another assembly is described like a type of the application: Google.Protobuf.ByteString, whose property Span
    // has a ref struct type and whose indexer is an indexer, so neither is a member and neither is a field.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void TypeOfAnotherAssembly_IsReadAndItsNonMembersAreNotFields()
    {
        CelBindings bindings = new CelBindings().Add("h", Holder());

        Assert.True(Evaluate<bool>(Environment, "h.foreign.length == 3 && !h.foreign.isEmpty && h.foreign.memory == b'\\x01\\x02\\x03'", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(Environment, "h.foreign.span").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(Environment, "h.foreign.item").Code);
    }

    // A member of a type that is not null and one that is: the structure and the class that can be null.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void NullObjectMembers_AreTheNullValue()
    {
        CelBindings bindings = new CelBindings().Add("h", Holder());

        Assert.True(Evaluate<bool>(Environment, "!has(h.noCls) && h.noCls == null && !has(h.optStr) && h.optStr == null", bindings));
    }

    // Objects are values: two instances of a type with equal fields are equal, and an instance is not equal to a value of
    // another type, without an error; the check allows the comparison of two types only through dyn.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ObjectsOfTheContext_AreEqualByFieldsAndNotEqualToOtherTypes()
    {
        var other = Holder();
        other.Cls = new ClassKind { Id = 1, Name = "n" };
        var bindings = new CelBindings().Add("h", Holder()).Add("g", other);
        CelEnvironment environment = Builder(KindsContext.Default).AddVariable<Holder>("h").AddVariable<Holder>("g").Build().GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "h == g && h.cls == g.cls", bindings));
        Assert.True(Evaluate<bool>(environment, "dyn(h.cls) != h.rec && dyn(h.cls) != 1", bindings));
    }

    private static Holder Holder() => new()
    {
        Cls = new ClassKind { Id = 1, Name = "n" },
        Rec = new RecordKind(2, "r"),
        Str = new StructKind { Id = 3, Name = "s" },
        Pnt = new PointKind(4, 5),
        Shape = new Circle { Id = 6, Label = "c", Area = 3.0, Radius = 1.0 },
        Page = new Page<Item> { Rows = [new Item { Sku = "s" }], Total = 1 },
        Nested = new Outer.Inner { Depth = 2 },
        Foreign = ByteString.CopyFrom(1, 2, 3),
    };
}
