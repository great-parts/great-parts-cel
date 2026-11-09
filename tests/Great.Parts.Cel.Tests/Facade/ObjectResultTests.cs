using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Facade;

// The result row of object values, a type described at run time included, and the expected type of a class of the
// application (docs/design/facade.md, "Expected type", "Result conversion").
public sealed class ObjectResultTests
{
    [Fact]
    public static void ObjectValue_IsTheInstanceItselfForTThatItIsAnInstanceOf()
    {
        var contact = new Contact { Name = "Ann" };
        var order = new SpecialOrder();

        Assert.Same(contact, Run<Contact>("c", Bind(("c", contact))).GetValueOrThrow());
        Assert.Same(contact, Run<Contact>("lead", Bind(("lead", contact))).GetValueOrThrow());
        Assert.Same(contact, Run<object>("c", Bind(("c", contact))).GetValueOrThrow());
        Assert.Same(order, Run<Order>("o", Bind(("o", order))).GetValueOrThrow());
        Assert.Same(order, Run<SpecialOrder>("o", Bind(("o", order))).GetValueOrThrow());
        Assert.Null(Run<Contact>("c", Bind(("c", CelValue.Null))).GetValueOrThrow());
    }

    [Theory]
    [InlineData("c", "acme.crm.Contact")]
    [InlineData("d", "Great.Parts.Cel.Tests.Evaluation.ObjectSamples+Unknown")]
    public static void InstanceThatIsNotAT_IsResultConversionWithTheNameOfItsType_NeverTypeNotAccessible(string expression, string celType)
    {
        CelError error = Assert.Single(Run<SpecialOrder>(expression, Bind(("c", new Contact()), ("d", new Unknown()))).Errors);

        Assert.Equal((CelErrorCode.ResultConversion, 0), (error.Code, error.Location.Offset));
        Assert.Equal(
            "result of type '" + celType + "' cannot be converted to .NET type 'Great.Parts.Cel.Tests.Values.ObjectTypeTests+SpecialOrder'",
            error.Message);
    }

    [Fact]
    public static void ObjectValueForAPrimitiveT_IsResultConversionWithTheNameOfItsDescription()
    {
        CelError error = Assert.Single(Run<string>("dyn(c)", Bind(("c", new Contact()))).Errors);

        Assert.Equal("result of type 'acme.crm.Contact' cannot be converted to .NET type 'System.String'", error.Message);
    }

    [Fact]
    public static void ExpectedTypeOfAClass_IsItsObjectTypeWhenASourceDescribesItExactly_ElseDyn()
    {
        CelError mismatch = Assert.Single(ObjectEnvironment.Compile<Order>("c").Errors);

        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, mismatch.Code);
        Assert.Equal("expression of type 'acme.crm.Contact' is not assignable to the expected type 'acme.Order'", mismatch.Message);
        Assert.True(ObjectEnvironment.Compile<SpecialOrder>("c").IsSuccess);
        Assert.True(ObjectEnvironment.Compile<Contact>("1").IsSuccess);
    }

    [Fact]
    public static void ExplicitExpectedTypeForAClass_IsDynATypeParameterOrAnObjectType()
    {
        Assert.True(ObjectEnvironment.Compile<Contact>("c", new CelCompileOptions { ExpectedType = ContactType.Type }).IsSuccess);
        Assert.True(ObjectEnvironment.Compile<Contact>("d", new CelCompileOptions { ExpectedType = CelType.Dyn }).IsSuccess);
        _ = Assert.Throws<ArgumentException>(() => ObjectEnvironment.Compile<Contact>("c", new CelCompileOptions { ExpectedType = CelType.Int }));
    }

    private static CelEvalResult<T> Run<T>(string expression, CelBindings bindings) =>
        ObjectEnvironment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
}
