namespace Great.Parts.Cel.Generator;

// The reasons that a type or a member cannot be read, as the page docs/design/generator.md writes them (the table of reasons). A
// reason is an English text without a final period: it is the argument {1} of GPCEL001 and {2} of GPCEL002, and the text that
// GetExcludedFieldReason gives to the type checker. {t} of the page is the display name of the type, which the callers pass.
internal static class Reasons
{
    internal const string RefStruct = "ref structs cannot be stored in a CEL value";

    internal const string OpenGeneric = "open generic types and type parameters cannot be described without reflection; list a closed type";

    internal const string Pointer = "pointers are not data";

    internal const string DelegateType = "delegates are not data";

    internal const string SystemType = "System.Type is not data";

    internal const string TaskType = "tasks are not data";

    internal const string StreamType = "streams are not data";

    internal const string NonGeneric = "non-generic collections have no element type; use a typed collection";

    internal const string JsonDocumentType = "JsonDocument is released by the application; use JsonElement";

    internal const string JsonNodeType = "JsonNode is mutable; use JsonElement";

    internal const string MultiDimensional = "multidimensional arrays are not supported; use an array of arrays or a list";

    internal const string WellKnownMessage = "well-known protobuf types are read only as fields of messages";

    internal const string MessageInterface = "list the message class, not the interface";

    internal const string RootMessage = "protobuf messages are described by Great.Parts.Cel.Protobuf, not by a type context";

    internal const string UnderlyingType = "list the underlying type";

    internal const string DictionaryKey = "dictionary keys must be string, an integer, bool, an enumeration or Guid";

    internal const string SetElement = "set elements must be numbers, bool, string, bytes, timestamp, duration or decimal, which CEL orders";

    internal static string NoSize(string type) => type + " has no size and no index; use an array, a list or a collection with a size";

    internal static string NoCelType(string type) => type + " has no CEL type; use a string or your own type";

    internal static string TypeMarked(string type, string attribute) => type + " is marked [" + attribute + "]";

    internal static string MemberMarked(string attribute) => "the member is marked [" + attribute + "]";

    internal static string NotAnObject(string type) => type + " is not an object type; list the element type";

    internal static string OwnType(string type) => type + " has its own CEL type";

    internal static string CopiedAtEachRead(string type) =>
        type + " is copied at each read of an element, which the cost does not charge; use a list, an array or a dictionary";

    internal static string Component(string kind, string type, string reason) =>
        "the " + kind + " type " + type + " cannot be read: " + reason;

    internal static string MoreThanOne(string collectionInterface) =>
        "implements " + collectionInterface + " for more than one element type";

    internal static string InvalidTypeName(string name) => "'" + name + "' is not a valid CEL qualified type name";

    // The reasons of GPCEL012, a type that AllowObjectLiterals lists and the created code cannot create ("Object literals" of the page
    // of the created code): {t} is the display name of the type, which the callers pass where the reason names a member.
    internal const string LiteralInterface = "an interface cannot be created";

    internal const string LiteralAbstract = "an abstract class cannot be created";

    internal const string LiteralNoConstructor = "it has no public constructor";

    internal const string LiteralSeveralConstructors = "it has no public parameterless constructor and more than one public constructor with parameters";

    internal static string LiteralParameterWithoutField(string parameter) =>
        "the parameter '" + parameter + "' of its only public constructor matches no field";

    internal static string LiteralParameterType(string parameter) =>
        "the type of the parameter '" + parameter + "' of its only public constructor differs from the type of its field";

    internal static string LiteralRequiredMember(string member) =>
        "the required member '" + member + "' cannot be set by an object literal";
}
