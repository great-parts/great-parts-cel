using System;
using System.Collections.Generic;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// The anonymous objects that these tests pass to the simple entry are registered by the generator of call sites, which runs in the
// compiler of this project: forms of the call and kinds of properties that the examples do not use. The results are the same under
// the JIT and under Native AOT, since the registrations read the properties without reflection.
[Trait("Requirement", "U-7")]
public sealed class GeneratedAnonymousDataTests
{
    [Fact]
    public void KeywordOfCSharpAsAPropertyName_IsAVariableOfThatName()
    {
        Assert.Equal(42L, CelExpression.Evaluate<long>("class + 1", new { @class = 41L }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NamedArguments_FindTheData()
    {
        Assert.Equal(6L, CelExpression.Evaluate<long>(data: new { named = 3L }, expression: "named * 2", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PropertiesOfOtherOrder_AreAnotherTypeAndBothWork()
    {
        Assert.Equal("a1", CelExpression.Evaluate<string>("s + string(n)", new { s = "a", n = 1L }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("b2", CelExpression.Evaluate<string>("s + string(n)", new { n = 2L, s = "b" }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ListsAndDictionariesOfTheList_AreReadByTheirStaticTypes()
    {
        var data = new
        {
            names = new[] { "a", "b" },
            weights = new Dictionary<string, double> { ["a"] = 1.5 },
            flags = new List<bool> { true, false },
        };

        Assert.True(CelExpression.Evaluate<bool>("names.size() == 2 && weights['a'] == 1.5 && flags[0]", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DateTimeOfTheKindUtc_IsATimestamp()
    {
        Assert.Equal(2026L, CelExpression.Evaluate<long>("moment.getFullYear()", new { moment = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) }, cancellationToken: TestContext.Current.CancellationToken));
    }
}
