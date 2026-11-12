using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Generator;

// The members of one object type: which are hidden, which are fields, with what names and types, and which are left out with a
// reason. The diagnostics about members (GPCEL002, GPCEL003, GPCEL005 of a member and GPCEL007) are reported here.
internal sealed class MemberReader
{
    private readonly TypeClassifier classifier;
    private readonly ContextOptions options;
    private readonly DiagnosticSink sink;
    private readonly IAssemblySymbol application;

    internal MemberReader(TypeClassifier classifier, ContextOptions options, DiagnosticSink sink, IAssemblySymbol application)
    {
        this.classifier = classifier;
        this.options = options;
        this.sink = sink;
        this.application = application;
    }

    // A member that was read: a field when Shape is set, a member that is left out when Reason is.
    private sealed record Item(string CsName, string FieldName, string Access, LocationInfo Place, Shape? Shape, string? Reason);

    internal void Read(ObjectEntry entry)
    {
        var readable = new List<Item>();
        var excluded = new List<Item>();
        foreach (ISymbol member in MemberCatalog.Of(entry.Symbol))
        {
            Item? item = Examine(entry, member);
            if (item is null)
            {
                continue;
            }

            (item.Shape is null ? excluded : readable).Add(item);
        }

        Finish(entry, readable, excluded);
    }

    private static string AccessOf(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    // The place of a member is its declaration when the member is in the source of the compilation of the context. A member of a
    // type of another assembly has no place there, also when an IDE gives that assembly as a compilation and not as metadata: its
    // trees are not the trees of this compilation, so the place is where the type was reached.
    private LocationInfo PlaceOf(ObjectEntry entry, ISymbol member)
    {
        if (SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, application))
        {
            foreach (Location location in member.Locations)
            {
                if (location.IsInSource)
                {
                    return LocationInfo.From(location);
                }
            }
        }

        return entry.ReachedBy;
    }

    private Item? Examine(ObjectEntry entry, ISymbol member)
    {
        ITypeSymbol? type = MemberCatalog.TypeOf(member);
        if (type is null || IsHidden(entry, member))
        {
            return null;
        }

        LocationInfo place = PlaceOf(entry, member);
        string fieldName = NameOf(member);
        if (!TypeNames.IsWord(fieldName))
        {
            sink.Report(DiagnosticIds.InvalidName, place, fieldName, "identifier");
            return null;
        }

        string access = AccessOf(member.Name);
        string? mark = Attributes.Mark(member);
        if (mark is not null)
        {
            return LeftOut(entry, member, fieldName, access, place, Reasons.MemberMarked(mark));
        }

        return Classified(entry, member, classifier.Classify(type, false), fieldName, access, place);
    }

    private Item? Classified(ObjectEntry entry, ISymbol member, Classification result, string fieldName, string access, LocationInfo place)
    {
        switch (result.Kind)
        {
            case ClassificationKind.Ok:
                return new Item(member.Name, fieldName, access, place, result.Shape, null);
            case ClassificationKind.Refused:
                return LeftOut(entry, member, fieldName, access, place, result.Text);
            case ClassificationKind.DecimalUnavailable:
                sink.Report(DiagnosticIds.DecimalMember, place, member.Name, entry.Display);
                return new Item(member.Name, fieldName, access, place, null, Reasons.DecimalUnavailable);
            case ClassificationKind.InvalidName:
                sink.Report(DiagnosticIds.InvalidName, place, result.Text, "qualified type name");
                return new Item(member.Name, fieldName, access, place, null, Reasons.InvalidTypeName(result.Text));
            default:
                return null;
        }
    }

    // A member that is left out with a reason, and the warning GPCEL002 for it.
    private Item LeftOut(ObjectEntry entry, ISymbol member, string fieldName, string access, LocationInfo place, string reason)
    {
        sink.Report(DiagnosticIds.ExcludedMember, place, member.Name, entry.Display, reason);
        return new Item(member.Name, fieldName, access, place, null, reason);
    }

    // A hidden member is not a field and has no warning: [CelIgnore], a name of IgnoreMembers of the root of its type, and
    // [JsonIgnore] with the condition Always unless the context ignores the attributes of System.Text.Json.
    private bool IsHidden(ObjectEntry entry, ISymbol member)
    {
        if (Attributes.Has(member, WellKnown.CelIgnoreAttribute))
        {
            return true;
        }

        if (entry.Root is { } root && Array.IndexOf(root.IgnoreMembers, member.Name) >= 0)
        {
            return true;
        }

        return !options.IgnoreJson && JsonIgnoresAlways(member);
    }

    // [JsonIgnore] with no argument or with the condition Always, the constant 1 of JsonIgnoreCondition.
    private static bool JsonIgnoresAlways(ISymbol member)
    {
        AttributeData? attribute = Attributes.Find(member, WellKnown.JsonIgnoreAttribute);
        if (attribute is null)
        {
            return false;
        }

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == "Condition")
            {
                return argument.Value.Value is 1;
            }
        }

        return true;
    }

    // The name of the field: [CelName], then [JsonPropertyName] unless the context ignores it, then the naming policy.
    private string NameOf(ISymbol member)
    {
        if (StringArgument(Attributes.Find(member, WellKnown.CelNameAttribute)) is { } celName)
        {
            return celName;
        }

        if (!options.IgnoreJson && StringArgument(Attributes.Find(member, WellKnown.JsonPropertyNameAttribute)) is { } jsonName)
        {
            return jsonName;
        }

        return NamingPolicies.Apply(options.NamingPolicy, member.Name);
    }

    private static string? StringArgument(AttributeData? attribute) =>
        attribute is { ConstructorArguments.Length: 1 } ? attribute.ConstructorArguments[0].Value as string : null;

    private void Finish(ObjectEntry entry, List<Item> readable, List<Item> excluded)
    {
        readable.Sort(static (left, right) => string.CompareOrdinal(left.CsName, right.CsName));
        var fields = new List<Item>();
        var byName = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (Item item in readable)
        {
            if (byName.TryGetValue(item.FieldName, out Item? first))
            {
                sink.Report(DiagnosticIds.DuplicateField, item.Place, first.CsName, item.CsName, entry.Display, item.FieldName);
                continue;
            }

            byName[item.FieldName] = item;
            fields.Add(item);
        }

        fields.Sort(static (left, right) => string.CompareOrdinal(left.FieldName, right.FieldName));
        var models = new List<FieldModel>();
        for (int number = 0; number < fields.Count; number++)
        {
            Item field = fields[number];
            models.Add(new FieldModel(number, field.FieldName, field.Access, field.Shape!, CelTypes.Of(field.Shape!)));
        }

        entry.Fields = EquatableArray.From(models);
        entry.Excluded = ExcludedModels(excluded, byName);
    }

    // The excluded members by the C# names, ordinal; when two get one field name the first answers; a name that a field has is
    // not asked for a reason.
    private static EquatableArray<ExcludedMemberModel> ExcludedModels(List<Item> excluded, Dictionary<string, Item> fields)
    {
        excluded.Sort(static (left, right) => string.CompareOrdinal(left.CsName, right.CsName));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var models = new List<ExcludedMemberModel>();
        foreach (Item item in excluded)
        {
            if (!fields.ContainsKey(item.FieldName) && seen.Add(item.FieldName))
            {
                models.Add(new ExcludedMemberModel(item.FieldName, item.Reason!));
            }
        }

        models.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return EquatableArray.From(models);
    }
}
