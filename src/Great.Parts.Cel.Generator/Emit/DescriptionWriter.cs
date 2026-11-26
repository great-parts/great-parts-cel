using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// One nested sealed class that derives from CelObjectType<T> for an object type of the .NET type T: the constructor with the fields,
// the reading of a field by a switch on its number from the instance passed by reference (so a structure is not boxed), the presence,
// and the reasons of the excluded members.
internal static class DescriptionWriter
{
    private const string Values = "global::Great.Parts.Cel.Values.";

    // The text that stands for the member in the expressions of ValueReader and PresenceReader: it is bound to `value.Member`, or
    // to a local that has read the member once when an expression names it more than once.
    private const string Self = "\u0001";

    // What one field needs: the expression that reads it and the one that tells whether it is present, and how many times each
    // names the member.
    private sealed record FieldCode(FieldModel Field, string Read, int ReadUses, string Presence, int PresenceUses);

    // The modifiers of an override of a member of CelObjectType, which is protected internal: the override of another assembly is
    // protected, and the override of an assembly that the library gives the access to its internal members must say protected internal.
    internal static string Override(bool internalAccess) => internalAccess ? "protected internal override " : "protected override ";

    // access is the accessibility of the class: private for the nested class of a type context, internal for a description that
    // the files of the registrations of the call sites name. internalAccess tells whether the assembly sees the internal members
    // of the library. placed adds the static method Placed, which converts an instance in a place of this type by the place rule
    // of a context: the file of descriptions of the call sites converts the elements of collections with it.
    internal static void Write(CodeWriter w, ObjectTypeModel type, ObjectIndex objects, string version, string access, bool internalAccess, bool placed = false)
    {
        var converters = new List<HoistedConverter>();
        var codes = new List<FieldCode>();
        foreach (FieldModel field in type.Fields.Items)
        {
            var reader = new ValueReader(field.Number, objects);
            var value = new Operand(Self);
            string read = reader.Read(field.Shape, value);
            var presence = new Operand(Self);
            string presenceText = PresenceReader.Read(field.Shape, presence);
            codes.Add(new FieldCode(field, read, value.Uses, presenceText, presence.Uses));
            converters.AddRange(reader.Converters);
        }

        w.Line("[global::System.CodeDom.Compiler.GeneratedCode(\"Great.Parts.Cel.Generator\", " + Literals.Quote(version) + ")]");
        w.Open(access + " sealed class " + type.ClassName + " : " + Values + "CelObjectType<" + type.ClrName + ">");
        w.Line("internal static readonly " + type.ClassName + " Description = new();");
        foreach (HoistedConverter converter in converters)
        {
            w.EmptyLine();
            w.Line("private readonly " + converter.DelegateType + " " + converter.Name + ";");
        }

        if (placed)
        {
            w.EmptyLine();
            WritePlaced(w, type);
        }

        w.EmptyLine();
        WriteConstructor(w, type, converters);
        w.EmptyLine();
        WriteGetField(w, type, codes, internalAccess);
        w.EmptyLine();
        WriteHasField(w, type, codes, internalAccess);
        if (type.Excluded.Length > 0)
        {
            w.EmptyLine();
            WriteExcluded(w, type, internalAccess);
        }

        if (type.Literal is { } literal)
        {
            w.EmptyLine();
            LiteralWriter.Write(w, type, literal, internalAccess);
        }

        CostWriter.Write(w, type, internalAccess);
        w.Close();
    }

    // The place rule of a context for an instance that is not null, in a static method of the description, where the protected
    // FromInstance is accessible: a structure is the bare boxed instance, an instance of a sealed class and an instance of exactly
    // the declared type of a class that is not sealed are the bare instance, and an instance of a derived class is paired with this
    // description, so that it is read by the members of the declared type. Written only for a structure and for a class that is not
    // abstract: an instance of an abstract class or of an interface is never of the declared type and is always paired.
    private static void WritePlaced(CodeWriter w, ObjectTypeModel type)
    {
        string head = "internal static " + Values + "CelValue Placed(" + type.ClrName + " e) =>";
        if (type.Kind == ObjectKind.Struct)
        {
            w.Line(head + " FromInstance((object)e);");
            return;
        }

        if (type.IsSealed)
        {
            w.Line(head + " FromInstance(e);");
            return;
        }

        w.Line(head);
        w.Indent();
        w.Line("e.GetType() == typeof(" + type.ClrName + ") ? FromInstance(e) : " + Values + "CelValue.FromObject(Description, e);");
        w.Outdent();
    }

    private static void WriteConstructor(CodeWriter w, ObjectTypeModel type, List<HoistedConverter> converters)
    {
        w.Line("private " + type.ClassName + "()");
        w.Indent();
        w.Line(": base(");
        w.Indent();
        w.Line(Literals.Quote(type.CelName) + ",");
        if (type.Fields.Length == 0)
        {
            w.Line("[])");
        }
        else
        {
            w.Line("[");
            w.Indent();
            foreach (FieldModel field in type.Fields.Items)
            {
                w.Line("new(" + Literals.Int(field.Number) + ", " + Literals.Quote(field.Name) + ", " + CelTypeExpressions.Of(field.CelType) + "),");
            }

            w.Outdent();
            w.Line("])");
        }

        w.Outdent();
        w.Outdent();
        w.Line("{");
        w.Indent();
        foreach (HoistedConverter converter in converters)
        {
            w.Line(converter.Name + " = " + converter.Lambda + ";");
        }

        w.Close();
    }

    private static void WriteGetField(CodeWriter w, ObjectTypeModel type, List<FieldCode> codes, bool internalAccess)
    {
        w.Open(Override(internalAccess) + Values + "CelValue GetField(in " + type.ClrName + " instance, int number)");
        w.Open("switch (number)");
        foreach (FieldCode code in codes)
        {
            WriteCase(w, code.Field, code.Read, code.ReadUses, "instance");
        }

        w.Line("default: throw new global::System.ArgumentOutOfRangeException(nameof(number));");
        w.Close();
        w.Close();
    }

    private static void WriteHasField(CodeWriter w, ObjectTypeModel type, List<FieldCode> codes, bool internalAccess)
    {
        bool needsValue = false;
        foreach (FieldCode code in codes)
        {
            needsValue |= code.Presence != PresenceReader.Always;
        }

        w.Open(Override(internalAccess) + "bool HasField(object instance, int number)");
        if (needsValue)
        {
            w.Line("var value = (" + type.ClrName + ")instance;");
        }

        w.Open("switch (number)");

        // The members that are always present share one return.
        var labels = new List<string>();
        foreach (FieldCode code in codes)
        {
            if (code.Presence == PresenceReader.Always)
            {
                labels.Add("case " + Literals.Int(code.Field.Number) + ":");
                continue;
            }

            FlushLabels(w, labels);
            WriteCase(w, code.Field, code.Presence, code.PresenceUses, "value");
        }

        FlushLabels(w, labels);
        w.Line("default: throw new global::System.ArgumentOutOfRangeException(nameof(number));");
        w.Close();
        w.Close();
    }

    private static void FlushLabels(CodeWriter w, List<string> labels)
    {
        if (labels.Count == 0)
        {
            return;
        }

        w.Line(string.Join(" ", labels) + " return true;");
        labels.Clear();
    }

    // A case that returns the expression: on one line when the expression names the member once, otherwise after a local that reads
    // the member once. The member is read from the instance, which is the parameter of the method or the local that the cast made.
    private static void WriteCase(CodeWriter w, FieldModel field, string expression, int uses, string instance)
    {
        string label = "case " + Literals.Int(field.Number) + ":";
        string member = instance + "." + field.Access;
        if (uses <= 1)
        {
            w.Line(label + " return " + expression.Replace(Self, member) + ";");
            return;
        }

        w.Open(label);
        w.Line("var v = " + member + ";");
        w.Line("return " + expression.Replace(Self, "v") + ";");
        w.Close();
    }

    private static void WriteExcluded(CodeWriter w, ObjectTypeModel type, bool internalAccess)
    {
        w.Open(Override(internalAccess) + "string? GetExcludedFieldReason(string fieldName)");
        w.Open("switch (fieldName)");
        foreach (ExcludedMemberModel member in type.Excluded.Items)
        {
            w.Line("case " + Literals.Quote(member.Name) + ": return " + Literals.Quote(member.Reason) + ";");
        }

        w.Line("default: return null;");
        w.Close();
        w.Close();
    }
}
