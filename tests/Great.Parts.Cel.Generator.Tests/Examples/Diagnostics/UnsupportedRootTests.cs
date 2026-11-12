using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Diagnostics;

// GPCEL001: a type that [CelObject] lists and that the context cannot describe. The place is the argument of the attribute
// (the text `typeof(...)`), the level is Error, and the rest of the context is still written. The reason of each kind is the
// text of the table of reasons of the generator page; {0} is the display name of the type as C# writes it (the keywords for
// the primitives, the arguments of a generic type in angle brackets, no `global::`). A kind that the page lists has one row.
public sealed class UnsupportedRootTests
{
    private const string Cel = "CEL has no integers wider than 64 bits; use long or string";

    private const string NoType = " has no CEL type; use a string or your own type";

    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "S-2")]
    [InlineData("Acme.Ref", "public ref struct Ref { public int A; }", "Acme.Ref", "ref structs cannot be stored in a CEL value")]
    [InlineData("Acme.Box<>", "public class Box<T> { public T? Value { get; set; } }", "Acme.Box<T>", "open generic types and type parameters cannot be described without reflection; list a closed type")]
    [InlineData("int*", "", "int*", "pointers are not data")]
    [InlineData("System.Action", "", "System.Action", "delegates are not data")]
    [InlineData("System.Type", "", "System.Type", "System.Type is not data")]
    [InlineData("System.Threading.Tasks.Task", "", "System.Threading.Tasks.Task", "tasks are not data")]
    [InlineData("System.IO.Stream", "", "System.IO.Stream", "streams are not data")]
    [InlineData("System.IO.MemoryStream", "", "System.IO.MemoryStream", "streams are not data")]
    [InlineData("System.Collections.Generic.IEnumerable<int>", "", "System.Collections.Generic.IEnumerable<int>", "System.Collections.Generic.IEnumerable<int> has no size and no index; use an array, a list or a collection with a size")]
    [InlineData("System.Collections.ArrayList", "", "System.Collections.ArrayList", "non-generic collections have no element type; use a typed collection")]
    [InlineData("char", "", "char", "char is a UTF-16 code unit, not a code point; use string")]
    [InlineData("System.Int128", "", "System.Int128", Cel)]
    [InlineData("System.UInt128", "", "System.UInt128", Cel)]
    [InlineData("System.Numerics.BigInteger", "", "System.Numerics.BigInteger", Cel)]
    [InlineData("System.DateOnly", "", "System.DateOnly", "System.DateOnly is not supported; use DateTimeOffset or string")]
    [InlineData("System.TimeOnly", "", "System.TimeOnly", "System.TimeOnly is not supported; use DateTimeOffset or string")]
    [InlineData("System.Text.Json.JsonDocument", "", "System.Text.Json.JsonDocument", "JsonDocument is released by the application; use JsonElement")]
    [InlineData("System.Text.Json.Nodes.JsonNode", "", "System.Text.Json.Nodes.JsonNode", "JsonNode is mutable; use JsonElement")]
    [InlineData("System.Text.Json.Nodes.JsonObject", "", "System.Text.Json.Nodes.JsonObject", "JsonNode is mutable; use JsonElement")]
    [InlineData("System.Uri", "", "System.Uri", "System.Uri" + NoType)]
    [InlineData("System.Version", "", "System.Version", "System.Version" + NoType)]
    [InlineData("System.Net.IPAddress", "", "System.Net.IPAddress", "System.Net.IPAddress" + NoType)]
    [InlineData("System.Text.RegularExpressions.Regex", "", "System.Text.RegularExpressions.Regex", "System.Text.RegularExpressions.Regex" + NoType)]
    [InlineData("Great.Parts.Cel.CelBindings", "", "Great.Parts.Cel.CelBindings", "Great.Parts.Cel.CelBindings" + NoType)]
    [InlineData("Acme.Old", "[System.Obsolete] public class Old { public int A { get; set; } }", "Acme.Old", "Acme.Old is marked [Obsolete]")]
    [InlineData("Acme.Fresh", "[System.Diagnostics.CodeAnalysis.Experimental(\"EXP01\")] public class Fresh { public int A { get; set; } }", "Acme.Fresh", "Acme.Fresh is marked [Experimental]")]
    [InlineData("Acme.Slow", "[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(\"x\")] public class Slow { public int A { get; set; } }", "Acme.Slow", "Acme.Slow is marked [RequiresUnreferencedCode]")]
    [InlineData("Acme.Dynamic", "[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(\"x\")] public class Dynamic { public int A { get; set; } }", "Acme.Dynamic", "Acme.Dynamic is marked [RequiresDynamicCode]")]
    [InlineData("System.Collections.Generic.List<int>", "", "System.Collections.Generic.List<int>", "System.Collections.Generic.List<int> is not an object type; list the element type")]
    [InlineData("int", "", "int", "int has its own CEL type")]
    [InlineData("int?", "", "int?", "list the underlying type")]
    public void GPCEL001_RootOfAnUnsupportedKind_IsAnErrorAtTheArgument(string type, string declaration, string shown, string reason)
    {
        Diagnostic diagnostic = GeneratorExample.Single(Source(type, declaration), "GPCEL001");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Type '" + shown + "' cannot be used in a CEL type context: " + reason, diagnostic.GetMessage());
        Assert.Equal("typeof(" + type + ")", GeneratorExample.Place(diagnostic));
        Assert.Equal("Type cannot be used by CEL expressions", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel001", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
        Assert.Equal("Great.Parts.Cel.Generator", diagnostic.Descriptor.Category);
    }

    // The message classes and the well-known types of protobuf are found by full name (the compilation holds the stub of
    // IMessage and of MessageDescriptor): a message is described by the provider of Great.Parts.Cel.Protobuf, an interface of
    // messages and a well-known type are not data of a context.
    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("Acme.Pb.Msg", "Acme.Pb.Msg", "protobuf messages are described by Great.Parts.Cel.Protobuf, not by a type context")]
    [InlineData("Google.Protobuf.IMessage", "Google.Protobuf.IMessage", "list the message class, not the interface")]
    [InlineData("Google.Protobuf.WellKnownTypes.Timestamp", "Google.Protobuf.WellKnownTypes.Timestamp", "well-known protobuf types are read only as fields of messages")]
    public void GPCEL001_RootThatIsAProtobufType_IsAnErrorAtTheArgument(string type, string shown, string reason)
    {
        Diagnostic diagnostic = GeneratorExample.Single(Source(type, ""), "GPCEL001", more: [GeneratorExample.ProtobufStub]);

        Assert.Equal("Type '" + shown + "' cannot be used in a CEL type context: " + reason, diagnostic.GetMessage());
        Assert.Equal("typeof(" + type + ")", GeneratorExample.Place(diagnostic));
    }

    // The root that is refused is left out and the rest of the context is written: the code of the other root is created,
    // and the refused one is not in it.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL001_OtherRootsOfTheContext_AreStillWritten()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(System.IO.Stream))]
            [CelObject(typeof(Good))]
            public partial class Ctx : CelTypeContext { }
            public class Good { public long V { get; set; } }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source));

        Assert.Equal(["GPCEL001"], GeneratorExample.Reported(result).Select(diagnostic => diagnostic.Id));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("\"Acme.Good\"", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Stream", text, System.StringComparison.Ordinal);
    }

    // An enumeration may be a root and describes the enumeration; two attributes for one type are one root.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL001_EnumerationRoot_IsAllowed()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(Color))]
            [CelObject(typeof(Color))]
            public partial class Ctx : CelTypeContext { }
            public enum Color { Red, Green = 5 }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source));

        Assert.Empty(GeneratorExample.Reported(result));
        Assert.Contains("\"Acme.Color\"", GeneratorExample.CreatedText(result), System.StringComparison.Ordinal);
    }

    private static string Source(string type, string declaration) =>
        GeneratorExample.Preamble + "[CelObject(typeof(" + type + "))]\npublic partial class Ctx : CelTypeContext { }\n" + declaration + "\n";
}
