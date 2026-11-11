using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Declarations.ExtensionTestSets;

namespace Great.Parts.Cel.Tests.Declarations;

// The validator of an overload: the checker runs it after the traversal of the tree, when it found no error, on the shape of
// the call with the final static types, and reports its answer with the place of the call or of the part it names.
public sealed class CallValidatorTests
{
    private static readonly CelType ListOfDyn = CelType.ListOf(CelType.Dyn);

    [Fact]
    public void Validator_GetsTheShapeOfAReceiverCallWithLiteralsAndFinalTypes()
    {
        List<CallShape> shapes = [];
        CelEnvironment environment = EnvironmentWith(shapes, static _ => CallValidation.Ok);
        const string Text = "'%d'.check([1, 'a'], {'k': 2})";

        CelCompileResult<CelValue> compiled = Compile(environment, Text);

        Assert.True(compiled.IsSuccess);
        CallShape shape = Assert.Single(shapes);
        Assert.NotNull(shape.Receiver);
        ValueShape receiver = shape.Receiver;
        Assert.Equal((0, "%d"), (receiver.Offset, receiver.StringConstant));
        Assert.Equal(CelType.String, receiver.Type);
        Assert.Equal(2, shape.Arguments.Length);

        ValueShape list = shape.Arguments[0];
        Assert.Equal(Text.IndexOf('[', StringComparison.Ordinal), list.Offset);
        Assert.Equal(CelType.ListOf(CelType.Dyn), list.Type);
        ImmutableArray<ValueShape> elements = Assert.NotNull(list.Elements);
        Assert.Equal(new[] { CelType.Int, CelType.String }, elements.Select(element => element.Type));
        Assert.Equal("a", elements[1].StringConstant);
        Assert.Null(elements[0].StringConstant);
        Assert.False(list.HasOptionalElements);
        Assert.Null(list.Entries);

        ValueShape map = shape.Arguments[1];
        ImmutableArray<(ValueShape Key, ValueShape Value)> entries = Assert.NotNull(map.Entries);
        (ValueShape key, ValueShape value) = Assert.Single(entries);
        Assert.Equal("k", key.StringConstant);
        Assert.Equal(CelType.Int, value.Type);
        Assert.Null(map.Elements);
    }

    [Fact]
    public void Validator_GetsTheFinalTypeOfAnEmptyListWithTheFreeVariableAsDyn()
    {
        List<CallShape> shapes = [];
        CelEnvironment environment = EnvironmentWith(shapes, static _ => CallValidation.Ok);

        Assert.True(Compile(environment, "'a'.check([], {})").IsSuccess);

        CallShape shape = Assert.Single(shapes);
        Assert.Equal(ListOfDyn, shape.Arguments[0].Type);
        Assert.Equal(CelType.MapOf(CelType.Dyn, CelType.Dyn), shape.Arguments[1].Type);
    }

    [Fact]
    public void ValidatorOfAGlobalOverload_GetsNoReceiver()
    {
        List<CallShape> shapes = [];
        CelEnvironment environment = EnvironmentWith(shapes, static _ => CallValidation.Ok);

        Assert.True(Compile(environment, "plain([1])").IsSuccess);

        CallShape shape = Assert.Single(shapes);
        Assert.Null(shape.Receiver);
        Assert.Equal(CelType.ListOf(CelType.Int), Assert.Single(shape.Arguments).Type);
    }

    [Fact]
    public void RejectedCall_IsAnErrorOfTheCheckStageAtTheCall()
    {
        CelEnvironment environment = EnvironmentWith([], static _ => new CallValidation(CelErrorCode.InvalidFormatString, "boom", null));
        const string Text = "'x'.check([1], {})";

        CelCompileResult<CelValue> compiled = Compile(environment, Text);

        Assert.False(compiled.IsSuccess);
        CelError error = Assert.Single(compiled.Errors);
        Assert.Equal((CelErrorCode.InvalidFormatString, CelErrorStage.Check, "invalid format string: boom"), (error.Code, error.Stage, error.Message));
        Assert.Equal(Text.IndexOf('(', StringComparison.Ordinal), error.Location.Offset);
    }

    [Fact]
    public void RejectedCall_IsAtThePartThatTheAnswerNames()
    {
        CelEnvironment environment = EnvironmentWith(
            [],
            static call => new CallValidation(CelErrorCode.InvalidFormatString, "bad element", call.Arguments[0].Elements!.Value[1].Offset));
        const string Text = "'x'.check([1, 2], {})";

        CelError error = Assert.Single(Compile(environment, Text).Errors);

        Assert.Equal(Text.IndexOf('2', StringComparison.Ordinal), error.Location.Offset);
    }

    [Fact]
    public void RejectedCallWithNoMatchingOverload_HasTheUsualTextWithTheFinalTypes()
    {
        CelEnvironment environment = EnvironmentWith([], static _ => new CallValidation(CelErrorCode.NoMatchingOverload, "", null));

        CelError error = Assert.Single(Compile(environment, "'x'.check([], {})").Errors);

        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
        Assert.Equal("found no matching overload for 'check' applied to '(string, list(dyn), map(dyn, dyn))'", error.Message);
    }

    [Fact]
    public void Validators_RunInTheOrderOfTheCallsByOffsetAndTheirErrorsAreOrderedToo()
    {
        List<int> order = [];
        CelEnvironment environment = EnvironmentWith(
            [],
            call =>
            {
                order.Add(call.Receiver!.Offset);
                return new CallValidation(CelErrorCode.InvalidFormatString, call.Receiver.StringConstant!, null);
            });
        const string Text = "['b'.check([], {}), 'a'.check([], {})]";

        CelCompileResult<CelValue> compiled = Compile(environment, Text);

        Assert.Equal(new[] { Text.IndexOf("'b'", StringComparison.Ordinal), Text.IndexOf("'a'", StringComparison.Ordinal) }, order);
        Assert.Equal(2, compiled.Errors.Length);
        Assert.Equal("invalid format string: b", compiled.Errors[0].Message);
        Assert.Equal("invalid format string: a", compiled.Errors[1].Message);
    }

    [Fact]
    public void Validator_DoesNotRunWhenTheTreeHasAnError()
    {
        List<CallShape> shapes = [];
        CelEnvironment environment = EnvironmentWith(shapes, static _ => new CallValidation(CelErrorCode.InvalidFormatString, "boom", null));

        CelCompileResult<CelValue> compiled = Compile(environment, "'a'.check([], {}) == 1 && unknown");

        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(compiled.Errors).Code);
        Assert.Empty(shapes);
    }

    [Fact]
    public void Validator_RunsOnlyForACallWhoseCandidatesIncludeItsOverload()
    {
        List<CallShape> shapes = [];
        CelEnvironment environment = EnvironmentWith(shapes, static _ => new CallValidation(CelErrorCode.InvalidFormatString, "boom", null));

        Assert.True(Compile(environment, "'a'.check(1)").IsSuccess);
        Assert.Empty(shapes);
        Assert.False(Compile(environment, "'a'.check([1], {'k': 1})").IsSuccess);
        Assert.Single(shapes);
    }

    // The validators run before the check against the expected type, so a rejected call is reported alone.
    [Fact]
    public void Validator_RunsBeforeTheCheckAgainstTheExpectedType()
    {
        CelEnvironment environment = EnvironmentWith([], static _ => new CallValidation(CelErrorCode.InvalidFormatString, "boom", null));

        CelCompileResult<string> compiled = environment.Compile<string>("'a'.check([], {})");

        Assert.False(compiled.IsSuccess);
        Assert.Equal(CelErrorCode.InvalidFormatString, Assert.Single(compiled.Errors).Code);
    }

    [Fact]
    public void ValidatorThatAcceptsTheCall_LeavesTheProgramAsAnyOther()
    {
        CelEnvironment environment = EnvironmentWith([], static _ => CallValidation.Ok);

        CelCompileResult<CelValue> compiled = Compile(environment, "'a'.check([], {})");

        Assert.True(compiled.IsSuccess);
        Assert.Equal(CelType.Int, compiled.GetProgramOrThrow().ResultType);
    }

    // check(string, list(dyn), map(dyn, dyn)) -> int with the validator, check(string, int) -> int without one, and
    // plain(list(int)) -> int with the validator.
    private static CelEnvironment EnvironmentWith(List<CallShape> shapes, CallValidator answer)
    {
        CallValidator validator = call =>
        {
            shapes.Add(call);
            return answer(call);
        };
        string source = Source("alpha", 1);
        CatalogExtension alpha = Set(
            "alpha",
            1,
            [
                Receiver(source, "check", "alpha_check", CelType.String, [ListOfDyn, CelType.MapOf(CelType.Dyn, CelType.Dyn)], CelType.Int, validator: validator),
                Receiver(source, "check", "alpha_check_int", CelType.String, [CelType.Int], CelType.Int),
                Global(source, "plain", "alpha_plain", [CelType.ListOf(CelType.Int)], CelType.Int, validator: validator),
            ]);
        return BuildEnvironment(Builder().AddExtension(Of(alpha)));
    }

    private static CelCompileResult<CelValue> Compile(CelEnvironment environment, string text) => environment.Compile<CelValue>(text);
}
