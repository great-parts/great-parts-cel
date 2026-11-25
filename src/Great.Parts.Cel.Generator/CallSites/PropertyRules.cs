using System.Linq;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The types that the simple entry reads without a description of the application, as the rule of the static type of the data writes
// them (the page docs/type-mapping.md, "Data of the simple entry"): the values of the list, the lists and dictionaries of the list of
// elements, and the reason for any other type. The CEL type of such a property is not decided here: the created file declares the
// variable with AddVariable<T>, and the library gives the CEL type of T. A class, a structure or an interface of the application, and
// the six forms of a collection of them, are read by ClassProperties, which asks this class which types are those.
internal static class PropertyRules
{
    internal const string ReasonObject = "object";

    internal const string ReasonCannotDescribe = "a type that a type context cannot describe";

    internal const string ReasonNestedAnonymous = "a nested anonymous object";

    internal const string ReasonNotAccessible = "not accessible from the created file";

    internal const string ReasonCollection = "a collection outside the list";

    internal const string ReasonOutsideList = "a type outside the list";

    internal const string ReasonTypeParameter = "a type parameter of the enclosing method";

    internal const string ReasonNotIdentifier = "not a CEL identifier";

    private const string Guid = "System.Guid";

    private const string Int128 = "System.Int128";

    private const string UInt128 = "System.UInt128";

    private const string BigInteger = "System.Numerics.BigInteger";

    private const string DateTimeOffset = "System.DateTimeOffset";

    private const string DateTime = "System.DateTime";

    private const string TimeSpan = "System.TimeSpan";

    private const string JsonElement = "System.Text.Json.JsonElement";

    private const string CelValue = "Great.Parts.Cel.Values.CelValue";

    private const string CelList = "Great.Parts.Cel.Values.CelList";

    private const string CelMap = "Great.Parts.Cel.Values.CelMap";

    private const string NamedList = "System.Collections.Generic.List`1";

    private const string NamedDictionary = "System.Collections.Generic.Dictionary`2";

    // The outcome of reading the type of a property against the list: read, refused for a reason that no description of the application
    // changes, or outside the list with the reason to give when no description of the application takes it.
    internal enum ListedRead : byte
    {
        Read,
        Refused,
        OutsideTheList,
    }

    // Reads the type of a property: the type as C# writes it with global::, or the reason it is not read.
    internal static ListedRead TryRead(Compilation compilation, ITypeSymbol type, out string csharpType, out string reason)
    {
        csharpType = string.Empty;
        reason = string.Empty;

        if (type.IsAnonymousType)
        {
            reason = ReasonNestedAnonymous;
            return ListedRead.Refused;
        }

        if (TypeNames.ContainsTypeParameter(type))
        {
            reason = ReasonTypeParameter;
            return ListedRead.Refused;
        }

        if (!IsAccessible(compilation, type))
        {
            reason = ReasonNotAccessible;
            return ListedRead.Refused;
        }

        csharpType = TypeNames.ClrName(type, annotated: false);
        if (IsValue(type) || IsCollection(type))
        {
            return ListedRead.Read;
        }

        reason = ReasonFor(type);
        return ListedRead.OutsideTheList;
    }

    // The row of a property type that is bound with the factory of CelValue: bool, the integers up to 64 bits, double, string, Guid,
    // CelList and CelMap, and the Nullable<> of the numbers and of bool. Every other row is bound by Add<T>.
    internal static DirectKind DirectOf(ITypeSymbol type, out bool nullable)
    {
        ITypeSymbol value = type;
        nullable = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        if (nullable)
        {
            value = ((INamedTypeSymbol)type).TypeArguments[0];
        }

        DirectKind kind = KindOf(value);
        if (nullable && kind is not (DirectKind.Bool or DirectKind.Int or DirectKind.UInt or DirectKind.Double))
        {
            nullable = false;
            return DirectKind.None;
        }

        return kind;
    }

    private static DirectKind KindOf(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
                return DirectKind.Bool;
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
                return DirectKind.Int;
            case SpecialType.System_Byte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
                return DirectKind.UInt;
            case SpecialType.System_Double:
                return DirectKind.Double;
            case SpecialType.System_String:
                return DirectKind.String;
            default:
                return IsNamed(type, Guid) ? DirectKind.Guid
                    : IsNamed(type, CelList) ? DirectKind.CelList
                    : IsNamed(type, CelMap) ? DirectKind.CelMap
                    : DirectKind.None;
        }
    }

    // A class, a structure or an interface of the application: a named type that is none of System, has no row and is no collection. The
    // types of the library and of protobuf are counted too, so that the description of the context refuses them with its reason.
    internal static bool IsApplicationObject(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface } named
        && type.SpecialType == SpecialType.None
        && !IsSystemType(named)
        && !IsCollectionType(type);

    // An array, or a type that is an enumerable and no string: what a property of that type is called a collection by.
    internal static bool IsCollectionType(ITypeSymbol type) =>
        type is IArrayTypeSymbol
        || (type.SpecialType != SpecialType.System_String && type.AllInterfaces.Any(static contract => contract.SpecialType == SpecialType.System_Collections_IEnumerable));

    // The element of X[], List<X>, IReadOnlyList<X> and ImmutableArray<X>, and the value of Dictionary<string, X> and
    // IReadOnlyDictionary<string, X>: the six forms of a collection that the created code binds; null for any other type.
    internal static ITypeSymbol? ElementOfCollectionForm(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol { IsSZArray: true } array)
        {
            return array.ElementType;
        }

        if (type is not INamedTypeSymbol { IsGenericType: true } named)
        {
            return null;
        }

        string name = TypeNames.FullName(named);
        if (name == NamedList || name == WellKnown.ReadOnlyList || name == WellKnown.ImmutableArray)
        {
            return named.TypeArguments[0];
        }

        return (name == NamedDictionary || name == WellKnown.ReadOnlyDictionary) && named.TypeArguments[0].SpecialType == SpecialType.System_String
            ? named.TypeArguments[1]
            : null;
    }

    // Tells whether a type is an application object or a Nullable<> of one, or a collection or an array of such a type at any depth.
    internal static bool MentionsApplicationObject(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return MentionsApplicationObject(nullable.TypeArguments[0]);
        }

        if (IsApplicationObject(type))
        {
            return true;
        }

        if (type is IArrayTypeSymbol array)
        {
            return MentionsApplicationObject(array.ElementType);
        }

        if (type is not INamedTypeSymbol { IsGenericType: true } named)
        {
            return false;
        }

        foreach (ITypeSymbol argument in named.TypeArguments)
        {
            if (MentionsApplicationObject(argument))
            {
                return true;
            }
        }

        return false;
    }

    // Tells whether a static type of the data is one of the dictionaries that the simple entry reads when it is passed as object:
    // an IReadOnlyDictionary<string, XV> of the list of elements, found as the type itself or through its interfaces.
    internal static bool IsListedDictionary(ITypeSymbol type)
    {
        if (IsReadOnlyDictionaryOfTheList(type))
        {
            return true;
        }

        foreach (INamedTypeSymbol contract in type.AllInterfaces)
        {
            if (IsReadOnlyDictionaryOfTheList(contract))
            {
                return true;
            }
        }

        return false;
    }

    // The name of a property as the created file writes it: a keyword of C# is written with @.
    internal static string Identifier(string name) =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? "@" + name : name;

    // The list of values that a property of a static type is: scalars, time, bytes, JSON, CelValue, CelList,
    // CelMap and Nullable<> of a scalar value type. A property type is read by its static type, so a row of a value is a type.
    private static bool IsValue(ITypeSymbol type)
    {
        if (IsScalar(type) || IsNamed(type, CelList) || IsNamed(type, CelMap))
        {
            return true;
        }

        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments[0].IsValueType
            && IsScalar(nullable.TypeArguments[0]);
    }

    // Rows 1 to 10: numbers, bool, string, Guid, bytes, time and JSON values.
    private static bool IsScalar(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Double:
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return true;
        }

        return IsBytes(type)
            || IsWide(type)
            || IsNamed(type, Guid)
            || IsNamed(type, DateTimeOffset)
            || IsNamed(type, DateTime)
            || IsNamed(type, TimeSpan)
            || IsNamed(type, JsonElement)
            || IsNamed(type, CelValue);
    }

    // byte[] or ReadOnlyMemory<byte>.
    private static bool IsBytes(ITypeSymbol type) =>
        type is IArrayTypeSymbol { IsSZArray: true, ElementType.SpecialType: SpecialType.System_Byte }
        || (type is INamedTypeSymbol { Name: "ReadOnlyMemory", TypeArguments.Length: 1 } memory
            && IsNamed(memory, WellKnown.ReadOnlyMemory)
            && memory.TypeArguments[0].SpecialType == SpecialType.System_Byte);

    // The integers wider than 64 bits.
    private static bool IsWide(ITypeSymbol type) =>
        IsNamed(type, Int128) || IsNamed(type, UInt128) || IsNamed(type, BigInteger);

    // The elements of a list and the values of a dictionary: the list of elements of the type mapping, a subset of the scalars.
    private static bool IsElement(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Double:
            case SpecialType.System_String:
            case SpecialType.System_Object:
            case SpecialType.System_Char:
                return true;
        }

        if (IsBytes(type) || IsWide(type) || IsNamed(type, Guid) || IsNamed(type, JsonElement) || IsNamed(type, CelValue) || IsNamed(type, CelList) || IsNamed(type, CelMap))
        {
            return true;
        }

        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && IsNullableElement(nullable.TypeArguments[0]);
    }

    private static bool IsNullableElement(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Double:
            case SpecialType.System_Char:
                return true;
        }

        return (IsBytes(type) && type.IsValueType) || IsWide(type) || IsNamed(type, Guid) || IsNamed(type, JsonElement) || IsNamed(type, CelValue);
    }

    // X[], List<X>, IReadOnlyList<X>, ImmutableArray<X>, Dictionary<string, XV> and IReadOnlyDictionary<string, XV>.
    private static bool IsCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol { IsSZArray: true } array)
        {
            return IsElement(array.ElementType);
        }

        if (type is not INamedTypeSymbol { IsGenericType: true } named)
        {
            return false;
        }

        string name = TypeNames.FullName(named);
        if (name == NamedList || name == WellKnown.ReadOnlyList || name == WellKnown.ImmutableArray)
        {
            return IsElement(named.TypeArguments[0]);
        }

        return (name == NamedDictionary || name == WellKnown.ReadOnlyDictionary)
            && named.TypeArguments[0].SpecialType == SpecialType.System_String
            && IsElement(named.TypeArguments[1]);
    }

    private static bool IsReadOnlyDictionaryOfTheList(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } named
        && TypeNames.FullName(named) == WellKnown.ReadOnlyDictionary
        && named.TypeArguments[0].SpecialType == SpecialType.System_String
        && IsElement(named.TypeArguments[1]);

    private static bool IsNamed(ITypeSymbol type, string metadataName) =>
        type is INamedTypeSymbol named && TypeNames.FullName(named) == metadataName;

    // The reason for a type that is not read: object, a collection, or another type that has no row.
    private static string ReasonFor(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Object)
        {
            return ReasonObject;
        }

        return IsCollectionType(type) ? ReasonCollection : ReasonOutsideList;
    }

    private static bool IsSystemType(INamedTypeSymbol type)
    {
        string name = TypeNames.NamespaceName(type.ContainingNamespace);
        return name == "System" || name.StartsWith("System.", System.StringComparison.Ordinal);
    }

    // A type the created file can name: not private, protected or file-local anywhere in the chain of containing types or in the
    // arguments of a generic type, as the compilation decides it for the assembly of the application.
    private static bool IsAccessible(Compilation compilation, ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsAccessible(compilation, array.ElementType);
            case INamedTypeSymbol named:
                if (named.IsFileLocal || !compilation.IsSymbolAccessibleWithin(named.OriginalDefinition, compilation.Assembly))
                {
                    return false;
                }

                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    if (!IsAccessible(compilation, argument))
                    {
                        return false;
                    }
                }

                return true;
            default:
                return true;
        }
    }
}
