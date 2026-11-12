using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// One nested sealed class that derives from CelObjectType for an object type: the constructor with the fields, the reading of a
// field by a switch on its number, the presence, and the reasons of the excluded members.
internal static class DescriptionWriter
{
    private const string Values = "global::Great.Parts.Cel.Values.";

    // The text that stands for the member in the expressions of ValueReader and PresenceReader: it is bound to `value.Member`, or
    // to a local that has read the member once when an expression names it more than once.
    private const string Self = "\u0001";

    // What one field needs: the expression that reads it and the one that tells whether it is present, and how many times each
    // names the member.
    private sealed record FieldCode(FieldModel Field, string Read, int ReadUses, string Presence, int PresenceUses);

    internal static void Write(CodeWriter w, ObjectTypeModel type, ObjectIndex objects, string version)
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
        w.Open("private sealed class " + type.ClassName + " : " + Values + "CelObjectType");
        w.Line("internal static readonly " + type.ClassName + " Description = new();");
        foreach (HoistedConverter converter in converters)
        {
            w.EmptyLine();
            w.Line("private readonly " + converter.DelegateType + " " + converter.Name + ";");
        }

        w.EmptyLine();
        WriteConstructor(w, type, converters);
        w.EmptyLine();
        WriteGetField(w, type, codes);
        w.EmptyLine();
        WriteHasField(w, type, codes);
        if (type.Excluded.Length > 0)
        {
            w.EmptyLine();
            WriteExcluded(w, type);
        }

        CostWriter.Write(w, type);
        w.Close();
    }

    private static void WriteConstructor(CodeWriter w, ObjectTypeModel type, List<HoistedConverter> converters)
    {
        w.Line("private " + type.ClassName + "()");
        w.Indent();
        w.Line(": base(");
        w.Indent();
        w.Line(Literals.Quote(type.CelName) + ",");
        w.Line("typeof(" + type.ClrName + "),");
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

    private static void WriteGetField(CodeWriter w, ObjectTypeModel type, List<FieldCode> codes)
    {
        w.Open("protected override " + Values + "CelValue GetField(object instance, int number)");
        if (codes.Count > 0)
        {
            w.Line("var value = (" + type.ClrName + ")instance;");
        }

        w.Open("switch (number)");
        foreach (FieldCode code in codes)
        {
            WriteCase(w, code.Field, code.Read, code.ReadUses);
        }

        w.Line("default: throw new global::System.ArgumentOutOfRangeException(nameof(number));");
        w.Close();
        w.Close();
    }

    private static void WriteHasField(CodeWriter w, ObjectTypeModel type, List<FieldCode> codes)
    {
        bool needsValue = false;
        foreach (FieldCode code in codes)
        {
            needsValue |= code.Presence != PresenceReader.Always;
        }

        w.Open("protected override bool HasField(object instance, int number)");
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
            WriteCase(w, code.Field, code.Presence, code.PresenceUses);
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
    // the member once.
    private static void WriteCase(CodeWriter w, FieldModel field, string expression, int uses)
    {
        string label = "case " + Literals.Int(field.Number) + ":";
        string member = "value." + field.Access;
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

    private static void WriteExcluded(CodeWriter w, ObjectTypeModel type)
    {
        w.Open("protected override string? GetExcludedFieldReason(string fieldName)");
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
