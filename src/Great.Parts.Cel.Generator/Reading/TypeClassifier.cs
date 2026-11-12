using System;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The classification of a type: one function over a type, with one result, how the type is read or why it is not. The order of
// the tests is the one of the page docs/design/generator.md ("Which types are read"). In root mode — the type is the argument
// of [CelObject] — a type that has a CEL type of its own is refused with that reason and an object type or an enumeration is
// answered as such; the object type is then registered by the caller, with its name.
internal sealed class TypeClassifier
{
    private readonly CompilationFacts facts;
    private readonly TypeRegistry registry;

    internal TypeClassifier(CompilationFacts facts, TypeRegistry registry)
    {
        this.facts = facts;
        this.registry = registry;
    }

    // belowCollection: the type is an element, a key or a value of a collection, at any depth.
    internal Classification Classify(ITypeSymbol type, bool root, bool belowCollection = false) =>
        ClassifyStructural(type, root) ?? ClassifyNullableOrArray(type, root, belowCollection) ?? ClassifyNamed(type, root, belowCollection);

    private static Nullability PositionOf(ITypeSymbol type)
    {
        if (type.IsValueType)
        {
            return Nullability.NotNull;
        }

        return type.NullableAnnotation switch
        {
            NullableAnnotation.Annotated => Nullability.Nullable,
            NullableAnnotation.NotAnnotated => Nullability.NotNull,
            _ => Nullability.Oblivious,
        };
    }

    private static ObjectKind KindOf(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Struct => ObjectKind.Struct,
        TypeKind.Interface => ObjectKind.Interface,
        _ => ObjectKind.Class,
    };

    private static Classification? ClassifyStructural(ITypeSymbol type, bool root)
    {
        switch (type.TypeKind)
        {
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
                return Classification.Refuse(Reasons.Pointer);
            case TypeKind.TypeParameter:
                return Classification.Refuse(Reasons.OpenGeneric);
            case TypeKind.Dynamic:
                return root
                    ? Classification.Refuse(Reasons.OwnType("dynamic"))
                    : Classification.Ok(new ScalarShape("object", Nullability.Oblivious, ScalarKind.Object));
            case TypeKind.Error:
                return Classification.Refuse(Reasons.NoCelType(TypeNames.Display(type)));
            default:
                break;
        }

        if (type.IsRefLikeType)
        {
            return Classification.Refuse(Reasons.RefStruct);
        }

        bool open = TypeNames.ContainsTypeParameter(type) || type is INamedTypeSymbol { IsUnboundGenericType: true };
        return open ? Classification.Refuse(Reasons.OpenGeneric) : null;
    }

    private Classification? ClassifyNullableOrArray(ITypeSymbol type, bool root, bool belowCollection)
    {
        if (type is IArrayTypeSymbol array)
        {
            return ClassifyArray(array, root);
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return ClassifyNullable(nullable, root, belowCollection);
        }

        return null;
    }

    private Classification ClassifyArray(IArrayTypeSymbol array, bool root)
    {
        bool bytes = array.Rank == 1 && array.ElementType.SpecialType == SpecialType.System_Byte;
        if (root)
        {
            string display = TypeNames.Display(array);
            return Classification.Refuse(bytes ? Reasons.OwnType(display) : Reasons.NotAnObject(display));
        }

        string clrName = TypeNames.ClrName(array, true);
        if (bytes)
        {
            return Classification.Ok(new ScalarShape(clrName, PositionOf(array), ScalarKind.Bytes));
        }

        if (array.Rank > 1)
        {
            return Classification.Refuse(Reasons.MultiDimensional);
        }

        Classification element = ClassifyComponent(array.ElementType, "element");
        return element.Kind == ClassificationKind.Ok
            ? Classification.Ok(new CollectionShape(clrName, PositionOf(array), CollectionFamily.List, null, element.Shape!))
            : element;
    }

    private Classification ClassifyNullable(INamedTypeSymbol nullable, bool root, bool belowCollection)
    {
        if (root)
        {
            return Classification.Refuse(Reasons.UnderlyingType);
        }

        Classification inner = Classify(nullable.TypeArguments[0], false, belowCollection);
        return inner.Kind == ClassificationKind.Ok
            ? Classification.Ok(new NullableShape(TypeNames.ClrName(nullable, true), Nullability.NotNull, inner.Shape!))
            : inner;
    }

    private Classification ClassifyNamed(ITypeSymbol type, bool root, bool belowCollection)
    {
        if (type is not INamedTypeSymbol named)
        {
            return Classification.Refuse(Reasons.NoCelType(TypeNames.Display(type)));
        }

        return ClassifyEnum(named, root)
            ?? ClassifyByName(named)
            ?? ClassifyScalar(named, root)
            ?? ClassifyCollection(named, root, belowCollection)
            ?? ClassifyProtobuf(named, root)
            ?? ClassifyEnumerable(named)
            ?? ClassifySystem(named)
            ?? ClassifyMarked(named)
            ?? ClassifyObject(named, root);
    }

    private Classification? ClassifyEnum(INamedTypeSymbol named, bool root)
    {
        if (named.TypeKind != TypeKind.Enum)
        {
            return null;
        }

        if (root)
        {
            return Classification.RootEnum(named);
        }

        EnumEntry entry = registry.ReachEnum(named);
        return entry.NameValid
            ? Classification.Ok(new EnumShape(TypeNames.ClrName(named, true), Nullability.NotNull, entry.CelName))
            : Classification.BadName(entry.CelName);
    }

    // The kinds that the page lists by name: a delegate, char, the integers wider than 64 bits, DateOnly and TimeOnly,
    // JsonDocument and the types of System.Text.Json.Nodes, System.Type, tasks and streams (and the types derived from them).
    private static Classification? ClassifyByName(INamedTypeSymbol named)
    {
        if (named.TypeKind == TypeKind.Delegate)
        {
            return Classification.Refuse(Reasons.DelegateType);
        }

        if (named.SpecialType == SpecialType.System_Char)
        {
            return Classification.Refuse(Reasons.CharType);
        }

        if (TypeNames.NamespaceName(named.ContainingNamespace) == WellKnown.JsonNodesNamespace)
        {
            return Classification.Refuse(Reasons.JsonNodeType);
        }

        for (INamedTypeSymbol? current = named; current is not null; current = current.BaseType)
        {
            string? reason = ReasonByName(TypeNames.FullName(current), named);
            if (reason is not null)
            {
                return Classification.Refuse(reason);
            }
        }

        return null;
    }

    private static string? ReasonByName(string fullName, INamedTypeSymbol named)
    {
        if (fullName is "System.Int128" or "System.UInt128" or "System.Numerics.BigInteger")
        {
            return Reasons.WideInteger;
        }

        if (fullName is "System.DateOnly" or "System.TimeOnly")
        {
            return Reasons.DateTimeType(TypeNames.Display(named));
        }

        if (fullName is "System.Text.Json.JsonDocument")
        {
            return Reasons.JsonDocumentType;
        }

        if (fullName is "System.Type")
        {
            return Reasons.SystemType;
        }

        if (fullName is "System.IO.Stream")
        {
            return Reasons.StreamType;
        }

        return fullName is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task`1" or "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask`1"
            ? Reasons.TaskType
            : null;
    }

    private Classification? ClassifyScalar(INamedTypeSymbol named, bool root)
    {
        ScalarKind? kind = ScalarKinds.Of(named);
        if (kind is null)
        {
            return null;
        }

        if (root)
        {
            return Classification.Refuse(Reasons.OwnType(TypeNames.Display(named)));
        }

        if (kind is ScalarKind.Decimal or ScalarKind.Decimal128 && !facts.HasDecimal128)
        {
            return Classification.Decimal;
        }

        int size = kind == ScalarKind.JsonElement ? StructSizes.Of(named) : 0;
        return Classification.Ok(new ScalarShape(TypeNames.ClrName(named, true), PositionOf(named), kind.Value, size));
    }

    private Classification? ClassifyCollection(INamedTypeSymbol named, bool root, bool belowCollection)
    {
        CollectionInfo? info = CollectionAnalysis.Analyze(named);
        if (info is null)
        {
            return null;
        }

        if (root)
        {
            return Classification.Refuse(Reasons.NotAnObject(TypeNames.Display(named)));
        }

        if (info.Ambiguous is not null)
        {
            return Classification.Refuse(Reasons.MoreThanOne(info.Ambiguous));
        }

        // A set, a collection without an index and a structure that is a collection (other than ImmutableArray<T>) are copied or boxed
        // at each read of an element, and the cost has no charge for that: the first test that finds such a type under a collection
        // decides, before the reasons below it.
        if (belowCollection && IsCopiedAtEachRead(named, info.Family))
        {
            return Classification.Refuse(Reasons.CopiedAtEachRead(TypeNames.Display(named)));
        }

        return BuildCollection(named, info);
    }

    private static bool IsCopiedAtEachRead(INamedTypeSymbol named, CollectionFamily family) =>
        family is CollectionFamily.Set or CollectionFamily.Collection
        || (named.IsValueType && family != CollectionFamily.ImmutableArray);

    private Classification BuildCollection(INamedTypeSymbol named, CollectionInfo info)
    {
        Shape? key = null;
        if (info.KeyType is not null)
        {
            Classification keyResult = ClassifyComponent(info.KeyType, "key");
            if (keyResult.Kind != ClassificationKind.Ok)
            {
                return keyResult;
            }

            if (!IsDictionaryKey(keyResult.Shape!))
            {
                return Classification.Refuse(Reasons.DictionaryKey);
            }

            key = keyResult.Shape;
        }

        Classification element = ClassifyComponent(info.ElementType, info.KeyType is null ? "element" : "value");
        if (element.Kind != ClassificationKind.Ok)
        {
            return element;
        }

        if (info.Family == CollectionFamily.Set && !IsOrdered(element.Shape!))
        {
            return Classification.Refuse(Reasons.SetElement);
        }

        // A structure has no null, and one other than ImmutableArray<T> is boxed by the read, so its size is the charge of the box.
        bool isValueType = named.IsValueType;
        int size = isValueType && info.Family != CollectionFamily.ImmutableArray ? StructSizes.Of(named) : 0;
        return Classification.Ok(new CollectionShape(
            TypeNames.ClrName(named, true),
            PositionOf(named),
            info.Family,
            key,
            element.Shape!,
            isValueType,
            info.ReadOnlyCollection,
            size));
    }

    // An element, a key or a value of a collection: a reason of the type is the reason of the member, with the place named.
    private Classification ClassifyComponent(ITypeSymbol type, string kind)
    {
        Classification result = Classify(type, false, true);
        return result.Kind == ClassificationKind.Refused
            ? Classification.Refuse(Reasons.Component(kind, TypeNames.Display(type), result.Text))
            : result;
    }

    private static bool IsDictionaryKey(Shape key) =>
        key is EnumShape or ScalarShape { Kind: ScalarKind.Int or ScalarKind.UInt or ScalarKind.Bool or ScalarKind.String or ScalarKind.Guid };

    // The elements that CEL orders: numbers, bool, string, bytes, time and decimal, and what is read as one of them.
    private static bool IsOrdered(Shape element) => element switch
    {
        EnumShape => true,
        NullableShape nullable => IsOrdered(nullable.Underlying),
        ScalarShape scalar => scalar.Kind is not (ScalarKind.CelValue or ScalarKind.Object or ScalarKind.JsonElement or ScalarKind.CelList or ScalarKind.CelMap),
        _ => false,
    };

    private static Classification? ClassifyProtobuf(INamedTypeSymbol named, bool root)
    {
        if (TypeNames.NamespaceName(named.ContainingNamespace) == WellKnown.WellKnownTypesNamespace)
        {
            return Classification.Refuse(Reasons.WellKnownMessage);
        }

        if (TypeNames.FullName(named) is WellKnown.Message or WellKnown.MessageOfType)
        {
            return Classification.Refuse(Reasons.MessageInterface);
        }

        if (!IsMessageClass(named))
        {
            return null;
        }

        if (root)
        {
            return Classification.Refuse(Reasons.RootMessage);
        }

        string descriptor = TypeNames.ClrName(named, false) + ".Descriptor.FullName";
        return Classification.Ok(new ObjectShape(TypeNames.ClrName(named, true), PositionOf(named), descriptor, ObjectKind.Message, true, false));
    }

    // A message class of protoc: it implements IMessage and has a public static property Descriptor of the type
    // MessageDescriptor, both found by the full name.
    private static bool IsMessageClass(INamedTypeSymbol named)
    {
        if (named.TypeKind == TypeKind.Interface || !ImplementsMessage(named))
        {
            return false;
        }

        foreach (ISymbol member in named.GetMembers("Descriptor"))
        {
            if (member is IPropertySymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Type: INamedTypeSymbol type }
                && TypeNames.FullName(type) == WellKnown.MessageDescriptor)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsMessage(INamedTypeSymbol named)
    {
        foreach (INamedTypeSymbol implemented in named.AllInterfaces)
        {
            if (TypeNames.FullName(implemented) == WellKnown.Message)
            {
                return true;
            }
        }

        return false;
    }

    // An IEnumerable<T> or an IAsyncEnumerable<T> has no size and no index; a type that implements only the non-generic
    // IEnumerable has no element type. A class that implements IEnumerable<T> and none of the collection interfaces is an object.
    private static Classification? ClassifyEnumerable(INamedTypeSymbol named)
    {
        string fullName = TypeNames.FullName(named);
        if (fullName is WellKnown.Enumerable or WellKnown.AsyncEnumerable)
        {
            return Classification.Refuse(Reasons.NoSize(TypeNames.Display(named)));
        }

        bool nonGeneric = fullName == WellKnown.NonGenericEnumerable;
        bool generic = false;
        foreach (INamedTypeSymbol implemented in named.AllInterfaces)
        {
            string name = TypeNames.FullName(implemented);
            nonGeneric |= name == WellKnown.NonGenericEnumerable;
            generic |= name == WellKnown.Enumerable;
        }

        return nonGeneric && !generic ? Classification.Refuse(Reasons.NonGeneric) : null;
    }

    // A class, a structure or an interface of System or of the library that has no row: it would be read as an object by a
    // random set of properties. The library is decided by the assembly of the symbol, not by the namespace: a type of
    // Great.Parts.Cel.Generator.Samples or of the tests is data.
    private static Classification? ClassifySystem(INamedTypeSymbol named)
    {
        string ns = TypeNames.NamespaceName(named.ContainingNamespace);
        bool system = ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal);
        bool library = named.ContainingAssembly?.Name is WellKnown.CelLibraryAssembly or WellKnown.CelProtobufAssembly;
        return system || library ? Classification.Refuse(Reasons.NoCelType(TypeNames.Display(named))) : null;
    }

    private static Classification? ClassifyMarked(INamedTypeSymbol named)
    {
        string? mark = Attributes.Mark(named);
        return mark is null ? null : Classification.Refuse(Reasons.TypeMarked(TypeNames.Display(named), mark));
    }

    private Classification ClassifyObject(INamedTypeSymbol named, bool root)
    {
        if (named.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
        {
            return Classification.Refuse(Reasons.NoCelType(TypeNames.Display(named)));
        }

        if (root)
        {
            return Classification.RootObject(named);
        }

        ObjectEntry? entry = registry.ReachObject(named);
        if (entry is null)
        {
            return Classification.Limit;
        }

        if (!entry.NameValid)
        {
            return Classification.BadName(entry.CelName);
        }

        ObjectKind kind = KindOf(named);
        int size = kind == ObjectKind.Struct ? StructSizes.Of(named) : 0;
        var shape = new ObjectShape(TypeNames.ClrName(named, true), PositionOf(named), entry.CelName, kind, named.IsSealed, named.IsAbstract, size);
        return Classification.Ok(shape);
    }
}
