using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Pipeline;

// Equal inputs give equal bytes: two runs, two compilations of one source, and the same program written in another order (the
// attributes of the context, the members of a class, the files that hold the parts of a partial type). The text does not
// depend on the machine: only "\n" ends a line.
public sealed class DeterminismTests
{
    private const string Ordered = """
        using System.Collections.Generic;
        using Great.Parts.Cel.Values;
        namespace Acme;
        [CelObject(typeof(Alpha))]
        [CelObject(typeof(Beta))]
        public partial class Ctx : CelTypeContext { }
        public enum Color { Red, Green }
        public class Alpha { public long Zed { get; set; } public string Name { get; set; } = ""; public Color Color { get; set; } public Beta? Link { get; set; } }
        public class Beta { public List<long> Numbers { get; set; } = new(); public double Ratio { get; set; } }
        """;

    // The attributes are written in the other order, the members of Alpha too.
    private const string Shuffled = """
        using System.Collections.Generic;
        using Great.Parts.Cel.Values;
        namespace Acme;
        [CelObject(typeof(Beta))]
        [CelObject(typeof(Alpha))]
        public partial class Ctx : CelTypeContext { }
        public enum Color { Red, Green }
        public class Beta { public double Ratio { get; set; } public List<long> Numbers { get; set; } = new(); }
        public class Alpha { public Beta? Link { get; set; } public Color Color { get; set; } public string Name { get; set; } = ""; public long Zed { get; set; } }
        """;

    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "R-5")]
    public void TwoRunsAndTwoCompilationsOfOneSource_GiveTheSameText()
    {
        string first = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Ordered)));
        string second = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Ordered)));

        Assert.Equal(first, second);
        var compilation = GeneratorExample.Compile(Ordered);
        Assert.Equal(first, GeneratorExample.CreatedText(GeneratorExample.Run(compilation)));
        Assert.Equal(first, GeneratorExample.CreatedText(GeneratorExample.Run(compilation)));
    }

    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "R-5")]
    public void OrderOfAttributesAndMembers_DoesNotChangeTheText()
    {
        string ordered = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Ordered)));
        string shuffled = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Shuffled)));

        Assert.Equal(ordered, shuffled);
    }

    // The parts of a partial type are in two files, in either order: the members come in a different order from the compiler.
    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "R-5")]
    public void OrderOfThePartsOfAPartialType_DoesNotChangeTheText()
    {
        const string Context = "using Great.Parts.Cel.Values;\nnamespace Acme;\n[CelObject(typeof(Part))]\npublic partial class Ctx : CelTypeContext { }\n";
        const string One = "namespace Acme;\npublic partial class Part { public long A { get; set; } public long C { get; set; } }\n";
        const string Two = "namespace Acme;\npublic partial class Part { public long B { get; set; } public long D { get; set; } }\n";

        string forward = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Context, more: [One, Two])));
        string backward = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Context, more: [Two, One])));

        Assert.Equal(forward, backward);
        int a = forward.IndexOf("\"a\"", System.StringComparison.Ordinal);
        Assert.True(a >= 0 && a < forward.IndexOf("\"b\"", System.StringComparison.Ordinal) && forward.IndexOf("\"b\"", System.StringComparison.Ordinal) < forward.IndexOf("\"c\"", System.StringComparison.Ordinal));
    }

    // Two roots with one CEL name: the second is the one with the greater display name of the type (ordinal), whichever attribute comes
    // first in the text, so the message and the place are the same for both orders of the attributes.
    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    public void GPCEL004_PlaceDoesNotDependOnTheOrderOfTheAttributes()
    {
        const string Forward = GeneratorExample.Preamble + """
            [CelObject(typeof(A), Name = "acme.Thing")]
            [CelObject(typeof(B), Name = "acme.Thing")]
            public partial class Ctx : CelTypeContext { }
            public class A { public long V { get; set; } }
            public class B { public long V { get; set; } }
            """;
        const string Backward = GeneratorExample.Preamble + """
            [CelObject(typeof(B), Name = "acme.Thing")]
            [CelObject(typeof(A), Name = "acme.Thing")]
            public partial class Ctx : CelTypeContext { }
            public class A { public long V { get; set; } }
            public class B { public long V { get; set; } }
            """;

        Diagnostic first = GeneratorExample.Single(Forward, "GPCEL004");
        Diagnostic second = GeneratorExample.Single(Backward, "GPCEL004");

        Assert.Equal(first.GetMessage(), second.GetMessage());
        Assert.Equal("typeof(B)", GeneratorExample.Place(first));
        Assert.Equal("typeof(B)", GeneratorExample.Place(second));
    }

    // The order of the diagnostics of a run does not depend on the order of the sources either: the same set of reports.
    [Fact]
    [Trait("Requirement", "S-2")]
    public void DiagnosticsOfTwoRuns_AreTheSameSet()
    {
        const string Source = "using Great.Parts.Cel.Values;\nnamespace Acme;\n[CelObject(typeof(System.IO.Stream))]\n[CelObject(typeof(char))]\npublic partial class Ctx : CelTypeContext { }\n";

        string[] first = [.. GeneratorExample.Reported(GeneratorExample.Run(GeneratorExample.Compile(Source))).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage())];
        string[] second = [.. GeneratorExample.Reported(GeneratorExample.Run(GeneratorExample.Compile(Source))).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage())];

        Assert.Equal(first, second);
        Assert.Equal(2, first.Length);
    }
}
