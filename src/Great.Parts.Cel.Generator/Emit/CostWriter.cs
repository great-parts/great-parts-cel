using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// FieldReadCost of a description class ("Charges of a read" of the page of the created code): the units beyond the unit of the
// selection that the read of a member costs, for the three kinds of member whose read does work that grows with the data or with
// the size of a structure. The method is written only for a type that has such a member; the others keep the default, 0.
internal static class CostWriter
{
    private const string Generic = "global::System.Collections.Generic.";
    private const int BoxBytesPerUnit = 64;

    // What the read of a member costs: Box is the units of the box of a structure (0 when the read boxes nothing), Collection is
    // the member that is a set or a collection without an index (charged by its size), Wrapper is the Nullable<T> around them.
    private sealed record Charge(bool Wrapper, long Box, CollectionShape? Collection);

    private static Charge? Of(Shape shape)
    {
        bool wrapper = shape is NullableShape;
        Shape core = shape is NullableShape nullable ? nullable.Underlying : shape;
        long box = core switch
        {
            ObjectShape { Kind: ObjectKind.Struct } structure => Units(structure.Size),
            CollectionShape { IsValueType: true, Family: not CollectionFamily.ImmutableArray } structure => Units(structure.Size),
            ScalarShape { Kind: ScalarKind.JsonElement } json => Units(json.Size),
            _ => 0,
        };
        CollectionShape? collection = core is CollectionShape { Family: CollectionFamily.Set or CollectionFamily.Collection } counted ? counted : null;
        return box == 0 && collection is null ? null : new Charge(wrapper, box, collection);
    }

    // 1 + ⌈S/64⌉ for a structure of S bytes.
    private static long Units(int size) => 1 + (((long)size + BoxBytesPerUnit - 1) / BoxBytesPerUnit);

    // The method, and the helper of the sets when a member is a set; nothing when no member is charged.
    internal static void Write(CodeWriter w, ObjectTypeModel type)
    {
        var charged = new List<(FieldModel Field, Charge Charge)>();
        bool sets = false;
        bool needsValue = false;
        foreach (FieldModel field in type.Fields.Items)
        {
            Charge? charge = Of(field.Shape);
            if (charge is null)
            {
                continue;
            }

            charged.Add((field, charge));
            sets |= charge.Collection is { Family: CollectionFamily.Set };
            needsValue |= charge.Wrapper || charge.Collection is not null;
        }

        if (charged.Count == 0)
        {
            return;
        }

        w.EmptyLine();
        w.Open("protected override long FieldReadCost(object instance, int number)");
        if (needsValue)
        {
            w.Line("var value = (" + type.ClrName + ")instance;");
        }

        w.Open("switch (number)");
        foreach ((FieldModel field, Charge charge) in charged)
        {
            WriteCase(w, field, charge);
        }

        w.Line("default: return 0;");
        w.Close();
        w.Close();
        if (sets)
        {
            w.EmptyLine();
            WriteSortCost(w);
        }
    }

    private static void WriteCase(CodeWriter w, FieldModel field, Charge charge)
    {
        string label = "case " + Literals.Int(field.Number) + ":";
        string member = "value." + field.Access;
        if (charge.Collection is null)
        {
            // The box of a structure: a constant; a Nullable<T> that has no value reads as null and costs 0.
            string box = Literals.Long(charge.Box);
            w.Line(label + " return " + (charge.Wrapper ? member + ".HasValue ? " + box + " : 0;" : box + ";"));
            return;
        }

        w.Open(label);
        w.Line("var v = " + member + ";");
        string source = "v";
        if (charge.Wrapper)
        {
            ReturnZeroIf(w, "!v.HasValue");
            source = "v.Value";
        }
        else if (!charge.Collection.IsValueType)
        {
            ReturnZeroIf(w, "v is null");
        }

        WriteCollection(w, charge, charge.Collection, source);
        w.Close();
    }

    // A member with no value reads as null and costs nothing beyond the unit of the selection.
    private static void ReturnZeroIf(CodeWriter w, string condition)
    {
        w.Open("if (" + condition + ")");
        w.Line("return 0;");
        w.Close();
    }

    // The size n is the Count of the instance that the member holds now; a set adds the sort, a collection without an index the copy.
    private static void WriteCollection(CodeWriter w, Charge charge, CollectionShape collection, string source)
    {
        string element = collection.Element.ClrName;
        string countType = collection.Family == CollectionFamily.Set && !collection.ReadOnlyCollection ? "ICollection" : "IReadOnlyCollection";
        w.Line("long n = ((" + Generic + countType + "<" + element + ">)" + source + ").Count;");
        if (collection.Family == CollectionFamily.Collection)
        {
            w.Line("return " + Literals.Long(1 + charge.Box) + " + n;");
            return;
        }

        string unit = Unit(w, collection, source);
        w.Line("return SortCost(n, " + unit + ", " + Literals.Long(charge.Box) + ");");
    }

    // c of the set: 1 for numbers, bool, enumerations, times and decimal; 1 + ⌈2 · Lmax/10⌉ for string, bytes and Guid, where
    // Lmax is found by one pass over the elements (36 for a Guid), and a null element counts 0.
    private static string Unit(CodeWriter w, CollectionShape collection, string source)
    {
        Shape element = collection.Element is NullableShape nullable ? nullable.Underlying : collection.Element;
        bool wrapped = collection.Element is NullableShape;
        switch (element)
        {
            case ScalarShape { Kind: ScalarKind.Guid }:
                return Literals.Long(1 + ((2 * 36) + 9) / 10);
            case ScalarShape { Kind: ScalarKind.String or ScalarKind.Bytes or ScalarKind.BytesMemory } text:
                WriteLongest(w, collection, source, text.Kind, wrapped);
                return "1L + ((2L * longest + 9L) / 10L)";
            default:
                return "1L";
        }
    }

    private static void WriteLongest(CodeWriter w, CollectionShape collection, string source, ScalarKind kind, bool wrapped)
    {
        string length = wrapped ? "e.Value.Length" : "e.Length";
        string present = wrapped ? "e.HasValue" : kind == ScalarKind.BytesMemory ? string.Empty : "e is not null";
        string condition = present.Length == 0 ? length + " > longest" : present + " && " + length + " > longest";
        w.Line("int longest = 0;");
        w.Open("foreach (var e in (" + Generic + "IEnumerable<" + collection.Element.ClrName + ">)" + source + ")");
        w.Open("if (" + condition + ")");
        w.Line("longest = " + length + ";");
        w.Close();
        w.Close();
    }

    // The charge of a set: 1 + c · n · ⌈log₂(n + 1)⌉ and the box, a sum that does not fit a long is long.MaxValue. ⌈log₂(n + 1)⌉ is
    // the number of bits of n; the factors n · bits fit a long, and the product with c is checked by its high part.
    private static void WriteSortCost(CodeWriter w)
    {
        w.Open("private static long SortCost(long count, long unit, long box)");
        w.Line("long bits = 64 - global::System.Numerics.BitOperations.LeadingZeroCount((ulong)count);");
        w.Line("long high = global::System.Math.BigMul(unit, count * bits, out long low);");
        w.Line("return high != 0 || low < 0 || low > long.MaxValue - 1 - box ? long.MaxValue : low + 1 + box;");
        w.Close();
    }
}
