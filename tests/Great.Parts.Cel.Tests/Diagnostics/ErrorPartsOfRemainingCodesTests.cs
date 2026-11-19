using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Tests.Protobuf;
using Great.Parts.Cel.Tests.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Diagnostics;

// The parts of the errors of the codes that ErrorPartsByCodeTests and the examples leave out, by the section "Parts of an error" of docs/error-codes.md:
// the check errors of a message literal, the evaluation errors of objects, protobuf messages and application functions, the build
// errors of declarations, and the two errors of any stage. A part the column does not name is null; a build error has no fragment.
[Trait("Requirement", "F-11")]
public sealed class ErrorPartsOfRemainingCodesTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    // Another delegate: two implementations that are the same delegate are one.
    private static readonly CelFunctionImplementation Other = arguments => arguments[0];

    private static readonly CelEnvironment Messages = MessageSamples.CreateEnvironment(MessageSamples.NewMessageType());

    [Fact]
    public void TypeMismatch_OfAnOperand_HasTheTwoTypesAndNoSubject()
    {
        CelError error = Assert.Single(Messages.Compile<CelValue>("1 || true").Errors);

        AssertParts(error, CelErrorCode.TypeMismatch, null, CelType.Bool, CelType.Int);
    }

    [Fact]
    public void TypeMismatch_OfAFieldValueOfALiteral_HasTheField()
    {
        CelError error = Assert.Single(Messages.Compile<CelValue>("acme.test.Message{id: 1.5}").Errors);

        AssertParts(error, CelErrorCode.TypeMismatch, "id", CelType.Int, CelType.Double);
    }

    [Fact]
    public void FieldNotAccessible_HasTheFieldAndTheTypeOfTheObject()
    {
        CelError error = Assert.Single(Messages.Compile<CelValue>("acme.test.Message{hidden: 1}").Errors);

        AssertParts(error, CelErrorCode.FieldNotAccessible, "hidden", null, CelType.ObjectNamed("acme.test.Message"));
    }

    // A member that is null although its CEL type does not allow null: the description reports it as the error value of the field.
    [Fact]
    public void UnexpectedNull_HasTheMemberAndTheTypeOfTheObject()
    {
        SharedTypeSamples.World world = SharedTypeSamples.World.Create(zooFirst: false);
        CelBindings bindings = new CelBindings().Add<CelValue>("a", CelValue.FromObject(world.Shelter, new SharedTypeSamples.Animal()));

        CelError error = Assert.Single(world.Evaluate("a.name", bindings).Errors);

        AssertParts(error, CelErrorCode.UnexpectedNull, "name", null, CelType.ObjectNamed("acme.Animal"));
    }

    [Fact]
    public void FieldNotSet_HasTheFieldAndTheTypeOfTheObject()
    {
        CelError error = Assert.Single(ObjectSamples.Evaluate("c.name", ObjectSamples.Bind(("c", new ObjectSamples.Contact()))).Errors);

        AssertParts(error, CelErrorCode.FieldNotSet, "name", null, CelType.ObjectNamed("acme.crm.Contact"));
    }

    [Fact]
    public void FieldTypeMismatch_HasTheFieldTheDeclaredTypeAndTheTypeOfTheValue()
    {
        CelError error = Assert.Single(ObjectSamples.Evaluate("broken.count", ObjectSamples.Bind(("broken", new ObjectSamples.Contact()))).Errors);

        AssertParts(error, CelErrorCode.FieldTypeMismatch, "count", CelType.Int, CelType.String);
    }

    [Fact]
    public void InvalidAny_HasTheTypeUrlAndNoTypes()
    {
        var x = new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = "type.googleapis.com/acme.Unknown" } };

        CelError error = Reading.ReadError("x.single_any", x);

        AssertParts(error, CelErrorCode.InvalidAny, "type.googleapis.com/acme.Unknown", null, null);
    }

    // A value that does not fit a field of a message of protobuf: the application of the package has no source value to take the
    // types from, so both are the declared CEL type of the field, int for int32 and uint for uint32.
    [Theory]
    [InlineData("TestAllTypes{single_int32: 2147483648}", "int")]
    [InlineData("TestAllTypes{single_uint32: 4294967296u}", "uint")]
    public static void ConversionRange_OfAProtobufField_HasTheDeclaredTypeOfTheFieldAsBothTypes(string expression, string type)
    {
        CelError error = Reading.ReadError(expression);

        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
        Assert.Null(error.Subject);
        Assert.Equal(type, error.ExpectedType?.ToString());
        Assert.Equal(type, error.ActualType?.ToString());
    }

    [Fact]
    public void ImplementationResultMismatch_HasTheOverloadTheDeclaredResultAndTheTypeOfTheResult()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<string, string>("blank", static _ => null!)
            .Build()
            .GetEnvironmentOrThrow();

        CelEvalResult<CelValue> result = environment.Compile<CelValue>("blank('a')").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        AssertParts(Assert.Single(result.Errors), CelErrorCode.ImplementationResultMismatch, "blank_string", CelType.String, CelType.Null);
    }

    [Fact]
    public void DuplicateType_HasTheTypeAndNoFragment()
    {
        var first = new SharedTypeSamples.Context("First", [new SharedTypeSamples.AnimalType()], []);
        var second = new SharedTypeSamples.Context("Second", [new SharedTypeSamples.DogType("acme.Animal")], []);

        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(first).AddTypes(second).Build();

        Assert.False(result.IsSuccess);
        Assert.All(result.Errors, error => AssertParts(error, CelErrorCode.DuplicateType, "acme.Animal", null, null));
    }

    [Fact]
    public void OverlappingOverloads_HasTheFunction()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl(
                "score",
                CelOverloadDecl.Global("score_1", [CelType.Int], CelType.Int),
                CelOverloadDecl.Global("score_2", [CelType.Int], CelType.Int)))
            .AddFunctionImplementation("score_1", Echo)
            .AddFunctionImplementation("score_2", Echo));

        AssertParts(error, CelErrorCode.OverlappingOverloads, "score", null, null);
    }

    [Fact]
    public void StandardLibraryConflict_HasTheName()
    {
        AssertParts(
            BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("int", CelType.Dyn)),
            CelErrorCode.StandardLibraryConflict,
            "int",
            null,
            null);
    }

    [Fact]
    public void MissingImplementation_HasTheOverloadIdentifier()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool))));

        AssertParts(error, CelErrorCode.MissingImplementation, "isInn_string", null, null);
    }

    [Fact]
    public void TypeNotDeclared_HasTheTypeName()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("order", CelType.ObjectNamed("acme.sales.Order")));

        AssertParts(error, CelErrorCode.TypeNotDeclared, "acme.sales.Order", null, null);
    }

    [Fact]
    public void UnusedImplementation_HasTheOverloadIdentifier()
    {
        AssertParts(
            BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunctionImplementation("nothing_int", Echo)),
            CelErrorCode.UnusedImplementation,
            "nothing_int",
            null,
            null);
    }

    [Fact]
    public void ImplementationSignatureMismatch_HasTheOverloadIdentifier()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool)))
            .AddFunctionImplementation<long, bool>("isInn_string", number => number > 0));

        AssertParts(error, CelErrorCode.ImplementationSignatureMismatch, "isInn_string", null, null);
    }

    // The conflict of two databases names no part: the error is about the environment as a whole.
    [Fact]
    public void TimeZoneDatabaseConflict_HasNoParts()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .UseTimeZones(CelTimeZoneDatabase.FromSystem())
            .UseTimeZones(CelTimeZoneDatabase.Empty));

        AssertParts(error, CelErrorCode.TimeZoneDatabaseConflict, null, null, null);
    }

    [Fact]
    public void DuplicateOverloadId_HasTheOverloadIdentifier()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("same_id", [CelType.Int], CelType.Int)))
            .AddFunction(new CelFunctionDecl("g", CelOverloadDecl.Global("same_id", [CelType.String], CelType.Int)))
            .AddFunctionImplementation("same_id", Echo));

        AssertParts(error, CelErrorCode.DuplicateOverloadId, "same_id", null, null);
    }

    [Fact]
    public void DuplicateImplementation_HasTheOverloadIdentifier()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("isInn", CelOverloadDecl.Global("isInn_string", [CelType.String], CelType.Bool)))
            .AddFunctionImplementation("isInn_string", Echo)
            .AddFunctionImplementation("isInn_string", Other));

        AssertParts(error, CelErrorCode.DuplicateImplementation, "isInn_string", null, null);
    }

    [Fact]
    public void DuplicateExtension_HasTheNameOfTheSet()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(Own("acme", 1)).AddExtension(Own("acme", 1)));

        AssertParts(error, CelErrorCode.DuplicateExtension, "acme", null, null);
    }

    // The stack of the thread is nearly used up where the program is evaluated: the error has no part.
    [Fact]
    public void InsufficientStack_HasNoParts()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<long>("x").Build().GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("x + 1").GetProgramOrThrow();

        CelEvalResult<long> result = EvaluateOnExhaustedStack(program, new CelBindings().Add("x", 41L));

        AssertParts(Assert.Single(result.Errors), CelErrorCode.InsufficientStack, null, null, null);
    }

    // An exception inside the library cannot be brought in through the public API, so the test calls the internal entry of the plan
    // with bindings whose lookup throws.
    [Fact]
    public void InternalError_HasNoPartsButTheException()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<long>("x").Build().GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("1 + x").GetProgramOrThrow();
        var bindings = new ThrowingBindings();

        CelValue value = program.Plan.Evaluate(bindings, null, CancellationToken.None);

        Assert.True(value.TryGetError(out CelError? error));
        AssertParts(error, CelErrorCode.InternalError, null, null, null);
        Assert.Same(bindings.Thrown, error.Exception);
    }

    private static CelExtension Own(string name, uint version) =>
        CelExtension.Create(
            name,
            version,
            [new CelFunctionDecl(name + "Probe", CelOverloadDecl.Global(name + "_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { [name + "_probe_string"] = Echo });

    private static CelError BuildError(CelEnvironmentBuilder builder)
    {
        CelError error = Assert.Single(builder.Build().Errors);
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Equal(default, error.Span);
        return error;
    }

    private static void AssertParts(CelError error, CelErrorCode code, string? subject, CelType? expected, CelType? actual)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(subject, error.Subject);
        Assert.Equal(expected, error.ExpectedType);
        Assert.Equal(actual, error.ActualType);
    }

    // Descends until the runtime reports that the stack is nearly used up, then evaluates there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CelEvalResult<long> EvaluateOnExhaustedStack(CelProgram<long> program, CelBindings bindings)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return program.Evaluate(bindings, cancellationToken: CancellationToken.None);
        }

        Span<byte> frame = stackalloc byte[1024];
        frame.Fill(1);
        CelEvalResult<long> result = EvaluateOnExhaustedStack(program, bindings);
        Assert.Equal((byte)1, frame[^1]);
        return result;
    }

    // Bindings whose every lookup throws the same exception.
    private sealed class ThrowingBindings : IReadOnlyDictionary<string, CelValue>
    {
        public InvalidOperationException Thrown { get; } = new("lookup failed");

        public int Count => throw Thrown;

        public IEnumerable<string> Keys => throw Thrown;

        public IEnumerable<CelValue> Values => throw Thrown;

        public CelValue this[string key] => throw Thrown;

        public bool ContainsKey(string key) => throw Thrown;

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out CelValue value) => throw Thrown;

        public IEnumerator<KeyValuePair<string, CelValue>> GetEnumerator() => throw Thrown;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
