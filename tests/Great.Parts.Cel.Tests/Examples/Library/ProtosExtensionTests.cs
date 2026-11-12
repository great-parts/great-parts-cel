using System.Collections.Generic;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The set protos, version 0 (the page of the extension sets, "protos"): proto.getExt(m, e) reads the proto2 extension e of
// the message m and proto.hasExt(m, e) tests that it is set, where e is the full name of the extension; the extension
// is a field of the description of the message, added with AddProtoExtension, so the macros are a selection and a has()
// and the rules of fields hold: an extension that is not set reads as the default of its kind (0, the empty message, the
// empty list), a message that is not the extended one, or an extension that is not registered, is NoSuchField (2004) at type
// checking at the "(" of the call and EvalNoSuchField (3001) through dyn, and the name is not reachable by a plain
// selection. The set has one version, 0; the member of the catalog does not check it, and Build says
// UnknownExtensionVersion. The environment description in JSON takes the name protos. The values are those of cel-go v0.32.0
// (a probe of the oracle, 2026-11-11). Variables: p2x is the message of ProtobufExample.WithExtensions, p2e a proto2
// TestAllTypes with nothing set, x a proto3 TestAllTypes; the extensions are those of the extension file of the cel-spec
// test messages, and the environments have no container.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-3")]
[Trait("Requirement", "F-2")]
public sealed class ProtosExtensionTests
{
    private const string Values = """
        proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext) => 7
        proto.hasExt(p2x, cel.expr.conformance.proto2.int32_ext) => true
        proto.hasExt(p2e, cel.expr.conformance.proto2.int32_ext) => false
        proto.getExt(p2e, cel.expr.conformance.proto2.int32_ext) => 0
        proto.getExt(p2x, cel.expr.conformance.proto2.nested_ext).single_int32 => 9
        proto.getExt(p2x, cel.expr.conformance.proto2.nested_ext) == cel.expr.conformance.proto2.TestAllTypes{single_int32: 9} => true
        proto.hasExt(p2x, cel.expr.conformance.proto2.nested_ext) => true
        proto.hasExt(p2e, cel.expr.conformance.proto2.nested_ext) => false
        proto.getExt(p2e, cel.expr.conformance.proto2.nested_ext) == cel.expr.conformance.proto2.TestAllTypes{} => true
        size(proto.getExt(p2x, cel.expr.conformance.proto2.repeated_test_all_types)) => 2
        proto.getExt(p2x, cel.expr.conformance.proto2.repeated_test_all_types)[1].single_int32 => 2
        proto.hasExt(p2x, cel.expr.conformance.proto2.repeated_test_all_types) => true
        proto.hasExt(p2e, cel.expr.conformance.proto2.repeated_test_all_types) => false
        size(proto.getExt(p2e, cel.expr.conformance.proto2.repeated_test_all_types)) => 0
        proto.getExt(p2x, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext) => 8
        proto.hasExt(p2x, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext) => true
        proto.hasExt(p2e, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext) => false
        proto.getExt(p2x, cel.expr.conformance.proto2.nested_enum_ext) => 1
        proto.getExt(dyn(p2x), cel.expr.conformance.proto2.int32_ext) => 7
        proto.hasExt(dyn(p2x), cel.expr.conformance.proto2.int32_ext) => true
        proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext) + proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext) => 14
        has(p2x.single_int32) && proto.hasExt(p2x, cel.expr.conformance.proto2.int32_ext) => true
        """;

    // A selection that the macro makes has the name of the extension for the field; the place of the error is the "(" of
    // the call, and at evaluation the name is the same.
    private const string Errors = """
        Check | proto.getExt^(x, cel.expr.conformance.proto2.int32_ext) | NoSuchField | undefined field 'cel.expr.conformance.proto2.int32_ext' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | proto.hasExt^(x, cel.expr.conformance.proto2.int32_ext) | NoSuchField | undefined field 'cel.expr.conformance.proto2.int32_ext' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | proto.getExt^(p2x, cel.expr.conformance.proto2.nope) | NoSuchField | undefined field 'cel.expr.conformance.proto2.nope' in type 'cel.expr.conformance.proto2.TestAllTypes'
        Check | proto.hasExt^(p2x, cel.expr.conformance.proto2.nope) | NoSuchField | undefined field 'cel.expr.conformance.proto2.nope' in type 'cel.expr.conformance.proto2.TestAllTypes'
        Check | proto.getExt^(p2x, proto2.int32_ext) | NoSuchField | undefined field 'proto2.int32_ext' in type 'cel.expr.conformance.proto2.TestAllTypes'
        Check | proto.getExt^(1, cel.expr.conformance.proto2.int32_ext) | NoSuchField | -
        Check | p2x^.cel.expr.conformance.proto2.int32_ext | NoSuchField | undefined field 'cel' in type 'cel.expr.conformance.proto2.TestAllTypes'
        Evaluate | proto.getExt^(dyn(x), cel.expr.conformance.proto2.int32_ext) | EvalNoSuchField | no such field 'cel.expr.conformance.proto2.int32_ext' in cel.expr.conformance.proto3.TestAllTypes
        Evaluate | proto.hasExt^(dyn(x), cel.expr.conformance.proto2.int32_ext) | EvalNoSuchField | no such field 'cel.expr.conformance.proto2.int32_ext' in cel.expr.conformance.proto3.TestAllTypes
        """;

    private static readonly (string Expression, CelType Type)[] ResultTypes =
    [
        ("proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext)", CelType.Int),
        ("proto.getExt(p2x, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext)", CelType.Int),
        ("proto.getExt(p2x, cel.expr.conformance.proto2.nested_enum_ext)", CelType.Int),
        ("proto.getExt(p2x, cel.expr.conformance.proto2.nested_ext)", CelType.ObjectNamed("cel.expr.conformance.proto2.TestAllTypes")),
        ("proto.getExt(p2x, cel.expr.conformance.proto2.repeated_test_all_types)", CelType.ListOf(CelType.ObjectNamed("cel.expr.conformance.proto2.TestAllTypes"))),
        ("proto.hasExt(p2x, cel.expr.conformance.proto2.int32_ext)", CelType.Bool),
        ("proto.getExt(dyn(p2x), cel.expr.conformance.proto2.int32_ext)", CelType.Dyn),
    ];

    private readonly CelEnvironment environment = Builder().AddExtension(CelExtensions.Protos(0)).Build().GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("p2x", ProtobufExample.WithExtensions())
        .Add("p2e", new Proto2.TestAllTypes())
        .Add("x", new Proto3.TestAllTypes());

    [Fact]
    public void GetExtAndHasExt_ReadAndTestTheExtensionsOfAMessage()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Values, bindings));
    }

    [Fact]
    public void ExtensionThatTheMessageDoesNotHave_IsNoSuchField()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, Errors, bindings));
    }

    [Fact]
    public void Macros_HaveTheTypeOfTheExtension()
    {
        var failures = new List<string>();
        foreach ((string expression, CelType type) in ResultTypes)
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
            if (!compiled.IsSuccess)
            {
                failures.Add(expression + ": " + compiled.Errors[0].Code);
            }
            else if (!compiled.GetProgramOrThrow().ResultType.Equals(type))
            {
                failures.Add(expression + ": " + compiled.GetProgramOrThrow().ResultType);
            }
        }

        Assert.Empty(failures);
    }

    // The facade returns the extension as a .NET value of the generated class.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void Extension_IsAResultOfItsDotNetType()
    {
        CelBindings own = new CelBindings().Add("p2x", ProtobufExample.WithExtensions());
        CelEnvironment two = Builder().AddExtension(CelExtensions.Protos(0)).Build().GetEnvironmentOrThrow();

        long number = two.Compile<long>("proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext)")
            .GetProgramOrThrow().Evaluate(own, cancellationToken: CancellationToken.None).GetValueOrThrow();
        Proto2.TestAllTypes nested = two.Compile<Proto2.TestAllTypes>("proto.getExt(p2x, cel.expr.conformance.proto2.nested_ext)")
            .GetProgramOrThrow().Evaluate(own, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal(7L, number);
        Assert.Equal(9, nested.SingleInt32);
    }

    // The member of the catalog has the name and the version it is given and does not check the version; Build lists the
    // versions that exist, and the same set twice is one declaration.
    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-8")]
    public void Protos_HasOneVersion()
    {
        CelExtension asked = CelExtensions.Protos(1);
        Assert.Equal(("protos", 1u), (asked.Name, asked.Version));

        CelBuildResult unknown = Builder().AddExtension(asked).Build();
        CelError error = Assert.Single(unknown.Errors);

        Assert.Equal((CelErrorCode.UnknownExtensionVersion, CelErrorStage.Build, "extension 'protos' has no version 1; available versions: 0"), (error.Code, error.Stage, error.Message));
        Assert.True(Builder().AddExtension(CelExtensions.Protos(0)).AddExtension(CelExtensions.Protos(0)).Build().IsSuccess);
        Assert.Equal(("protos", 0u), (CelExtensions.Protos(0).Name, CelExtensions.Protos(0).Version));
    }

    // The name protos in the description in JSON adds the set, and without it the macros are not expanded.
    [Fact]
    [Trait("Requirement", "F-7")]
    public void NameProtos_InTheDescriptionInJson_AddsTheSet()
    {
        const string Json = """{"extensions": [{"name": "protos", "version": "0"}]}""";
        CelEnvironmentBuilder builder = Builder();
        Assert.Empty(CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(Json)));
        const string Expression = "proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext) == 7";

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        Assert.True(environment.Compile<bool>(Expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("p2x", ProtobufExample.WithExtensions()), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.False(Builder().Build().GetEnvironmentOrThrow().Compile<bool>(Expression).IsSuccess);
    }

    private static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ProtobufExample.TypesAll)
        .AddVariable<Proto2.TestAllTypes>("p2x")
        .AddVariable<Proto2.TestAllTypes>("p2e")
        .AddVariable<Proto3.TestAllTypes>("x");
}
