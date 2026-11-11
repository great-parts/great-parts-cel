using System;
using System.Collections.Generic;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Extensions.Network;
using Great.Parts.Cel.Tests.Library.Extensions.Network;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Network.NetworkEnvironment;

namespace Great.Parts.Cel.Tests.Values;

// The rules of the kind Abstract, with the two types of the set network, the only abstract types of the library: the kind and the type
// of a value, equality by content in an expression and in .NET, the type values that name the types, the comparison with null and with
// other kinds, the refusal as a map key, the conversion to CelValue and to object, and the declaration of a variable by the name of the
// type. Each row of the first table is an expression that is true.
public sealed class AbstractValueTests
{
    private const string Holds = """
        ip('1.2.3.4') == ip('1.2.3.4')
        ip('1.2.3.4') != ip('1.2.3.5')
        ip('::1.2.3.4') != ip('1.2.3.4')
        ip('0.0.0.1') != ip('::1')
        ip('2001:DB8::1') == ip('2001:db8:0:0:0:0:0:1')
        cidr('10.0.0.1/8') == cidr('10.0.0.1/8')
        cidr('10.0.0.1/8') != cidr('10.0.0.0/8')
        cidr('10.0.0.0/8') != cidr('10.0.0.0/9')
        cidr('::1/64') == cidr('0:0:0:0:0:0:0:1/64')
        dyn(ip('1.2.3.4')) != dyn(cidr('1.2.3.4/32'))
        dyn(ip('1.2.3.4')) != dyn('1.2.3.4')
        dyn(ip('1.2.3.4')) != dyn(16909060)
        dyn(cidr('10.0.0.0/8')) != dyn(ip('10.0.0.0'))
        ip('1.2.3.4') != null
        null != ip('1.2.3.4')
        cidr('10.0.0.0/8') != null
        [ip('1.2.3.4'), ip('::1')] == [ip('1.2.3.4'), ip('::1')]
        [ip('1.2.3.4'), ip('::1')] != [ip('::1'), ip('1.2.3.4')]
        ip('::1') in [ip('1.2.3.4'), ip('::1')]
        !(ip('::2') in [ip('1.2.3.4'), ip('::1')])
        size([ip('1.2.3.4'), null]) == 2
        [null, ip('1.2.3.4')][1] == ip('1.2.3.4')
        [ip('1.2.3.4'), ip('::1')].all(a, a.isLoopback() || a.family() == 4)
        [cidr('10.0.0.0/8'), cidr('::/0')].exists(c, c.prefixLength() == 0)
        type(ip('1.2.3.4')) == net.IP
        type(cidr('10.0.0.0/8')) == net.CIDR
        type(ip('1.2.3.4')) != net.CIDR
        type(ip('::1')) == type(ip('1.1.1.1'))
        type(net.IP) == type
        net.IP != net.CIDR
        dyn(net.IP) == type(ip('::'))
        type(dyn(ip('1.2.3.4'))) == net.IP
        """;

    public static TheoryData<string> HoldingCases => HoldingData();

    [Fact]
    public static void KindsAbstract_HaveTheirPublishedNumbers()
    {
        Assert.Equal((19, 16), ((int)CelTypeKind.Abstract, (int)CelValueKind.Abstract));
    }

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    [Theory]
    [InlineData("ip('1.2.3.4')", "net.IP")]
    [InlineData("ip('::1')", "net.IP")]
    [InlineData("cidr('10.0.0.0/8')", "net.CIDR")]
    public static void Value_HasTheKindAbstractAndItsTypeByName(string expression, string name)
    {
        CelValue value = ValueOf(expression);

        CelType type = ValueTypeRules.TypeOf(value);

        Assert.Equal(CelValueKind.Abstract, value.Kind);
        Assert.Equal((CelTypeKind.Abstract, name, name), (type.Kind, type.Name, type.ToString()));
        Assert.Empty(type.Parameters);
        Assert.Equal(name == "net.IP" ? NetworkTypes.Ip : NetworkTypes.Cidr, type);
    }

    // .NET equality: the same type and the same content, without the CEL rules, and a hash that follows it.
    [Fact]
    public static void ValuesOfSeparateEvaluations_AreEqualByContentInDotNet()
    {
        CelValue first = ValueOf("ip('2001:DB8::1')");
        CelValue same = ValueOf("ip('2001:db8:0:0:0:0:0:1')");
        CelValue prefix = ValueOf("cidr('10.0.0.1/8')");
        CelValue samePrefix = ValueOf("cidr('10.0.0.1/8')");

        Assert.Equal(first, same);
        Assert.True(first == same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.Equal(prefix, samePrefix);
        Assert.Equal(prefix.GetHashCode(), samePrefix.GetHashCode());
        Assert.NotEqual(first, ValueOf("ip('2001:db8::2')"));
        Assert.NotEqual(prefix, ValueOf("cidr('10.0.0.0/8')"));
        Assert.NotEqual(ValueOf("ip('1.2.3.4')"), ValueOf("cidr('1.2.3.4/32')"));
        Assert.NotEqual(ValueOf("ip('1.2.3.4')"), CelValue.FromString("1.2.3.4"));
        Assert.NotEqual(ValueOf("ip('1.2.3.4')"), CelValue.Null);
        Assert.Contains(same, new HashSet<CelValue> { first });
        Assert.DoesNotContain(prefix, new HashSet<CelValue> { first });
    }

    [Fact]
    public static void CelEquals_ComparesByContent()
    {
        CelValue first = ValueOf("ip('1.2.3.4')");

        Assert.True(CelValue.CelEquals(first, ValueOf("ip('1.2.3.4')")).TryGetBoolean(out bool same) && same);
        Assert.True(CelValue.CelEquals(first, ValueOf("ip('1.2.3.5')")).TryGetBoolean(out bool other) && !other);
        Assert.True(CelValue.CelEquals(first, ValueOf("cidr('1.2.3.4/32')")).TryGetBoolean(out bool prefix) && !prefix);
        Assert.True(CelValue.CelEquals(first, CelValue.Null).TryGetBoolean(out bool none) && !none);
    }

    // The types of an operator that no overload takes are refused when the expression is checked, and the dyn pair of an ordering at
    // evaluation: an abstract type has equality and no order.
    [Theory]
    [InlineData("ip('1.1.1.1') < ip('1.1.1.2')")]
    [InlineData("cidr('10.0.0.0/8') >= cidr('10.0.0.0/8')")]
    [InlineData("ip('1.2.3.4') == '1.2.3.4'")]
    [InlineData("ip('1.2.3.4') == cidr('1.2.3.4/32')")]
    [InlineData("ip('1.2.3.4') == 16909060")]
    public static void ExpressionWithoutAnOverload_IsRefusedByTheCheck(string expression)
    {
        CelCompileResult<CelValue> compiled = NetworkEnvironment.Environment.Compile<CelValue>(expression);

        Assert.False(compiled.IsSuccess);
    }

    [Fact]
    public static void OrderingOfTwoAbstractValues_IsNoMatchingOverloadAtEvaluation()
    {
        CelBindings bindings = new CelBindings().Add("x", ValueOf("ip('1.1.1.1')")).Add("y", ValueOf("ip('1.1.1.2')"));

        CelError error = Assert.Single(Evaluate("x < y", bindings).Errors);

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
    }

    // An abstract value is no map key, and the message names its type.
    [Fact]
    public static void MapLiteralWithAnAbstractKey_IsUnsupportedMapKeyType()
    {
        CelBindings bindings = new CelBindings().Add("x", ValueOf("cidr('10.0.0.0/8')"));

        CelError error = Assert.Single(Evaluate("{x: 1}", bindings).Errors);

        Assert.Equal((CelErrorCode.UnsupportedMapKeyType, "unsupported map key type 'net.CIDR'"), (error.Code, error.Message));
    }

    [Fact]
    public static void IndexOfAMapWithAnAbstractKey_IsNoSuchKey()
    {
        CelBindings bindings = new CelBindings().Add("x", ValueOf("ip('1.2.3.4')"));

        CelError error = Assert.Single(Evaluate("{'a': 1}[x]", bindings).Errors);

        Assert.Equal((CelErrorCode.NoSuchKey, "no such key: net.IP"), (error.Code, error.Message));
    }

    [Fact]
    public static void SelectionOnAnAbstractValue_NamesItsType()
    {
        CelBindings bindings = new CelBindings().Add("x", ValueOf("ip('1.2.3.4')"));

        CelError error = Assert.Single(Evaluate("x.octets", bindings).Errors);

        Assert.Equal((CelErrorCode.FieldSelectionNotSupported, "value of type 'net.IP' does not support field selection of 'octets'"), (error.Code, error.Message));
    }

    // The natural mapping has no .NET type for an abstract value: it is the CelValue, also as an object. Any other type is a
    // conversion error that names the type.
    [Fact]
    public static void Result_ConvertsToCelValueAndToObjectOnly()
    {
        CelValue expected = ValueOf("cidr('10.0.0.0/8')");

        CelValue asValue = NetworkEnvironment.Environment.Compile<CelValue>("cidr('10.0.0.0/8')").GetProgramOrThrow().Evaluate(new CelBindings(), null, TestContext.Current.CancellationToken).GetValueOrThrow();
        object asObject = NetworkEnvironment.Environment.Compile<object>("cidr('10.0.0.0/8')").GetProgramOrThrow().Evaluate(new CelBindings(), null, TestContext.Current.CancellationToken).GetValueOrThrow();
        CelEvalResult<string> asText = NetworkEnvironment.Environment.Compile<string>("dyn(cidr('10.0.0.0/8'))").GetProgramOrThrow().Evaluate(new CelBindings(), null, TestContext.Current.CancellationToken);

        Assert.Equal(expected, asValue);
        Assert.Equal(expected, Assert.IsType<CelValue>(asObject));
        CelError error = Assert.Single(asText.Errors);
        Assert.Equal(CelErrorCode.ResultConversion, error.Code);
        Assert.Contains("net.CIDR", error.Message, StringComparison.Ordinal);
    }

    // A variable is declared with the name of the type, as a description of an environment writes it; the value is a CelValue that an
    // evaluation gave, and a value of the other abstract type, a number or a string does not fit it.
    [Fact]
    public static void VariableDeclaredByTheNameOfTheType_TakesValuesOfThatTypeOnly()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Network(1))
            .AddVariable("address", CelType.ObjectNamed("net.IP"))
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<bool> program = environment.Compile<bool>("address.isLoopback() || address == ip('1.2.3.4')").GetProgramOrThrow();

        Assert.True(program.Evaluate(new CelBindings().Add("address", ValueOf("ip('127.0.0.1')")), null, TestContext.Current.CancellationToken).GetValueOrThrow());
        Assert.True(program.Evaluate(new CelBindings().Add("address", ValueOf("ip('1.2.3.4')")), null, TestContext.Current.CancellationToken).GetValueOrThrow());
        Assert.False(program.Evaluate(new CelBindings().Add("address", ValueOf("ip('1.2.3.5')")), null, TestContext.Current.CancellationToken).GetValueOrThrow());
        CelError error = Assert.Single(program.Evaluate(new CelBindings().Add("address", ValueOf("cidr('10.0.0.0/8')")), null, TestContext.Current.CancellationToken).Errors);
        Assert.Equal(
            (CelErrorCode.BindingTypeMismatch, "value of variable 'address' has type 'net.CIDR', which does not match the declared type 'net.IP'"),
            (error.Code, error.Message));
        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(program.Evaluate(new CelBindings().Add("address", "1.2.3.4"), null, TestContext.Current.CancellationToken).Errors).Code);
    }

    // A type that is a list of the abstract type is resolved at any depth.
    [Fact]
    public static void NameOfTheTypeInsideAList_IsResolved()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Network(1))
            .AddVariable("addresses", CelType.ListOf(CelType.ObjectNamed("net.IP")))
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<bool> program = environment.Compile<bool>("addresses.exists(a, a.isLoopback())").GetProgramOrThrow();
        CelValue list = ValueOf("[ip('1.2.3.4'), ip('::1')]");

        Assert.True(program.Evaluate(new CelBindings().Add("addresses", list), null, TestContext.Current.CancellationToken).GetValueOrThrow());
    }

    // Without the set the name is no type, and the error names the set that declares it.
    [Theory]
    [InlineData("net.IP", "network")]
    [InlineData("net.CIDR", "network")]
    public static void NameOfTheTypeWithoutTheSet_IsTypeNotDeclaredWithAHint(string name, string set)
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("address", CelType.ObjectNamed(name))
            .Build();

        CelError error = Assert.Single(result.Errors);

        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, "variable 'address' has type '" + name + "', which no type source or extension of the environment declares; add the extension set '" + set + "'"),
            (error.Code, error.Message));
    }

    // A name that no set declares has no hint.
    [Fact]
    public static void UnknownNameOfAType_IsTypeNotDeclaredWithoutAHint()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Network(1))
            .AddVariable("address", CelType.ObjectNamed("net.MAC"))
            .Build();

        CelError error = Assert.Single(result.Errors);

        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, "variable 'address' has type 'net.MAC', which no type source or extension of the environment declares"),
            (error.Code, error.Message));
    }

    // A function of the application declares a parameter and a result of the type by its name; its typed implementation takes and gives
    // CelValue, which the mapping calls dyn.
    [Fact]
    public static void FunctionOfTheApplication_TakesAndGivesAValueOfTheType()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Network(1))
            .AddFunction(new CelFunctionDecl(
                "sameOrFirst",
                CelOverloadDecl.Global("same_or_first_ip_ip", [CelType.ObjectNamed("net.IP"), CelType.ObjectNamed("net.IP")], CelType.ObjectNamed("net.IP"))))
            .AddFunctionImplementation<CelValue, CelValue, CelValue>("same_or_first_ip_ip", static (first, _) => first)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(
            environment.Compile<bool>("sameOrFirst(ip('1.2.3.4'), ip('::1')).family() == 4").GetProgramOrThrow().Evaluate(new CelBindings(), null, TestContext.Current.CancellationToken).GetValueOrThrow());
        Assert.False(environment.Compile<bool>("sameOrFirst(ip('1.2.3.4'), cidr('::/0')) == ip('::')").IsSuccess);
    }

    private static TheoryData<string> HoldingData()
    {
        TheoryData<string> data = [];
        foreach (string row in Holds.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            data.Add(row);
        }

        return data;
    }
}
