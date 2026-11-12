#if NET11_0_OR_GREATER
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// The refusal of a field of the type decimal in an environment without the set, in the shapes that the examples of the field leave: the
// presence test of a field of a nested type, a field whose type has the decimal at a depth of an optional or of lists in lists, and
// a field selected from the variable of a macro. The rule asks whether the type of the field mentions the kind Decimal, at any depth;
// the other fields of the same types stay readable. The types are described over a dictionary, since a check reads no value.
public sealed class DecimalFieldNestedCheckTests
{
    private const string Reason = "is not available to expressions: the CEL type decimal needs the extension set 'decimal'";

    private static readonly CelObjectType Line = CelObjectTypeBuilder.ForDictionary("acme.sales.Line")
        .AddField("price", CelType.Decimal)
        .AddField("note", CelType.String)
        .Build();

    private static readonly CelObjectType Order = CelObjectTypeBuilder.ForDictionary("acme.sales.Order")
        .AddField("line", Line.Type)
        .AddField("maybe", CelType.OptionalOf(CelType.Decimal))
        .AddField("matrix", CelType.ListOf(CelType.ListOf(CelType.Decimal)))
        .AddField("title", CelType.String)
        .Build();

    private const string Rows = $$"""
        Check | has^(order.line.price) | FieldNotAccessible | field 'price' of type 'acme.sales.Line' {{Reason}}
        Check | order^.maybe | FieldNotAccessible | field 'maybe' of type 'acme.sales.Order' {{Reason}}
        Check | has^(order.maybe) | FieldNotAccessible | field 'maybe' of type 'acme.sales.Order' {{Reason}}
        Check | order^.matrix | FieldNotAccessible | field 'matrix' of type 'acme.sales.Order' {{Reason}}
        Check | [order].exists(o, o^.matrix == []) | FieldNotAccessible | field 'matrix' of type 'acme.sales.Order' {{Reason}}
        Check | order^.maybe == order^.matrix | FieldNotAccessible FieldNotAccessible | field 'maybe' of type 'acme.sales.Order' {{Reason}} ;; field 'matrix' of type 'acme.sales.Order' {{Reason}}
        """;

    // The fields of the same types that are not decimal are as readable as in any environment.
    [Fact]
    public static void FieldsThatAreNotDecimal_CompileInTheSameEnvironment()
    {
        CelEnvironment environment = Environment();

        Assert.True(environment.Compile<CelValue>("order.title == 'a' && has(order.title)").IsSuccess);
        Assert.True(environment.Compile<CelValue>("order.line.note == 'n' && has(order.line.note)").IsSuccess);
        Assert.True(environment.Compile<CelValue>("[order].all(o, o.title != '')").IsSuccess);
    }

    [Fact]
    public static void FieldOfTheTypeDecimalAtADepth_IsFieldNotAccessibleAtTheSelectionOrTheCall()
    {
        Assert.Empty(ExtensionRows.CheckErrors(Environment(), Rows));
    }

    private static CelEnvironment Environment() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Optional(2))
            .AddTypes(CelTypeProvider.Create("sales", Order, Line))
            .AddVariable("order", Order.Type)
            .Build()
            .GetEnvironmentOrThrow();
}
#endif
