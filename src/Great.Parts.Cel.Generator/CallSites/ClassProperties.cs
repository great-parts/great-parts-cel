using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// What reading the type of a property as a class of the application gave.
internal enum ClassOutcome : byte
{
    // The type is none of the classes: another rule gives the reason.
    NotClass,

    // A class, a structure or an interface of the application, or the Nullable<> of a structure: Add<T> binds it.
    Single,

    // One of the six forms of a collection of classes or structures: the created code binds it with a view.
    Collection,

    // The type cannot be described: Reason says why.
    Refused,

    // A diagnostic of the type context was reported for the type: no second one is given for the property.
    Reported,
}

internal readonly record struct ClassReading(ClassOutcome Outcome, string Reason = "", Shape? Collection = null);

// The classes of the application that the properties of one anonymous object reach. A type is read by the code of the type context
// (the classifier, the members, the checks of the names) with the default options of a context: the naming policy CamelCase, the
// attributes of System.Text.Json honoured, the public instance members. The diagnostics of the context are reported at the property
// through which the type is reached, when the member has no place in the source of the compilation.
internal sealed class ClassProperties
{
    // What the created files are, in the messages of GPCEL004 and GPCEL009: there is no type context here.
    internal const string Subject = "CelExpression.Evaluate";

    private readonly CancellationToken cancellationToken;
    private readonly DiagnosticSink sink = new();
    private readonly TypeRegistry registry = new();
    private readonly TypeClassifier classifier;
    private readonly MemberReader members;

    internal ClassProperties(IAssemblySymbol application, CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
        classifier = new TypeClassifier(registry);
        members = new MemberReader(classifier, new ContextOptions(0, false), sink, application);
    }

    // The diagnostics found while reading the types, in the order that does not depend on the order of the sources.
    internal EquatableArray<DiagnosticModel> Diagnostics => sink.ToArray();

    internal ClassReading Read(ITypeSymbol type, LocationInfo place)
    {
        if (!IsClassForm(type))
        {
            return Describable(type);
        }

        registry.ReachedBy = place;
        Classification result = classifier.Classify(type, false);
        switch (result.Kind)
        {
            case ClassificationKind.Ok:
                break;
            case ClassificationKind.Refused:
                return Refuse(result.Text);
            case ClassificationKind.InvalidName:
                sink.Report(DiagnosticIds.InvalidName, place, result.Text, "qualified type name");
                return new ClassReading(ClassOutcome.Reported);
            default:
                return ReportLimit(place);
        }

        Shape shape = result.Shape!;
        if (LeafOf(shape) is not ObjectShape leaf)
        {
            return new ClassReading(ClassOutcome.NotClass);
        }

        if (leaf.Kind == ObjectKind.Message)
        {
            return Refuse(Reasons.RootMessage);
        }

        ReadMembers();
        if (registry.LimitExceeded)
        {
            return ReportLimit(place);
        }

        return shape is CollectionShape ? new ClassReading(ClassOutcome.Collection, Collection: shape) : new ClassReading(ClassOutcome.Single);
    }

    // The members of the types that the property reached, and of the types those reach, with an explicit stack so that a deep graph
    // cannot overflow the stack of the compiler.
    private void ReadMembers()
    {
        while (registry.Pending.Count > 0 && !registry.LimitExceeded)
        {
            cancellationToken.ThrowIfCancellationRequested();
            members.Read(registry.Pending.Pop());
        }
    }

    // The closure of the types that were read: the models of the object types and of the enumerations with the checks of the names done,
    // and the CEL names of both. A call without a class has none. The names of the created classes are not kept: the closures of all
    // calls are put together and named for the assembly.
    internal (ClassClosure Closure, EquatableArray<string> Objects, EquatableArray<string> Enums) Close()
    {
        if (registry.Objects.Count == 0 && registry.Enums.Count == 0)
        {
            return (ClassClosure.Empty, default, default);
        }

        (List<ObjectTypeModel> objects, List<EnumModel> enums) = ModelBuilder.Close(Subject, registry, sink);
        var objectModels = new List<ObjectTypeModel>();
        var objectNames = new List<string>();
        foreach (ObjectTypeModel type in objects)
        {
            objectModels.Add(type with { ClassName = string.Empty });
            objectNames.Add(type.CelName);
        }

        var enumModels = new List<EnumModel>();
        var enumNames = new List<string>();
        foreach (EnumModel enumeration in enums)
        {
            enumModels.Add(enumeration with { FieldName = string.Empty });
            enumNames.Add(enumeration.CelName);
        }

        return (new ClassClosure(EquatableArray.From(objectModels), EquatableArray.From(enumModels)), EquatableArray.From(objectNames), EquatableArray.From(enumNames));
    }

    // A class, a structure or an interface of the application, the Nullable<> of a structure, and the six forms of a collection whose
    // element or value names such a type.
    private static bool IsClassForm(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return PropertyRules.IsApplicationObject(nullable.TypeArguments[0]);
        }

        if (PropertyRules.IsApplicationObject(type))
        {
            return true;
        }

        ITypeSymbol? element = PropertyRules.ElementOfCollectionForm(type);
        return element is not null && PropertyRules.MentionsApplicationObject(element);
    }

    // A type that is no class of the application: object and the collections keep their reasons of the list; another type that a type
    // context refuses (a delegate, Uri, a type of System with no row) is a type that a context cannot describe, and the others (float,
    // decimal, an enumeration) are outside the list.
    private static ClassReading Describable(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Object || PropertyRules.IsCollectionType(type))
        {
            return new ClassReading(ClassOutcome.NotClass);
        }

        Classification result = new TypeClassifier(new TypeRegistry()).Classify(type, false);
        return result.Kind == ClassificationKind.Refused ? Refuse(result.Text) : new ClassReading(ClassOutcome.NotClass);
    }

    private static ClassReading Refuse(string reason) =>
        new(ClassOutcome.Refused, PropertyRules.ReasonCannotDescribe + ": " + reason);

    private ClassReading ReportLimit(LocationInfo place)
    {
        sink.Report(DiagnosticIds.ContextTooLarge, place, Subject, TypeRegistry.Limit.ToString(CultureInfo.InvariantCulture));
        return new ClassReading(ClassOutcome.Reported);
    }

    // The type at the end of a chain of collections and Nullable<>: the element of a list, the value of a dictionary.
    private static Shape LeafOf(Shape shape)
    {
        while (true)
        {
            switch (shape)
            {
                case CollectionShape collection:
                    shape = collection.Element;
                    break;
                case NullableShape nullable:
                    shape = nullable.Underlying;
                    break;
                default:
                    return shape;
            }
        }
    }
}
