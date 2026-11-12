using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of protobuf messages (the cost page, the rows of application data for a protobuf field, for the unpacking of an
// Any and for the literal of a message, the class of creation of a message, the equality of two messages, and the macros of
// the set protos), checked as the rows of ApplicationDataCostTests are: "row | expression | cost", the total cost of
// the expression is exact, so the evaluation succeeds with that cost as the limit and fails with CostLimitExceeded at one unit less.
// The total is the row plus the nodes around it: a read of a variable 1, a literal 0. A field is read for 1, as a selection,
// also a field that is not set and a message field; has() costs 1; a literal of n fields that write string and bytes values
// of L units and bytes in all creates the message for 1 + n + the L over 10 rounded up; the unpacking of an Any whose type URL
// has U UTF-16 units and whose content has B bytes costs 1 + the (U + B) over 10 rounded up, in addition to the read of the
// field that holds it; the equality of two messages costs 1 + the (Sl + Sr) over 10 rounded up, Sl and Sr the sizes of the
// messages in bytes of the wire format, and nothing else, whatever the fields are; proto.getExt and proto.hasExt are a
// selection and a has().
//
// Variables: x has every field set (ProtobufExample.Full), e none; n and n2 are empty NestedTestAllTypes, n3 and n4 have child
// set to an empty one (2 bytes each), n5 and n6 have a child of a child of an empty one (6 bytes each); a10 and a11 hold in
// single_any an Any of a message TestAllTypes whose content has 10 and 11 bytes (a single_string of 8 and of 9 characters)
// and whose type URL, type.googleapis.com/cel.expr.conformance.proto3.TestAllTypes, has 60 units, so the unpacking is
// 1 + 7 and 1 + 8 units; p2x is the message of ProtobufExample.WithExtensions.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-2")]
public sealed class ProtobufCostTests
{
    private const string Rows = """
        field | x.single_int32 | 2
        field that is not set | e.single_int32 | 2
        message field that is not set, then its field | e.standalone_message.bb | 3
        field of a field | x.standalone_message.bb | 3
        has() of a field | has(x.single_int32) | 2
        has() of a message field | has(x.standalone_message) | 2
        repeated field, no copy | x.repeated_int32 | 2
        map field, no copy | x.map_string_int64 | 2
        empty literal, 1 + 0 | TestAllTypes{} | 1
        literal of one field, 1 + 1 | TestAllTypes{single_int32: 5} | 2
        literal of two fields, a string of 1, 1 + 2 + 1 | TestAllTypes{single_int32: 5, single_string: 'a'} | 4
        literal of a string of 11, 1 + 1 + 2 | TestAllTypes{single_string: '01234567890'} | 4
        literal inside a literal, 2 + 2 | TestAllTypes{standalone_message: TestAllTypes.NestedMessage{bb: 1}} | 4
        literal and the read of its field | TestAllTypes{single_int32: 5}.single_int32 | 3
        Any of 10 bytes, read 2 and unpacking 1 + 7 | a10.single_any | 10
        Any of 11 bytes, read 2 and unpacking 1 + 8 | a11.single_any | 11
        equality of two empty messages, 2 + 1 + 0 | n == n2 | 3
        equality of two messages of 2 bytes, 2 + 1 + 1 | n3 == n4 | 4
        equality of messages of 0 and 2 bytes, 2 + 1 + 1 | n == n3 | 4
        equality of two messages of 6 bytes, 2 + 1 + 2 | n5 == n6 | 5
        proto.getExt, a selection | proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext) | 2
        proto.hasExt, a has() | proto.hasExt(p2x, cel.expr.conformance.proto2.int32_ext) | 2
        """;

    private readonly CelEnvironment environment;

    private readonly CelBindings bindings;

    public ProtobufCostTests()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(ProtobufExample.TypesAll)
            .UseContainer(ProtobufExample.Container3)
            .AddExtension(CelExtensions.Protos(0))
            .AddVariable<Proto2.TestAllTypes>("p2x");
        foreach (string name in new[] { "x", "e", "a10", "a11" })
        {
            builder.AddVariable<Proto3.TestAllTypes>(name);
        }

        foreach (string name in new[] { "n", "n2", "n3", "n4", "n5", "n6" })
        {
            builder.AddVariable<Proto3.NestedTestAllTypes>(name);
        }

        environment = builder.Build().GetEnvironmentOrThrow();
        bindings = new CelBindings()
            .Add("x", ProtobufExample.Full())
            .Add("e", new Proto3.TestAllTypes())
            .Add("a10", new Proto3.TestAllTypes { SingleAny = Any.Pack(new Proto3.TestAllTypes { SingleString = "01234567" }) })
            .Add("a11", new Proto3.TestAllTypes { SingleAny = Any.Pack(new Proto3.TestAllTypes { SingleString = "012345678" }) })
            .Add("n", new Proto3.NestedTestAllTypes())
            .Add("n2", new Proto3.NestedTestAllTypes())
            .Add("n3", new Proto3.NestedTestAllTypes { Child = new Proto3.NestedTestAllTypes() })
            .Add("n4", new Proto3.NestedTestAllTypes { Child = new Proto3.NestedTestAllTypes() })
            .Add("n5", DeepChild())
            .Add("n6", DeepChild())
            .Add("p2x", ProtobufExample.WithExtensions());
    }

    private static Proto3.NestedTestAllTypes DeepChild() => new()
    {
        Child = new Proto3.NestedTestAllTypes { Child = new Proto3.NestedTestAllTypes { Child = new Proto3.NestedTestAllTypes() } },
    };

    [Fact]
    public void EveryRow_CostsWhatTheCostPageSays()
    {
        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            string? failure = Check(cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    private string? Check(string expression, ulong cost)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelProgram<CelValue> program = compiled.GetProgramOrThrow();
        CelEvalResult<CelValue> atCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None);
        if (!atCost.IsSuccess)
        {
            return "limit " + cost.ToString(CultureInfo.InvariantCulture) + ": " + atCost.Errors[0].Code + " at " + atCost.Errors[0].Location.Offset.ToString(CultureInfo.InvariantCulture);
        }

        CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        return belowCost.Errors.Length == 1 && belowCost.Errors[0].Code == CelErrorCode.CostLimitExceeded
            ? null
            : "limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough";
    }
}
