using System.Collections.Generic;
using System.Globalization;

namespace Great.Parts.Cel.Generator;

// The members of a description that make object literals possible ("Object literals" of the page of the created code):
// IsConstructible, CanSetField, IsRequiredField and CreateInstance, which converts each value of the literal into a local of the type
// of its member, then calls the constructor and sets the members. The conversions are plain C# over the public members of CelValue
// (no reflection); an init member that is not required is set through an accessor method of its init accessor, which Native AOT
// supports.
internal static class LiteralWriter
{
    private const string Values = "global::Great.Parts.Cel.Values.";
    private const string CelValue = Values + "CelValue";
    private const string ErrorCode = "global::Great.Parts.Cel.Diagnostics.CelErrorCode.";
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
    private const string Generic = "global::System.Collections.Generic.";

    internal static void Write(CodeWriter w, ObjectTypeModel type, LiteralModel literal)
    {
        w.Line("protected override bool IsConstructible => true;");
        WriteFlags(w, "CanSetField", literal, static field => !field.Settable, false);
        WriteFlags(w, "IsRequiredField", literal, static field => field.Required, true);
        w.EmptyLine();
        WriteCreateInstance(w, type, literal);
        w.EmptyLine();
        w.Open("private " + CelValue + " LiteralMismatch(int number, " + CelValue + " value, string expected)");
        w.Line("return FieldError(number, " + ErrorCode + "InvalidArgument, \"expected a value of the type '\" + expected + \"' but found a value of the kind \" + value.Kind);");
        w.Close();
        WriteAccessors(w, literal);
    }

    // CanSetField returns true except for the numbers listed; IsRequiredField returns false except for them. Nothing is written when
    // the list is empty, since the default of the base class is the answer.
    private static void WriteFlags(CodeWriter w, string name, LiteralModel literal, System.Func<LiteralFieldModel, bool> listed, bool answer)
    {
        var labels = new List<string>();
        foreach (LiteralFieldModel field in literal.Fields.Items)
        {
            if (listed(field))
            {
                labels.Add("case " + Literals.Int(field.Number) + ":");
            }
        }

        if (labels.Count == 0)
        {
            return;
        }

        w.EmptyLine();
        w.Open("protected override bool " + name + "(int number)");
        w.Open("switch (number)");
        w.Line(string.Join(" ", labels) + " return " + (answer ? "true" : "false") + ";");
        w.Line("default: return " + (answer ? "false" : "true") + ";");
        w.Close();
        w.Close();
    }

    private static bool NeedsFlag(LiteralFieldModel field) =>
        field.Settable && (field.IsParameter ? field.DefaultText.Length > 0 : field.Setter != SetterKind.Initializer);

    private static void WriteCreateInstance(CodeWriter w, ObjectTypeModel type, LiteralModel literal)
    {
        w.Open("protected override " + CelValue + " CreateInstance(global::System.ReadOnlySpan<int> fieldNumbers, global::System.ReadOnlySpan<" + CelValue + "> fieldValues)");
        foreach (LiteralFieldModel field in literal.Fields.Items)
        {
            if (!field.Settable)
            {
                continue;
            }

            w.Line(field.Shape!.ClrName + " v" + Literals.Int(field.Number) + " = default!;");
            if (NeedsFlag(field))
            {
                w.Line("bool s" + Literals.Int(field.Number) + " = false;");
            }
        }

        w.Open("for (int i = 0; i < fieldNumbers.Length; i++)");
        w.Line(CelValue + " value = fieldValues[i];");
        w.Open("switch (fieldNumbers[i])");
        foreach (LiteralFieldModel field in literal.Fields.Items)
        {
            if (!field.Settable)
            {
                continue;
            }

            string number = Literals.Int(field.Number);
            w.Line("case " + number + ":");
            w.Indent();
            w.Line("{");
            w.Indent();
            Convert(w, field.Shape!, "value", "v" + number, number, 0);
            if (NeedsFlag(field))
            {
                w.Line("s" + number + " = true;");
            }

            w.Line("break;");
            w.Outdent();
            w.Line("}");
            w.Outdent();
        }

        w.Line("default: return FieldError(fieldNumbers[i], " + ErrorCode + "InvalidArgument, \"the field cannot be set by an object literal\");");
        w.Close();
        w.Close();
        WriteConstruction(w, type, literal);
        w.Close();
    }

    private static void WriteConstruction(CodeWriter w, ObjectTypeModel type, LiteralModel literal)
    {
        var arguments = new string[literal.ParameterCount];
        var initializers = new List<string>();
        foreach (LiteralFieldModel field in literal.Fields.Items)
        {
            string number = Literals.Int(field.Number);
            if (field.IsParameter)
            {
                arguments[field.ParameterIndex] = !field.Settable ? field.DefaultText
                    : field.DefaultText.Length > 0 ? "s" + number + " ? v" + number + " : " + field.DefaultText
                    : "v" + number;
            }
            else if (field.Settable && field.Setter == SetterKind.Initializer)
            {
                initializers.Add(field.Access + " = v" + number + ",");
            }
        }

        string call = "var instance = new " + type.ClrName + "(" + string.Join(", ", arguments) + ")";
        if (initializers.Count == 0)
        {
            w.Line(call + ";");
        }
        else
        {
            w.Line(call);
            w.Line("{");
            w.Indent();
            foreach (string initializer in initializers)
            {
                w.Line(initializer);
            }

            w.Outdent();
            w.Line("};");
        }

        foreach (LiteralFieldModel field in literal.Fields.Items)
        {
            string number = Literals.Int(field.Number);
            if (field.IsParameter || !field.Settable)
            {
                continue;
            }

            if (field.Setter == SetterKind.Assign)
            {
                w.Line("if (s" + number + ") instance." + field.Access + " = v" + number + ";");
            }
            else if (field.Setter == SetterKind.Accessor)
            {
                w.Line("if (s" + number + ") Set_" + field.AccessorName + "(" + (literal.IsStruct ? "ref " : string.Empty) + "instance, v" + number + ");");
            }
        }

        w.Line("return FromInstance(" + (literal.IsStruct ? "(object)instance" : "instance") + ");");
    }

    // An accessor method for the init accessor of each member that is set after construction: C# lets only an object initializer call
    // an init accessor, and the accessor method is how created code does it without reflection.
    private static void WriteAccessors(CodeWriter w, LiteralModel literal)
    {
        foreach (LiteralFieldModel field in literal.Fields.Items)
        {
            if (!field.Settable || field.IsParameter || field.Setter != SetterKind.Accessor)
            {
                continue;
            }

            w.EmptyLine();
            w.Line("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = " + Literals.Quote("set_" + field.AccessorName) + ")]");
            w.Line("private static extern void Set_" + field.AccessorName + "(" + (literal.IsStruct ? "ref " : string.Empty) + field.DeclaringType + " target, " + field.Shape!.ClrName + " value);");
        }
    }

    private static string Bad(string number, string source, string expected) =>
        "LiteralMismatch(" + number + ", " + source + ", " + Literals.Quote(expected) + ")";

    private static string Range(string number, string value, string celType) =>
        "FieldError(" + number + ", " + ErrorCode + "ConversionRange, " + value + ".ToString(" + Invariant + ") + \" is out of the range of " + celType + "\")";

    private static string Error(string number, string code, string text) =>
        "FieldError(" + number + ", " + ErrorCode + code + ", " + Literals.Quote(text) + ")";

    // The statements that convert the CEL value `source` into the variable `target`, which the caller declared, or return an error
    // value; a block of its own, so the temporaries of one conversion never meet those of another. Depth names the temporaries of a
    // collection and of its elements apart.
    private static void Convert(CodeWriter w, CreateShape shape, string source, string target, string number, int depth)
    {
        w.Line("{");
        w.Indent();
        if (shape.IsReference)
        {
            w.Open("if (" + source + ".Kind == " + Values + "CelValueKind.Null)");
            w.Line(NullAssignment(shape, target, number));
            w.Close();
            w.Open("else");
            Body(w, shape, source, target, number, depth);
            w.Close();
        }
        else
        {
            Body(w, shape, source, target, number, depth);
        }

        w.Close();
    }

    private static string NullAssignment(CreateShape shape, string target, string number) => shape.Nullability switch
    {
        Nullability.NotNull => "return FieldError(" + number + ", " + ErrorCode + "UnexpectedNull, null);",
        Nullability.Nullable => target + " = null;",
        _ => target + " = null!;",
    };

    // The statement that reads the CEL value `source` into the temporary `t` through the accessor `getter`, or returns the mismatch
    // of the kind `kind`.
    private static void Read(CodeWriter w, string source, string getter, string type, string t, string number, string kind) =>
        w.Line("if (!" + source + "." + getter + "(out " + type + " " + t + ")) return " + Bad(number, source, kind) + ";");

    // The read of the temporary and the assignment of `value`, an expression over the temporary, to the variable `target`.
    private static void Take(CodeWriter w, string source, string getter, string type, string t, string number, string kind, string target, string value)
    {
        Read(w, source, getter, type, t, number, kind);
        w.Line(target + " = " + value + ";");
    }

    private static void Body(CodeWriter w, CreateShape shape, string source, string target, string number, int depth)
    {
        string d = depth.ToString(CultureInfo.InvariantCulture);
        string t = "t" + d;
        switch (shape.Kind)
        {
            case CreateKind.Int:
                Read(w, source, "TryGetInt64", "long", t, number, "int");
                if (shape.Keyword != "long")
                {
                    w.Line("if (" + t + " < " + shape.Keyword + ".MinValue || " + t + " > " + shape.Keyword + ".MaxValue) return " + Range(number, t, "int") + ";");
                    w.Line(target + " = (" + shape.Keyword + ")" + t + ";");
                }
                else
                {
                    w.Line(target + " = " + t + ";");
                }

                break;
            case CreateKind.UInt:
                Read(w, source, "TryGetUInt64", "ulong", t, number, "uint");
                if (shape.Keyword != "ulong")
                {
                    w.Line("if (" + t + " > " + shape.Keyword + ".MaxValue) return " + Range(number, t, "uint") + ";");
                    w.Line(target + " = (" + shape.Keyword + ")" + t + ";");
                }
                else
                {
                    w.Line(target + " = " + t + ";");
                }

                break;
            case CreateKind.Double:
                Take(w, source, "TryGetDouble", "double", t, number, "double", target, shape.Keyword == "double" ? t : "(" + shape.Keyword + ")" + t);
                break;
            case CreateKind.Bool:
                Take(w, source, "TryGetBoolean", "bool", t, number, "bool", target, t);
                break;
            case CreateKind.String:
                Take(w, source, "TryGetString", "string?", t, number, "string", target, t + "!");
                break;
            case CreateKind.Char:
                Read(w, source, "TryGetString", "string?", t, number, "string");
                w.Line("if (" + t + "!.Length != 1) return " + Error(number, "InvalidArgument", "expected a string of one UTF-16 code unit") + ";");
                w.Line(target + " = " + t + "[0];");
                break;
            case CreateKind.Guid:
                Read(w, source, "TryGetString", "string?", t, number, "string");
                w.Line("if (!global::System.Guid.TryParseExact(" + t + ", \"D\", out global::System.Guid g" + d + ")) return " + Error(number, "InvalidArgument", "expected a UUID of the form 8-4-4-4-12 hexadecimal digits") + ";");
                w.Line(target + " = g" + d + ";");
                break;
            case CreateKind.Int128:
                Take(w, source, "TryGetInt64", "long", t, number, "int", target, "(global::System.Int128)" + t);
                break;
            case CreateKind.UInt128:
                Take(w, source, "TryGetUInt64", "ulong", t, number, "uint", target, "(global::System.UInt128)" + t);
                break;
            case CreateKind.BigInteger:
                Take(w, source, "TryGetInt64", "long", t, number, "int", target, "new global::System.Numerics.BigInteger(" + t + ")");
                break;
            case CreateKind.Date:
                Take(w, source, "TryGetDate", "global::System.DateOnly", t, number, "date", target, t);
                break;
            case CreateKind.TimeOfDay:
                Take(w, source, "TryGetTimeOfDay", "global::System.TimeOnly", t, number, "time_of_day", target, t);
                break;
            case CreateKind.Bytes:
                Take(w, source, "TryGetBytes", "global::System.ReadOnlyMemory<byte>", t, number, "bytes", target, t + ".ToArray()");
                break;
            case CreateKind.BytesMemory:
                Take(w, source, "TryGetBytes", "global::System.ReadOnlyMemory<byte>", t, number, "bytes", target, t);
                break;
            case CreateKind.Timestamp:
                Take(w, source, "TryGetTimestamp", Values + "CelTimestamp", t, number, "timestamp", target, t);
                break;
            case CreateKind.DateTimeOffset:
            case CreateKind.DateTime:
                Read(w, source, "TryGetTimestamp", Values + "CelTimestamp", t, number, "timestamp");
                w.Line("if (!" + t + ".TryGetDateTimeOffset(out global::System.DateTimeOffset z" + d + ")) return " + Error(number, "ConversionRange", "the timestamp is out of the range of DateTimeOffset") + ";");
                w.Line(target + " = z" + d + (shape.Kind == CreateKind.DateTime ? ".UtcDateTime;" : ";"));
                break;
            case CreateKind.Duration:
                Take(w, source, "TryGetDuration", Values + "CelDuration", t, number, "duration", target, t);
                break;
            case CreateKind.TimeSpan:
                Read(w, source, "TryGetDuration", Values + "CelDuration", t, number, "duration");
                w.Line("if (!" + t + ".TryGetTimeSpan(out global::System.TimeSpan z" + d + ")) return " + Error(number, "ConversionRange", "the duration is out of the range of TimeSpan") + ";");
                w.Line(target + " = z" + d + ";");
                break;
            case CreateKind.Decimal:
                w.Line("if (" + source + ".Kind != " + Values + "CelValueKind.Decimal) return " + Bad(number, source, "decimal") + ";");
                w.Line("if (!" + source + ".TryGetDecimal(out decimal " + t + ")) return " + Error(number, "ConversionRange", "the value is out of the range of System.Decimal") + ";");
                w.Line(target + " = " + t + ";");
                break;
            case CreateKind.CelValue:
                w.Line(target + " = " + source + ";");
                break;
            case CreateKind.CelList:
                Take(w, source, "TryGetList", Values + "CelList?", t, number, "list", target, t + "!");
                break;
            case CreateKind.CelMap:
                Take(w, source, "TryGetMap", Values + "CelMap?", t, number, "map", target, t + "!");
                break;
            case CreateKind.Object:
                w.Line("if (!" + source + ".TryGetObject(out object? " + t + ") || " + t + " is not " + shape.Keyword + " o" + d + ") return " + Bad(number, source, shape.Keyword.Replace("global::", string.Empty)) + ";");
                w.Line(target + " = o" + d + ";");
                break;
            case CreateKind.Nullable:
                NullableBody(w, shape, source, target, number, depth);
                break;
            case CreateKind.Dictionary:
                DictionaryBody(w, shape, source, target, number, depth);
                break;
            default:
                SequenceBody(w, shape, source, target, number, depth);
                break;
        }
    }

    // Nullable<T>: the null value is the Nullable without a value; any other value is converted as T.
    private static void NullableBody(CodeWriter w, CreateShape shape, string source, string target, string number, int depth)
    {
        string d = depth.ToString(CultureInfo.InvariantCulture);
        CreateShape inner = shape.Element!;
        w.Open("if (" + source + ".Kind == " + Values + "CelValueKind.Null)");
        w.Line(target + " = null;");
        w.Close();
        w.Open("else");
        w.Line(inner.ClrName + " n" + d + " = default;");
        Convert(w, inner, source, "n" + d, number, depth + 1);
        w.Line(target + " = n" + d + ";");
        w.Close();
    }

    // An array, a list or an ImmutableArray<T>: a List<T> of the converted elements, then the target type from it.
    private static void SequenceBody(CodeWriter w, CreateShape shape, string source, string target, string number, int depth)
    {
        string d = depth.ToString(CultureInfo.InvariantCulture);
        CreateShape element = shape.Element!;
        w.Line("if (!" + source + ".TryGetList(out " + Values + "CelList? list" + d + ")) return " + Bad(number, source, "list") + ";");
        w.Line("var items" + d + " = new " + Generic + "List<" + element.ClrName + ">(list" + d + "!.Count);");
        w.Open("foreach (" + CelValue + " item" + d + " in list" + d + "!)");
        w.Line(element.ClrName + " element" + d + " = default!;");
        Convert(w, element, "item" + d, "element" + d, number, depth + 1);
        w.Line("items" + d + ".Add(element" + d + ");");
        w.Close();
        string made = shape.Kind switch
        {
            CreateKind.Array => "items" + d + ".ToArray()",
            CreateKind.ImmutableArray => "global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(items" + d + ".ToArray())",
            _ => "items" + d,
        };
        w.Line(target + " = " + made + ";");
    }

    // A Dictionary<K, V>: the keys and the values converted one pair at a time.
    private static void DictionaryBody(CodeWriter w, CreateShape shape, string source, string target, string number, int depth)
    {
        string d = depth.ToString(CultureInfo.InvariantCulture);
        CreateShape key = shape.Key!;
        CreateShape value = shape.Element!;
        w.Line("if (!" + source + ".TryGetMap(out " + Values + "CelMap? map" + d + ")) return " + Bad(number, source, "map") + ";");
        w.Line("var pairs" + d + " = new " + Generic + "Dictionary<" + key.ClrName + ", " + value.ClrName + ">(map" + d + "!.Count);");
        w.Open("foreach (" + Generic + "KeyValuePair<" + CelValue + ", " + CelValue + "> pair" + d + " in map" + d + "!)");
        w.Line(key.ClrName + " key" + d + " = default!;");
        Convert(w, key, "pair" + d + ".Key", "key" + d, number, depth + 1);
        w.Line(value.ClrName + " entry" + d + " = default!;");
        Convert(w, value, "pair" + d + ".Value", "entry" + d, number, depth + 1);
        w.Line("pairs" + d + "[key" + d + "] = entry" + d + ";");
        w.Close();
        w.Line(target + " = pairs" + d + ";");
    }
}
