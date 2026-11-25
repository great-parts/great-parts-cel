using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Great.Parts.Cel.Generator;

// Puts together the types that the properties of the anonymous objects of all calls of the assembly reach. A type that two calls reach
// is one description; two types of one CEL name are GPCEL004 at the call of the second; more types than the limit of the closure of a
// type context are GPCEL009 at the call that passes the limit, and then no description is written at all. The calls are taken in the
// order of the file and of the position, never in the order the compiler found them, so the result does not depend on it, and the
// created classes and fields get their names from the sorted CEL names, as the classes of a type context do.
internal static class ClassDescriptions
{
    private const string GlobalPrefix = "global::";

    internal static ClassDescriptionsResult Merge(ImmutableArray<ClosureAtPlace> all)
    {
        ClosureAtPlace[] ordered = [.. all.OrderBy(static item => item.Place.FilePath, StringComparer.Ordinal).ThenBy(static item => item.Place.Span.Start)];
        var objects = new Dictionary<string, ObjectTypeModel>(StringComparer.Ordinal);
        var enums = new Dictionary<string, EnumModel>(StringComparer.Ordinal);
        var diagnostics = new List<DiagnosticModel>();
        bool tooLarge = false;
        foreach (ClosureAtPlace item in ordered)
        {
            foreach (ObjectTypeModel type in item.Closure.Objects.Items)
            {
                if (!objects.TryGetValue(type.CelName, out ObjectTypeModel? owner))
                {
                    objects[type.CelName] = type;
                }
                else if (!owner.Equals(type))
                {
                    diagnostics.Add(Duplicate(owner.ClrName, type.ClrName, type.CelName, item.Place));
                }
            }

            foreach (EnumModel enumeration in item.Closure.Enums.Items)
            {
                if (!enums.TryGetValue(enumeration.CelName, out EnumModel? owner))
                {
                    enums[enumeration.CelName] = enumeration;
                }
                else if (!owner.Equals(enumeration))
                {
                    diagnostics.Add(Duplicate(owner.ClrName, enumeration.ClrName, enumeration.CelName, item.Place));
                }
            }

            if (!tooLarge && objects.Count > TypeRegistry.Limit)
            {
                tooLarge = true;
                diagnostics.Add(new DiagnosticModel(
                    DiagnosticIds.ContextTooLarge,
                    item.Place,
                    new EquatableArray<string>([ClassProperties.Subject, TypeRegistry.Limit.ToString(CultureInfo.InvariantCulture)])));
            }
        }

        if (tooLarge || objects.Count == 0)
        {
            return new ClassDescriptionsResult(null, EquatableArray.From(diagnostics));
        }

        return new ClassDescriptionsResult(Name(objects, enums), EquatableArray.From(diagnostics));
    }

    private static ClassDescriptionsModel Name(Dictionary<string, ObjectTypeModel> objects, Dictionary<string, EnumModel> enums)
    {
        var classNames = new HashSet<string>(StringComparer.Ordinal);
        var named = new List<ObjectTypeModel>();
        foreach (ObjectTypeModel type in objects.Values.OrderBy(static type => type.CelName, StringComparer.Ordinal))
        {
            named.Add(type with { ClassName = ModelBuilder.UniqueName(ModelBuilder.ClassPrefix, type.CelName, classNames) });
        }

        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var enumerations = new List<EnumModel>();
        foreach (EnumModel enumeration in enums.Values.OrderBy(static enumeration => enumeration.CelName, StringComparer.Ordinal))
        {
            enumerations.Add(enumeration with { FieldName = ModelBuilder.UniqueName(ModelBuilder.EnumPrefix, enumeration.CelName, fieldNames) });
        }

        return new ClassDescriptionsModel(EquatableArray.From(named), EquatableArray.From(enumerations));
    }

    private static DiagnosticModel Duplicate(string first, string second, string celName, LocationInfo place) =>
        new(DiagnosticIds.DuplicateType, place, new EquatableArray<string>([Display(first), Display(second), ClassProperties.Subject, celName]));

    private static string Display(string clrName) =>
        clrName.StartsWith(GlobalPrefix, StringComparison.Ordinal) ? clrName.Substring(GlobalPrefix.Length) : clrName;
}
