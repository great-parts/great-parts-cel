namespace Great.Parts.Cel.Generator;

// The full metadata names of the types that the generator recognizes. It references no package: every type of the library, of
// System.Text.Json and of protobuf is found by its name.
internal static class WellKnown
{
    internal const string CelObjectAttribute = "Great.Parts.Cel.Values.CelObjectAttribute";

    internal const string CelTypeContextOptionsAttribute = "Great.Parts.Cel.Values.CelTypeContextOptionsAttribute";

    internal const string CelIgnoreAttribute = "Great.Parts.Cel.Values.CelIgnoreAttribute";

    internal const string CelNameAttribute = "Great.Parts.Cel.Values.CelNameAttribute";

    internal const string CelTypeContext = "Great.Parts.Cel.Values.CelTypeContext";

    internal const string CelLibraryAssembly = "Great.Parts.Cel";

    internal const string CelProtobufAssembly = "Great.Parts.Cel.Protobuf";

    internal const string JsonIgnoreAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";

    internal const string JsonPropertyNameAttribute = "System.Text.Json.Serialization.JsonPropertyNameAttribute";

    internal const string Decimal128 = "System.Numerics.Decimal128";

    internal const string ReadOnlyMemory = "System.ReadOnlyMemory`1";

    internal const string ImmutableArray = "System.Collections.Immutable.ImmutableArray`1";

    internal const string JsonNodesNamespace = "System.Text.Json.Nodes";

    internal const string WellKnownTypesNamespace = "Google.Protobuf.WellKnownTypes";

    internal const string Message = "Google.Protobuf.IMessage";

    internal const string MessageOfType = "Google.Protobuf.IMessage`1";

    internal const string MessageDescriptor = "Google.Protobuf.Reflection.MessageDescriptor";

    internal const string NonGenericEnumerable = "System.Collections.IEnumerable";

    internal const string Enumerable = "System.Collections.Generic.IEnumerable`1";

    internal const string AsyncEnumerable = "System.Collections.Generic.IAsyncEnumerable`1";

    internal const string ReadOnlyList = "System.Collections.Generic.IReadOnlyList`1";

    internal const string List = "System.Collections.Generic.IList`1";

    internal const string ReadOnlySet = "System.Collections.Generic.IReadOnlySet`1";

    internal const string Set = "System.Collections.Generic.ISet`1";

    internal const string ReadOnlyDictionary = "System.Collections.Generic.IReadOnlyDictionary`2";

    internal const string Dictionary = "System.Collections.Generic.IDictionary`2";

    internal const string ReadOnlyCollection = "System.Collections.Generic.IReadOnlyCollection`1";
}
