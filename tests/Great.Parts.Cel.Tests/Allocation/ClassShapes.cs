using System.Globalization;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// The environments and programs that the memory calibration of the cache of the simple entry fills for shapes with classes. A shape
// has 16 variables: eight integers, a double, a string and a boolean, four classes of 8 members each and a list of a class of 3 members.
// The environment is built as the registration of a call site builds it: one source of types with the descriptions that the shape
// reaches, then the variables; the descriptions are made once and shared by all shapes, as the descriptions of an assembly are. The key
// of an environment is the sorted names and types of its variables, which is at least as large as the key of an anonymous type, so the
// calibration does not understate the cache. The programs are sums of the integers and of a member of each class, up to about 90 code
// units, 16 on each shape, the same as the programs of the shapes without classes.
internal static class ClassShapes
{
    private const int Integers = 8;
    private const int Classes = 4;

    private static readonly CelObjectType[] ClassTypes =
    [
        Describe<CalibrationFirst>("calibration.First"),
        Describe<CalibrationSecond>("calibration.Second"),
        Describe<CalibrationThird>("calibration.Third"),
        Describe<CalibrationFourth>("calibration.Fourth"),
    ];

    private static readonly CelObjectType LineType = new CelObjectTypeBuilder<CalibrationLine>("calibration.Line")
        .AddField("sku", CelType.String, static line => CelValue.FromString(line.Sku))
        .AddField("qty", CelType.Int, static line => CelValue.FromInt64(line.Qty))
        .AddField("price", CelType.Double, static line => CelValue.FromDouble(line.Price))
        .Build();

    private static readonly CelTypeProvider Source = CelTypeProvider.Create("calibration", ClassTypes[0], ClassTypes[1], ClassTypes[2], ClassTypes[3], LineType);

    internal static void Fill(SimpleEntryCache cache, int shapes, int programsPerShape)
    {
        for (int shape = 0; shape < shapes; shape++)
        {
            ShapeSlot[] slots = Slots(shape);
            SimpleEntryCache.EnvironmentEntry environment = cache.AddEnvironment(new ShapeKey(new ShapeProbe(slots, slots.Length)), Build(slots));
            for (int program = 0; program < programsPerShape; program++)
            {
                CelProgram<long> compiled = cache.GetOrCompile<long>(environment, Text(shape, program)).GetProgramOrThrow();
                if (shape == 0 && program == 0)
                {
                    Check(compiled);
                }
            }
        }
    }

    private static CelObjectType Describe<T>(string name)
        where T : CalibrationRecord =>
        new CelObjectTypeBuilder<T>(name)
            .AddField("id", CelType.Int, static item => CelValue.FromInt64(item.Id))
            .AddField("rank", CelType.Int, static item => CelValue.FromInt64(item.Rank))
            .AddField("count", CelType.Int, static item => CelValue.FromInt64(item.Count))
            .AddField("total", CelType.Double, static item => CelValue.FromDouble(item.Total))
            .AddField("price", CelType.Double, static item => CelValue.FromDouble(item.Price))
            .AddField("name", CelType.String, static item => CelValue.FromString(item.Name))
            .AddField("code", CelType.String, static item => CelValue.FromString(item.Code))
            .AddField("active", CelType.Bool, static item => CelValue.FromBoolean(item.Active))
            .Build();

    private static string IntegerName(int shape, int index) =>
        "n" + shape.ToString(CultureInfo.InvariantCulture) + "_" + index.ToString(CultureInfo.InvariantCulture);

    private static string ClassName(int shape, int index) =>
        "c" + shape.ToString(CultureInfo.InvariantCulture) + "_" + index.ToString(CultureInfo.InvariantCulture);

    // The variables of a shape: name and CEL type, in the order of the declaration.
    private static ShapeSlot[] Slots(int shape)
    {
        string suffix = shape.ToString(CultureInfo.InvariantCulture);
        var slots = new ShapeSlot[Integers + 3 + Classes + 1];
        int next = 0;
        for (int index = 0; index < Integers; index++)
        {
            slots[next++] = new ShapeSlot { Name = IntegerName(shape, index), Type = CelType.Int };
        }

        slots[next++] = new ShapeSlot { Name = "d" + suffix, Type = CelType.Double };
        slots[next++] = new ShapeSlot { Name = "s" + suffix, Type = CelType.String };
        slots[next++] = new ShapeSlot { Name = "f" + suffix, Type = CelType.Bool };
        for (int index = 0; index < Classes; index++)
        {
            slots[next++] = new ShapeSlot { Name = ClassName(shape, index), Type = ClassTypes[index].Type };
        }

        slots[next] = new ShapeSlot { Name = "l" + suffix, Type = CelType.ListOf(LineType.Type) };
        return slots;
    }

    private static CelEnvironment Build(ShapeSlot[] slots)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder().AddTypes(Source);
        foreach (ShapeSlot slot in slots)
        {
            builder.AddVariable(slot.Name, slot.Type);
        }

        return builder.Build().GetEnvironmentOrThrow();
    }

    // A sum of the integers and of the identifiers of the classes of the shape, starting at a different term for each program.
    private static string Text(int shape, int program)
    {
        int terms = Integers + Classes;
        var text = new StringBuilder();
        for (int term = 0; text.Length < 80; term++)
        {
            if (term > 0)
            {
                text.Append(" + ");
            }

            int index = (program + term) % terms;
            text.Append(index < Integers ? IntegerName(shape, index) : ClassName(shape, index - Integers) + ".id");
        }

        return text.ToString();
    }

    // The first program of the first shape is evaluated once, so that the environment is known to be built right.
    private static void Check(CelProgram<long> program)
    {
        var bindings = new CelBindings();
        for (int index = 0; index < Integers; index++)
        {
            bindings.Add(IntegerName(0, index), (long)index + 1);
        }

        CalibrationRecord[] records = [new CalibrationFirst(), new CalibrationSecond(), new CalibrationThird(), new CalibrationFourth()];
        for (int index = 0; index < Classes; index++)
        {
            bindings.Add(ClassName(0, index), CelValue.FromObject(ClassTypes[index], records[index]));
        }

        var line = new CalibrationLine { Sku = "a", Qty = 1, Price = 1.0 };
        bindings.Add("l0", CelValue.FromList(CelList.CreateView<CalibrationLine>([line], static element => CelValue.FromObject(LineType, element))));

        Assert.True(program.Evaluate(bindings, null, CancellationToken.None).GetValueOrThrow() > 0);
    }
}

// A class of the application with 8 members; the four classes of a shape derive from it and differ in the name only.
internal abstract class CalibrationRecord
{
    public long Id { get; init; } = 7;

    public long Rank { get; init; } = 1;

    public long Count { get; init; } = 3;

    public double Total { get; init; } = 120.5;

    public double Price { get; init; } = 9.5;

    public string Name { get; init; } = "record";

    public string Code { get; init; } = "rec-1";

    public bool Active { get; init; } = true;
}

internal sealed class CalibrationFirst : CalibrationRecord;

internal sealed class CalibrationSecond : CalibrationRecord;

internal sealed class CalibrationThird : CalibrationRecord;

internal sealed class CalibrationFourth : CalibrationRecord;

// The class of the elements of the list of a shape: 3 members.
internal sealed class CalibrationLine
{
    public string Sku { get; init; } = "";

    public long Qty { get; init; }

    public double Price { get; init; }
}
