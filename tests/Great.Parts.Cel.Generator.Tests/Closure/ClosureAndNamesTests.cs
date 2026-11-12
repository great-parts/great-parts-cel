using System;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Closure;

// What the closure describes, the members it reads and the names it gives, which the examples of the specification leave to the tests
// of the developer: a type reached by several paths and a cycle, the types that only an excluded member reaches, the enumerations,
// hiding and overriding, explicit implementations, `required` and `init`, `IgnoreMembers` of a root against a member of another type,
// and the names of closed generic, nested and global types. The created code is compiled with its sources and read through the
// descriptions.
public sealed class ClosureAndNamesTests
{
    private const string Header = "using System;\nusing System.Collections.Generic;\nusing Great.Parts.Cel.Values;\nnamespace Acme;\n";

    private static string[] ObjectNames(CelTypeProvider provider) =>
        [.. provider.ObjectTypes.Select(type => type.Name).Order(StringComparer.Ordinal)];

    private static string[] FieldNames(CelTypeProvider provider, string type) =>
        [.. provider.ObjectTypes.Single(item => item.Name == type).Fields.Select(field => field.Name).Order(StringComparer.Ordinal)];

    [Fact]
    [Trait("Requirement", "F-2")]
    public void TypeReachedByManyPathsAndInACycle_IsDescribedOnce()
    {
        const string Source = Header + """
            [CelObject(typeof(A))] public partial class Ctx : CelTypeContext { }
            public class A { public B One { get; set; } = new(); public List<B> Many { get; set; } = new(); public Dictionary<string, B> ByName { get; set; } = new(); public B? Maybe { get; set; } }
            public class B { public A Back { get; set; } = new(); public long V { get; set; } }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        Assert.Equal(["Acme.A", "Acme.B"], ObjectNames(provider));
    }

    // A member is classified before its type is followed: a type that only an excluded member reaches is not described.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void TypeReachedOnlyByAnExcludedMember_IsNotDescribed()
    {
        const string Source = Header + """
            [CelObject(typeof(Holder))] public partial class Ctx : CelTypeContext { }
            public class Holder { public long Kept { get; set; } public Dictionary<double, Reached> Odd { get; set; } = new(); public System.IO.Stream? Body { get; set; } }
            public class Reached { public long V { get; set; } }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        Assert.Equal(["Acme.Holder"], ObjectNames(provider));
        Assert.Equal(["kept"], FieldNames(provider, "Acme.Holder"));
    }

    // An enumeration that a member reaches is an enumeration type of the context, with its constants in the order of the declaration; the
    // name of a root that is an enumeration is its Name, and the name of one that is reached is its default name.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void Enumerations_ConstantsAreInTheOrderOfTheDeclarationAndOnlyARootTakesName()
    {
        const string Source = Header + """
            [CelObject(typeof(Holder), Name = "acme.Holder")]
            [CelObject(typeof(Color), Name = "paint.Color")]
            public partial class Ctx : CelTypeContext { }
            public enum Color { Red, Green }
            public enum Letters { Zed, Alpha, Mid }
            public class Holder { public Letters Letter { get; set; } public Color Paint { get; set; } }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        Assert.Equal(["Acme.Letters", "paint.Color"], [.. provider.EnumTypes.Select(type => type.Name).Order(StringComparer.Ordinal)]);
        Assert.Equal(["acme.Holder"], ObjectNames(provider));
        string text = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Source)));
        Assert.Contains("new(\"Zed\", 0L), new(\"Alpha\", 1L), new(\"Mid\", 2L)", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void HidingAndOverriding_TheMemberOfTheMostDerivedTypeIsTheOnlyOne()
    {
        const string Source = Header + """
            [CelObject(typeof(Derived))] public partial class Ctx : CelTypeContext { }
            public class Base { public long X { get; set; } public virtual long Y { get; set; } public long Z; }
            public class Derived : Base { public new string X { get; set; } = ""; public override long Y { get; set; } }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelObjectType type = loaded.Provider("Acme.Ctx").ObjectTypes.Single();

        Assert.Equal(["x", "y", "z"], [.. type.Fields.Select(field => field.Name).Order(StringComparer.Ordinal)]);
        Assert.Equal(CelType.String, type.Fields.Single(field => field.Name == "x").Type);
        Assert.Equal(CelType.Int, type.Fields.Single(field => field.Name == "y").Type);
    }

    // An explicit implementation of an interface member is no member of the type; `required` and `init` properties and public fields are
    // read like the others; an identifier with `@` is the name without it.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ExplicitImplementation_IsNotAMemberAndRequiredInitFieldsAndVerbatimNamesAre()
    {
        const string Source = Header + """
            [CelObject(typeof(Named))] public partial class Ctx : CelTypeContext { }
            public interface IHasName { string Name { get; } }
            public class Named : IHasName
            {
                string IHasName.Name => "";
                public required string R { get; set; }
                public string I { get; init; } = "";
                public long F;
                public long @event { get; set; }
            }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        Assert.Equal(["event", "f", "i", "r"], FieldNames(provider, "Acme.Named"));
    }

    // IgnoreMembers of a root hides the member of that type by its C# name and no member of another type that the closure reaches.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void IgnoreMembersOfARoot_DoesNotHideTheMemberOfTheSameNameInAnotherType()
    {
        const string Source = Header + """
            [CelObject(typeof(Holder), IgnoreMembers = new[] { "Id" })] public partial class Ctx : CelTypeContext { }
            public class Holder { public long Id { get; set; } public Inner Link { get; set; } = new(); }
            public class Inner { public long Id { get; set; } }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        Assert.Equal(["link"], FieldNames(provider, "Acme.Holder"));
        Assert.Equal(["id"], FieldNames(provider, "Acme.Inner"));
    }

    // Two members that [CelIgnore] hides are not a duplicate of the name that [CelName] gives them both.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelIgnoreOnBothOfTwoMembersWithOneName_IsNoDuplicate()
    {
        const string Source = Header + """
            [CelObject(typeof(Holder))] public partial class Ctx : CelTypeContext { }
            public class Holder
            {
                [CelIgnore] [CelName("x")] public long A { get; set; }
                [CelIgnore] [CelName("x")] public long B { get; set; }
                public long C { get; set; }
            }
            """;

        Assert.Empty(GeneratorExample.Reported(GeneratorExample.Run(GeneratorExample.Compile(Source))));
        using var loaded = new GeneratorExample.Loaded(Source);

        Assert.Equal(["c"], FieldNames(loaded.Provider("Acme.Ctx"), "Acme.Holder"));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void NamesOfTypes_ClosedGenericNestedAndGlobal()
    {
        const string Source = Header + """
            [CelObject(typeof(Holder))] public partial class Ctx : CelTypeContext { }
            public sealed class Tag { public long V { get; set; } }
            public sealed class Pair<A, B> { public A First { get; set; } = default!; public B Second { get; set; } = default!; }
            public class Outer
            {
                public sealed class Box<T> { public T Item { get; set; } = default!; }
                public sealed class Inner { public long V { get; set; } }
            }
            public class Holder
            {
                public Pair<int, Tag> Mixed { get; set; } = new();
                public Outer.Box<Tag> Boxed { get; set; } = new();
                public Pair<Outer.Inner, long> Nested { get; set; } = new();
            }
            """;
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        Assert.Equal(
            [
                "Acme.Holder",
                "Acme.Outer.Box_Acme_Tag",
                "Acme.Outer.Inner",
                "Acme.Pair_Acme_Outer_Inner_long",
                "Acme.Pair_int_Acme_Tag",
                "Acme.Tag",
            ],
            ObjectNames(provider));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void TypeInTheGlobalNamespace_HasNoPrefix()
    {
        const string Source = "using Great.Parts.Cel.Values;\n[CelObject(typeof(Global))] public partial class GlobalCtx : CelTypeContext { }\npublic class Global { public long V { get; set; } }\n";
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("GlobalCtx");

        Assert.Equal(["Global"], ObjectNames(provider));
    }
}
