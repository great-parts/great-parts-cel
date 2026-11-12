using System.Threading;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// google.protobuf.Struct, Value and ListValue in a field of a message (the type mapping page, the rows of Struct, Value
// and ListValue): a Struct is a map from string to dyn, a ListValue a list of dyn, and a Value is read by what it holds —
// null, a double (the only number of a Value, so an integer written in it reads as a double), a string, a bool, a Struct
// as a map, or a ListValue as a list; the values inside are read in the same way, at any depth. They are the maps and the lists
// of the language: a missing key of a Struct is, as in any map, EvalNoSuchField (3001) "no such field 'zz' in map" at the "."
// of the selection (cel-go gives an error there too, "no such key: zz"; the page of selection, SelectionTests) and NoSuchKey (3002)
// at the "[" of the index, and the operators, the macros and the comparisons are those of maps and lists. The writing of a literal is in ProtobufLiteralTests. The values are those of cel-go
// v0.32.0 (a probe of the oracle, 2026-11-11). Variables of type TestAllTypes: vnum, vstr, vbool, vnull, vstruct
// and vlist have single_value set to 1.5, 's', true, null, {a: 1, b: {c: [true, null]}} and [1, 'x', [2]]; s has
// single_struct {a: 1, b: 'x'} and list_value [1, 'a'].
[Trait("Requirement", "F-2")]
public sealed class ProtobufDynamicValueTests
{
    private const string Values = """
        vnum.single_value => 1.5
        vstr.single_value => 's'
        vbool.single_value => true
        vnull.single_value => null
        vstruct.single_value => {'a': 1.0, 'b': {'c': [true, null]}}
        vlist.single_value => [1.0, 'x', [2.0]]
        vnum.single_value + 1.0 => 2.5
        vstr.single_value + 'x' => 'sx'
        vstruct.single_value.b.c[0] => true
        vstruct.single_value.b.c[1] => null
        vstruct.single_value.a == 1 => true
        vlist.single_value[2][0] => 2.0
        type(vnum.single_value) == double => true
        type(vstr.single_value) == string => true
        type(vbool.single_value) == bool => true
        type(vnull.single_value) == null_type => true
        type(vstruct.single_value) == map => true
        type(vlist.single_value) == list => true
        s.single_struct.a => 1.0
        s.single_struct['b'] => 'x'
        size(s.single_struct) => 2
        'a' in s.single_struct => true
        'zz' in s.single_struct => false
        has(s.single_struct.a) => true
        has(s.single_struct.zz) => false
        s.single_struct.all(k, k in ['a', 'b']) => true
        s.single_struct == {'a': 1.0, 'b': 'x'} => true
        s.list_value[0] => 1.0
        s.list_value[1] => 'a'
        size(s.list_value) => 2
        s.list_value.exists(v, v == 'a') => true
        s.list_value == [1.0, 'a'] => true
        s.list_value + [3.0] => [1.0, 'a', 3.0]
        """;

    private const string Errors = """
        Evaluate | s.single_struct^.zz | EvalNoSuchField | no such field 'zz' in map
        Evaluate | s.single_struct^['zz'] | NoSuchKey | no such key: "zz"
        """;

    private readonly CelEnvironment environment;

    private readonly CelBindings bindings;

    public ProtobufDynamicValueTests()
    {
        CelEnvironmentBuilder builder = ProtobufExample.Builder3();
        foreach (string name in new[] { "vnum", "vstr", "vbool", "vnull", "vstruct", "vlist", "s" })
        {
            builder.AddVariable<Proto3.TestAllTypes>(name);
        }

        environment = builder.Build().GetEnvironmentOrThrow();
        Value inner = Value.ForStruct(new Struct { Fields = { ["c"] = Value.ForList(Value.ForBool(true), Value.ForNull()) } });
        bindings = new CelBindings()
            .Add("vnum", new Proto3.TestAllTypes { SingleValue = Value.ForNumber(1.5) })
            .Add("vstr", new Proto3.TestAllTypes { SingleValue = Value.ForString("s") })
            .Add("vbool", new Proto3.TestAllTypes { SingleValue = Value.ForBool(true) })
            .Add("vnull", new Proto3.TestAllTypes { SingleValue = Value.ForNull() })
            .Add("vstruct", new Proto3.TestAllTypes { SingleValue = Value.ForStruct(new Struct { Fields = { ["a"] = Value.ForNumber(1), ["b"] = inner } }) })
            .Add("vlist", new Proto3.TestAllTypes { SingleValue = Value.ForList(Value.ForNumber(1), Value.ForString("x"), Value.ForList(Value.ForNumber(2))) })
            .Add(
                "s",
                new Proto3.TestAllTypes
                {
                    SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(1), ["b"] = Value.ForString("x") } },
                    ListValue = new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } },
                });
    }

    [Fact]
    public void ValueStructAndListValue_AreReadByTheirContent()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Values, bindings));
    }

    [Fact]
    public void KeyThatAStructDoesNotHave_IsTheErrorOfAMap()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, Errors, bindings));
    }

    // A Value that holds a number is a double even when the number is whole, and the number does not become an int on
    // the way back: it is compared as a number of CEL.
    [Fact]
    public void WholeNumberOfAValue_IsADouble()
    {
        CelValue value = environment.Compile<CelValue>("vstruct.single_value.a").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal(CelValueKind.Double, value.Kind);
        Assert.True(value.TryGetDouble(out double number) && number == 1.0);
    }
}
