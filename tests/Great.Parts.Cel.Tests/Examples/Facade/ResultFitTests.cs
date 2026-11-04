using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// A list result for T = IReadOnlyList<long>. Whether it is a lazy view or a copy follows from the static
// type of the root, CelProgram<T>.ResultType, not from the expected type: list(dyn) to long can fail, so ld
// is copied, and its string element is ResultConversion; list(int) to long cannot fail, so x + y is a lazy
// view. A static type is no guarantee of the value: with x of type list(int) bound to [1] and y of type dyn
// bound to ['a'], x + y has the type list(int) and the value [1, 'a'], as in cel-go v0.32.0. So the value is
// checked against ResultType at any depth before the view; a mismatch is ResultConversion naming the type
// with the element that does not fit. The check and the copy come after the evaluation and are not charged.
// The place is the root.
[Trait("Requirement", "+R-10")]
public sealed class ResultFitTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("x", CelType.ListOf(CelType.Int))
        .AddVariable("y", CelType.Dyn)
        .AddVariable("ld", CelType.ListOf(CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-4")]
    public void CopyOfAListOfDyn_IsResultConversionForAnElementOfAnotherKind()
    {
        CelError error = Assert.Single(Run<IReadOnlyList<long>>("ld", Bindings(Strings("a")), null).Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 0), (error.Code, error.Stage, error.Location.Offset));
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("x + y", 2)]
    [InlineData("[1] + dyn(['a'])", 4)]
    public void ValueThatDoesNotFitTheResultType_IsResultConversionBeforeALazyView(string expression, int offset)
    {
        CelError error = Assert.Single(Run<IReadOnlyList<long>>(expression, Bindings(Strings("a")), null).Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal(
            "result of type 'list(string)' cannot be converted to .NET type '" + typeof(IReadOnlyList<long>).ToString() + "'",
            error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    [Fact]
    public void ValueThatFits_IsALazyView()
    {
        Assert.Equal(new long[] { 1, 2 }, Run<IReadOnlyList<long>>("x + y", Bindings(Ints(2)), null).GetValueOrThrow());
    }

    // For T = CelValue the value is returned as it is.
    [Fact]
    public void ValueForCelValue_IsTheListAsItIs()
    {
        Assert.True(Run<CelValue>("x + y", Bindings(Strings("a")), null).GetValueOrThrow().TryGetList(out CelList? list));
        Assert.Equal(2, list.Count);
        Assert.True(list[0].TryGetInt64(out long first));
        Assert.Equal(1L, first);
        Assert.True(list[1].TryGetString(out string? second));
        Assert.Equal("a", second);
    }

    // x bound to [1, 2, 3]: the read 1 and the check of the binding 3. The check before the lazy view is not
    // charged, so T = IReadOnlyList<long> costs what T = CelValue costs.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void CheckBeforeALazyView_IsNotCharged()
    {
        var bindings = new CelBindings().Add("x", Ints(1, 2, 3));

        Assert.True(Run<IReadOnlyList<long>>("x", bindings, 4).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Run<IReadOnlyList<long>>("x", bindings, 3).Errors).Code);
        Assert.True(Run<CelValue>("x", bindings, 4).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Run<CelValue>("x", bindings, 3).Errors).Code);
    }

    // x = [1]; y and ld = the given list.
    private static CelBindings Bindings(CelList list) => new CelBindings().Add("x", Ints(1)).Add("y", list).Add("ld", list);

    private static CelList Ints(params long[] values) => CelList.Create(Array.ConvertAll(values, CelValue.FromInt64));

    private static CelList Strings(params string[] values) => CelList.Create(Array.ConvertAll(values, CelValue.FromString));

    private CelEvalResult<T> Run<T>(string expression, CelBindings bindings, ulong? limit) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
}
