using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The check before a lazy view goes by the expected type of T, not by the result type, and lets null through where the
// view reads a null reference: an element or a map value of type string, byte[], CelList, CelMap or CelType. So the choice
// between a view and a copy does not change the outcome (docs/design/facade.md, "Result conversion"). The first expression of
// each pair has a list or map type and gives a view, the second has the type dyn and gives a copy.
public sealed class LazyViewCheckTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void NullElementOfAReferenceType_IsAValueOfTheViewAndOfTheCopy()
    {
        foreach (string expression in (string[])["['a'] + dyn([null])", "dyn(['a', null])"])
        {
            Assert.Equal(new string?[] { "a", null }, Value<IReadOnlyList<string?>>(expression));
        }

        foreach (string expression in (string[])["[b'a'] + dyn([null])", "dyn([b'a', null])"])
        {
            IReadOnlyList<byte[]> bytes = Value<IReadOnlyList<byte[]>>(expression);
            Assert.Equal(new byte[] { 0x61 }, bytes[0]);
            Assert.Null(bytes[1]);
        }

        foreach (string expression in (string[])["[[1]] + dyn([null])", "dyn([[1], null])"])
        {
            IReadOnlyList<CelList> lists = Value<IReadOnlyList<CelList>>(expression);
            Assert.Single(lists[0]);
            Assert.Null(lists[1]);
        }

        foreach (string expression in (string[])["[{'a': 1}] + dyn([null])", "dyn([{'a': 1}, null])"])
        {
            IReadOnlyList<CelMap> maps = Value<IReadOnlyList<CelMap>>(expression);
            Assert.Single(maps[0]);
            Assert.Null(maps[1]);
        }

        foreach (string expression in (string[])["[type(1)] + dyn([null])", "dyn([type(1), null])"])
        {
            Assert.Equal(new CelType?[] { CelType.Int, null }, Value<IReadOnlyList<CelType?>>(expression));
        }
    }

    [Fact]
    public void NullMapValueOfAReferenceType_IsAValueOfTheViewAndOfTheCopy()
    {
        foreach (string expression in (string[])["([{'a': 'x'}] + dyn([{'b': null}]))[1]", "dyn({'b': null})"])
        {
            IReadOnlyDictionary<string, string> map = Value<IReadOnlyDictionary<string, string>>(expression);
            Assert.Equal("b", Assert.Single(map).Key);
            Assert.Null(map["b"]);
        }
    }

    // A value type does not take null in the view nor in the copy. The view names the type of the mismatch, the copy the
    // name of the type of the value.
    [Fact]
    public void NullElementOfAValueType_IsResultConversionInTheViewAndInTheCopy()
    {
        Assert.Equal("list(null_type)", ErrorType<IReadOnlyList<long>>("[1] + dyn([null])"));
        Assert.Equal("list", ErrorType<IReadOnlyList<long>>("dyn([1, null])"));
        Assert.Equal("list", ErrorType<IReadOnlyList<int>>("[1] + dyn([null])"));
        Assert.Equal("list", ErrorType<IReadOnlyList<int>>("dyn([1, null])"));
        Assert.Equal("list(null_type)", ErrorType<IReadOnlyList<ReadOnlyMemory<byte>>>("[b'a'] + dyn([null])"));
        Assert.Equal("list", ErrorType<IReadOnlyList<ReadOnlyMemory<byte>>>("dyn([b'a', null])"));
    }

    // The root is not checked: a null at the top is null for string and object.
    [Fact]
    public void NullAtTheRoot_IsNull()
    {
        Assert.Null(Value<string>("(['a'] + dyn([null]))[1]"));
        Assert.Null(Value<object>("(['a'] + dyn([null]))[1]"));
    }

    // The type of the mismatch is written by the expected type without the admission of null.
    [Fact]
    public void ElementOfAnotherKind_NamesTheTypeOfTheMismatch()
    {
        Assert.Equal("list(int)", ErrorType<IReadOnlyList<string>>("['a'] + dyn([1])"));
        Assert.Equal("list(string)", ErrorType<IReadOnlyList<CelList>>("[[1]] + dyn(['a'])"));
    }

    // A position that the view reads as object or CelValue is dyn: the check does not refuse what the view reads.
    [Fact]
    public void ElementsReadAsObjectOrCelValue_AreNotCheckedByTheResultType()
    {
        Assert.Equal(new object[] { 1L, "a" }, Value<IReadOnlyList<object>>("[1] + dyn(['a'])"));
        Assert.Equal(new[] { CelValue.FromInt64(1), CelValue.FromString("a") }, Value<IReadOnlyList<CelValue>>("[1] + dyn(['a'])"));
    }

    private static T Value<T>(string expression) =>
        Environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();

    // The {celType} of the single error ResultConversion.
    private static string ErrorType<T>(string expression)
    {
        CelError error = Assert.Single(Environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).Errors);
        Assert.Equal(CelErrorCode.ResultConversion, error.Code);
        const string Prefix = "result of type '";
        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        int end = error.Message.IndexOf("' cannot be converted", StringComparison.Ordinal);
        return error.Message[Prefix.Length..end];
    }
}
