using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// A converter of an element or of a value of a collection that the constructor of a description stores in a field, because its
// body calls a protected helper of CelObjectType that names the field in an error (or the field of another such converter).
// Lambda is the whole lambda, `e0 => ...`.
internal sealed record HoistedConverter(string Name, string DelegateType, string Lambda);

// The reading of the members of one field: the C# expression that turns the value of a member into a CelValue, by the shape of the
// member ("Reading a member" and "Collections" of the page of the created code). A converter that needs nothing of the instance is
// a static lambda, which the compiler creates once; one that needs it is a field of the description, created by its constructor.
internal sealed class ValueReader
{
    private const string Values = "global::Great.Parts.Cel.Values.";
    private const string CelValue = Values + "CelValue";
    private const string NullValue = CelValue + ".Null";
    private const string UnexpectedNullCode = "global::Great.Parts.Cel.Diagnostics.CelErrorCode.UnexpectedNull";
    private const string Generic = "global::System.Collections.Generic.";
    private const string DoubleType = "global::System.Double";

    private readonly string number;
    private readonly ObjectIndex objects;
    private readonly List<HoistedConverter> converters = [];
    private int depth;
    private bool usesInstance;

    internal ValueReader(int number, ObjectIndex objects)
    {
        this.number = Literals.Int(number);
        this.objects = objects;
    }

    internal List<HoistedConverter> Converters => converters;

    // The expression for a member of the shape, written with the text of the operand.
    internal string Read(Shape shape, Operand value) => Row(shape, value, false);

    private string Row(Shape shape, Operand v, bool element) => shape switch
    {
        ScalarShape scalar => Scalar(scalar, v),
        EnumShape => Helper("FromEnum", v),
        NullableShape nullable => NullableRow(nullable, v, element),
        ObjectShape objectShape => ObjectRow(objectShape, v, element),
        CollectionShape collection => CollectionRow(collection, v),
        _ => NullValue,
    };

    private static string Factory(string method, string argument) => CelValue + "." + method + "(" + argument + ")";

    // A protected helper of CelObjectType that takes the number of the field: it needs the instance.
    private string Helper(string method, Operand v)
    {
        usesInstance = true;
        return method + "(" + number + ", " + v.Use() + ")";
    }

    private string Unexpected()
    {
        usesInstance = true;
        return "FieldError(" + number + ", " + UnexpectedNullCode + ", null)";
    }

    private string NullOrUnexpected(Nullability nullability) => nullability == Nullability.NotNull ? Unexpected() : NullValue;

    private string Scalar(ScalarShape shape, Operand v) => shape.Kind switch
    {
        ScalarKind.Int => Factory("FromInt64", v.Use()),
        ScalarKind.UInt => Factory("FromUInt64", v.Use()),
        ScalarKind.Double => Factory("FromDouble", shape.ClrName == DoubleType ? v.Use() : "(double)" + v.Use()),
        ScalarKind.Bool => Factory("FromBoolean", v.Use()),
        ScalarKind.String => Checked(v, shape.Nullability, Factory("FromString", v.Use())),
        ScalarKind.Guid => Factory("FromString", v.Use() + ".ToString(\"D\")"),
        ScalarKind.Bytes => Checked(v, shape.Nullability, Factory("FromBytes", v.Use())),
        ScalarKind.BytesMemory => Factory("FromBytes", v.Use()),
        ScalarKind.Timestamp => Factory("FromTimestamp", v.Use()),
        ScalarKind.Duration => Factory("FromDuration", v.Use()),
        ScalarKind.DateTimeOffset => Factory("FromTimestamp", Values + "CelTimestamp.FromDateTimeOffset(" + v.Use() + ")"),
        ScalarKind.DateTime => Helper("FromDateTime", v),
        ScalarKind.TimeSpan => Helper("FromTimeSpan", v),
        ScalarKind.Decimal128 => Helper("FromDecimal", v),
        ScalarKind.Decimal => "FromDecimal(" + v.Use() + ")",
        ScalarKind.CelValue => v.Use(),
        ScalarKind.Object => Checked(v, shape.Nullability, Helper("FromDynamic", v)),
        ScalarKind.JsonElement => Helper("FromDynamic", v),
        ScalarKind.CelList => Checked(v, Nullability.NotNull, Factory("FromList", v.Use())),
        ScalarKind.CelMap => Checked(v, Nullability.NotNull, Factory("FromMap", v.Use())),
        _ => NullValue,
    };

    // A reference type: null is the null value where the position takes null and UnexpectedNull where it does not.
    private string Checked(Operand v, Nullability nullability, string read) =>
        v.Use() + " is null ? " + NullOrUnexpected(nullability) + " : " + read;

    // Nullable<T>: the row of T over the value, and what is read when there is none: the null value where the CEL type of T takes
    // null (a wrapper, dyn, an object type), UnexpectedNull where it does not (time, decimal, a collection).
    private string NullableRow(NullableShape shape, Operand v, bool element)
    {
        string present = v.Use() + ".HasValue";
        string read = Row(shape.Underlying, v.Part(".Value"), element);
        return present + " ? " + read + " : " + Absent(shape.Underlying);
    }

    private string Absent(Shape underlying) => underlying switch
    {
        CollectionShape => Unexpected(),
        ScalarShape { Kind: ScalarKind.Timestamp or ScalarKind.DateTimeOffset or ScalarKind.DateTime or ScalarKind.Duration or ScalarKind.TimeSpan or ScalarKind.Decimal128 or ScalarKind.Decimal } => Unexpected(),
        _ => NullValue,
    };

    private string ObjectRow(ObjectShape shape, Operand v, bool element)
    {
        if (shape.Kind == ObjectKind.Struct)
        {
            return "FromInstance((object)" + v.Use() + ")";
        }

        string read = element ? Placed(shape, v) : "FromInstance(" + v.Use() + ")";
        return Checked(v, shape.Nullability, read);
    }

    // The place rule inside a collection: an element has the declared element type of the member. A sealed class (a message
    // included) is the declared type itself; a class that is not sealed is paired with the description of the declared type when
    // the instance is of a derived type; an abstract class and an interface always are.
    private string Placed(ObjectShape shape, Operand v)
    {
        ObjectTypeModel? type = shape.Kind == ObjectKind.Message ? null : objects.Find(shape.CelName);
        if (type is null || (shape.IsSealed && shape.Kind == ObjectKind.Class))
        {
            return "FromInstance(" + v.Use() + ")";
        }

        string described = Factory("FromObject", type.ClassName + ".Description, " + v.Use());
        if (shape.IsAbstract || shape.Kind == ObjectKind.Interface)
        {
            return described;
        }

        return v.Use() + ".GetType() == typeof(" + type.ClrName + ") ? FromInstance(" + v.Use() + ") : " + described;
    }

    private string CollectionRow(CollectionShape shape, Operand v)
    {
        string element = shape.Element.ClrName;
        string converter = Converter(shape.Element);
        switch (shape.Family)
        {
            case CollectionFamily.ImmutableArray:
                // default is an array that was never made: it is checked first and read as an absent collection. AsArray gives the
                // array that the structure holds, with no copy and no box.
                return v.Use() + ".IsDefault ? " + Unexpected() + " : "
                    + List("CreateView", element, "global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsArray(" + v.Use() + ")!", converter);
            case CollectionFamily.StructList:
                return List("CreateView", element, "(" + Generic + "IReadOnlyList<" + element + ">)" + v.Use(), converter);
            default:
                // A structure has no null: it is read as the class of its family over its box, and the box is made once.
                string read = shape.IsValueType ? StructCollection(shape, v, element, converter) : ReferenceCollection(shape, v, element, converter);
                return shape.IsValueType ? read : Checked(v, Nullability.NotNull, read);
        }
    }

    // A collection that is a reference type. A wrapper is made only for an instance that is not read-only already.
    private static string ReferenceCollection(CollectionShape shape, Operand v, string element, string converter)
    {
        string key = shape.Key?.ClrName ?? string.Empty;
        switch (shape.Family)
        {
            case CollectionFamily.ListOnly:
                return List(
                    "CreateView",
                    element,
                    "(object)" + v.Use() + " as " + Generic + "IReadOnlyList<" + element + "> ?? new global::System.Collections.ObjectModel.ReadOnlyCollection<" + element + ">(" + v.Use() + ")",
                    converter);
            case CollectionFamily.Set:
                return List(
                    "CreateSorted",
                    element,
                    "(object)" + v.Use() + " as " + Generic + "IReadOnlyCollection<" + element + "> ?? new " + Generic + "List<" + element + ">(" + v.Use() + ")",
                    converter);
            case CollectionFamily.Collection:
                return List("CreateCopy", element, v.Use(), converter);
            case CollectionFamily.Dictionary:
                return Map(key, element, v.Use(), converter);
            case CollectionFamily.DictionaryOnly:
                return Map(
                    key,
                    element,
                    "(object)" + v.Use() + " as " + Generic + "IReadOnlyDictionary<" + key + ", " + element + "> ?? new global::System.Collections.ObjectModel.ReadOnlyDictionary<" + key + ", " + element + ">(" + v.Use() + ")",
                    converter);
            default:
                return List("CreateView", element, v.Use(), converter);
        }
    }

    // A structure that is a collection of a family other than IReadOnlyList<T> (that one is the family StructList): its type is the
    // declared one, so it has no instance of a derived type to test for the read-only interface. It names the member once, which
    // the conversion to the interface boxes once.
    private static string StructCollection(CollectionShape shape, Operand v, string element, string converter)
    {
        string key = shape.Key?.ClrName ?? string.Empty;
        switch (shape.Family)
        {
            case CollectionFamily.ListOnly:
                return List("CreateView", element, "new global::System.Collections.ObjectModel.ReadOnlyCollection<" + element + ">(" + v.Use() + ")", converter);
            case CollectionFamily.Set:
                string source = shape.ReadOnlyCollection ? v.Use() : "new " + Generic + "List<" + element + ">(" + v.Use() + ")";
                return List("CreateSorted", element, source, converter);
            case CollectionFamily.Collection:
                return List("CreateCopy", element, v.Use(), converter);
            case CollectionFamily.Dictionary:
                return Map(key, element, v.Use(), converter);
            default:
                return Map(
                    key,
                    element,
                    "new global::System.Collections.ObjectModel.ReadOnlyDictionary<" + key + ", " + element + ">(" + v.Use() + ")",
                    converter);
        }
    }

    private static string List(string method, string element, string source, string converter) =>
        Factory("FromList", Values + "CelList." + method + "<" + element + ">(" + source + ", " + converter + ")");

    // keepSourceOrder is false in every case: the library decides the order by the type of the source.
    private static string Map(string key, string value, string source, string converter) =>
        Factory("FromMap", Values + "CelMap.CreateView<" + key + ", " + value + ">(" + source + ", " + converter + ", false)");

    // The converter of an element or of a value. A converter that needs nothing of the instance is a static lambda; one that needs
    // it (a helper of CelObjectType, or a converter of an inner collection that is a field) becomes a field of the description.
    private string Converter(Shape element)
    {
        string parameter = "e" + Literals.Int(depth);
        bool outer = usesInstance;
        usesInstance = false;
        depth++;
        string body = Row(element, new Operand(parameter), true);
        depth--;
        bool needsInstance = usesInstance;
        usesInstance = outer;
        if (!needsInstance)
        {
            return "static " + parameter + " => " + body;
        }

        string name = converters.Count == 0 ? "_convert" + number : "_convert" + number + "_" + Literals.Int(converters.Count + 1);
        converters.Add(new HoistedConverter(name, "global::System.Func<" + element.ClrName + ", " + CelValue + ">", parameter + " => " + body));
        usesInstance = true;
        return name;
    }
}
