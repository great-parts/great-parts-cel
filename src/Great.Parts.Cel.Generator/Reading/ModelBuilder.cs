using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The model of a context from what the reading found: the checks of the whole (two types with one CEL name, GPCEL004), the names
// of the nested classes and fields of the created code, and the sorted collections.
internal static class ModelBuilder
{
    private const string ClassPrefix = "Type_";

    private const string EnumPrefix = "Enum_";

    // A type that is described, with what the check of the names needs.
    private sealed record Named(string CelName, string Display, LocationInfo Place, ObjectEntry? Object, EnumEntry? Enum);

    internal static ContextModel Build(INamedTypeSymbol context, TypeRegistry registry, DiagnosticSink sink)
    {
        List<Named> survivors = CheckNames(context.Name, registry, sink);
        var objects = new List<ObjectTypeModel>();
        var enums = new List<EnumModel>();
        var objectClasses = new HashSet<string>(StringComparer.Ordinal);
        var enumFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (Named item in survivors)
        {
            if (item.Object is { } entry)
            {
                objects.Add(ObjectModel(entry, UniqueName(ClassPrefix, entry.CelName, objectClasses)));
            }
            else if (item.Enum is { } enumeration)
            {
                enums.Add(EnumModelOf(enumeration, UniqueName(EnumPrefix, enumeration.CelName, enumFields)));
            }
        }

        return new ContextModel(
            TypeNames.NamespaceName(context.ContainingNamespace),
            ContextDeclaration.ContainingTypes(context),
            context.Name,
            context.Name,
            TypeNames.FullName(context) + ".g.cs",
            EquatableArray.From(objects),
            EquatableArray.From(enums));
    }

    // Two types with one CEL name: the one with the greater display name is the second; it is reported at its place and left
    // out. The types come back in the order of the CEL name, ordinal.
    private static List<Named> CheckNames(string contextName, TypeRegistry registry, DiagnosticSink sink)
    {
        var all = new List<Named>();
        foreach (ObjectEntry entry in registry.Objects)
        {
            all.Add(new Named(entry.CelName, entry.Display, entry.ReachedBy, entry, null));
        }

        foreach (EnumEntry entry in registry.Enums)
        {
            all.Add(new Named(entry.CelName, entry.Display, entry.ReachedBy, null, entry));
        }

        all.Sort(static (left, right) =>
        {
            int result = string.CompareOrdinal(left.CelName, right.CelName);
            return result != 0 ? result : string.CompareOrdinal(left.Display, right.Display);
        });

        var survivors = new List<Named>();
        var first = new Dictionary<string, Named>(StringComparer.Ordinal);
        foreach (Named item in all)
        {
            if (first.TryGetValue(item.CelName, out Named? owner))
            {
                sink.Report(DiagnosticIds.DuplicateType, item.Place, owner.Display, item.Display, contextName, item.CelName);
                continue;
            }

            first[item.CelName] = item;
            survivors.Add(item);
        }

        return survivors;
    }

    // The CEL name with every dot written as _, behind a prefix; _2, _3 … are appended when two names would collide.
    private static string UniqueName(string prefix, string celName, HashSet<string> used)
    {
        string baseName = prefix + celName.Replace('.', '_');
        string name = baseName;
        for (int suffix = 2; !used.Add(name); suffix++)
        {
            name = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return name;
    }

    private static ObjectTypeModel ObjectModel(ObjectEntry entry, string className)
    {
        INamedTypeSymbol symbol = entry.Symbol;
        ObjectKind kind = symbol.TypeKind switch
        {
            TypeKind.Struct => ObjectKind.Struct,
            TypeKind.Interface => ObjectKind.Interface,
            _ => ObjectKind.Class,
        };
        return new ObjectTypeModel(
            entry.CelName,
            TypeNames.ClrName(symbol, false),
            kind,
            symbol.IsSealed,
            symbol.IsAbstract,
            className,
            entry.Fields,
            entry.Excluded);
    }

    private static EnumModel EnumModelOf(EnumEntry entry, string fieldName)
    {
        var constants = new List<EnumConstantModel>();
        foreach (ISymbol member in entry.Symbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, HasConstantValue: true } field && ValueOf(field.ConstantValue) is { } value)
            {
                constants.Add(new EnumConstantModel(field.Name, value));
            }
        }

        string underlying = entry.Symbol.EnumUnderlyingType is { } type ? TypeNames.ClrName(type, false) : "int";
        return new EnumModel(entry.CelName, TypeNames.ClrName(entry.Symbol, false), fieldName, underlying, EquatableArray.From(constants));
    }

    // The value of a constant as long; a constant of an enumeration over ulong that is above long.MaxValue has none and is left out.
    private static long? ValueOf(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is ulong unsigned)
        {
            return unsigned <= long.MaxValue ? (long)unsigned : null;
        }

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }
}
