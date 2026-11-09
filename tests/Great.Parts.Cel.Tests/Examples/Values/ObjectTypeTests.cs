using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// Descriptions of types described at run time (the type mapping page, "Types described at run time"):
// CelObjectTypeBuilder<TCarrier> reads a field of a carrier class through a delegate, and
// CelObjectTypeBuilder.ForDictionary reads the entry with the name of the field from an
// IReadOnlyDictionary<string, object?>. A field has the number of its position, the description has no
// .NET type, and CelValue.FromObject pairs a carrier with its description.
public sealed class ObjectTypeTests
{
    private static readonly string[] RefusedFieldNames = ["amount", "a-b", "1a", ""];

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void Builder_NumbersTheFieldsInTheOrderOfDeclaration()
    {
        CelObjectType deal = DealBuilder().Build();

        Assert.Equal("acme.crm.Deal", deal.Name);
        Assert.Null(deal.ClrType);
        Assert.Equal(CelType.ObjectNamed("acme.crm.Deal"), deal.Type);
        Assert.Equal(
            new[] { (0, "amount", CelType.Int), (1, "title", CelType.WrapperOf(CelType.String)) },
            deal.Fields.Select(field => (field.Number, field.Name, field.Type)));
        Assert.True(deal.TryGetField("title", out CelObjectField? title));
        Assert.Equal(1, title.Number);
        Assert.False(deal.TryGetField("Title", out _));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-7")]
    public void DictionaryForm_DescribesTheEntriesOfADictionary()
    {
        CelObjectType contact = ContactBuilder().AddField("manager", CelType.ObjectNamed("acme.crm.Contact")).Build();

        Assert.Equal("acme.crm.Contact", contact.Name);
        Assert.Null(contact.ClrType);
        Assert.Equal(new[] { (0, "name"), (1, "manager") }, contact.Fields.Select(field => (field.Number, field.Name)));
    }

    // Build copies the fields: a later AddField changes only the descriptions built after it.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void FieldAddedAfterBuild_DoesNotChangeTheBuiltDescription()
    {
        CelObjectTypeBuilder<DealCarrier> builder = DealBuilder();
        CelObjectType first = builder.Build();

        CelObjectType second = builder.AddField("stage", CelType.String, _ => CelValue.FromString("open")).Build();

        Assert.Equal(2, first.Fields.Length);
        Assert.Equal(3, second.Fields.Length);
        Assert.NotSame(first, second);
    }

    // A repeated name, a name that is not an identifier and a type parameter are refused at once, by both forms.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void FieldNameThatIsRepeatedOrNotAnIdentifier_IsArgumentException()
    {
        foreach (string name in RefusedFieldNames)
        {
            Assert.Throws<ArgumentException>(() => DealBuilder().AddField(name, CelType.Int, _ => CelValue.FromInt64(0)));
            Assert.Throws<ArgumentException>(() => CelObjectTypeBuilder.ForDictionary("acme.crm.Contact")
                .AddField("amount", CelType.Int)
                .AddField(name, CelType.Int));
        }
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void FieldOfATypeParameter_IsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DealBuilder().AddField("item", CelType.TypeParameter("A"), _ => CelValue.Null));
        Assert.Throws<ArgumentException>(() => ContactBuilder().AddField("item", CelType.TypeParameter("A")));
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    public void Provider_IsANamedSourceOfDescriptions()
    {
        CelObjectType deal = DealBuilder().Build();
        CelObjectType contact = ContactBuilder().Build();

        CelTypeProvider provider = CelTypeProvider.Create("crm", deal, contact);

        Assert.Equal("crm", provider.SourceName);
        Assert.Equal(new[] { deal, contact }, provider.ObjectTypes);
        Assert.Empty(provider.EnumTypes);
        Assert.Throws<ArgumentException>(() => CelTypeProvider.Create(string.Empty, deal));
    }

    // FromObject takes an instance of the carrier class only; the value holds the instance itself, not a copy.
    // .NET equality of two pairs compares the instance and the description by reference.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void FromObject_PairsAnInstanceOfTheCarrierClass()
    {
        CelObjectType deal = DealBuilder().Build();
        var carrier = new DealCarrier { Amount = 5 };

        CelValue value = CelValue.FromObject(deal, carrier);

        Assert.Equal(CelValueKind.Object, value.Kind);
        Assert.True(value.TryGetObject(out object? instance));
        Assert.Same(carrier, instance);
        Assert.Equal(value, CelValue.FromObject(deal, carrier));
        Assert.NotEqual(value, CelValue.FromObject(deal, new DealCarrier { Amount = 5 }));
        Assert.NotEqual(value, CelValue.FromObject(DealBuilder().Build(), carrier));
        Assert.False(CelValue.FromInt64(5).TryGetObject(out _));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void FromObjectWithAnInstanceOfAnotherClass_IsArgumentException()
    {
        CelObjectType deal = DealBuilder().Build();
        CelObjectType contact = ContactBuilder().Build();

        Assert.Equal(CelValueKind.Object, CelValue.FromObject(contact, new Dictionary<string, object?>()).Kind);
        Assert.Throws<ArgumentException>(() => CelValue.FromObject(deal, new OtherCarrier()));
        Assert.Throws<ArgumentException>(() => CelValue.FromObject(contact, "text"));
        Assert.Throws<ArgumentException>(() => CelValue.FromObject(contact, new Dictionary<string, string>()));
        Assert.Throws<ArgumentNullException>(() => CelValue.FromObject(deal, null!));
    }

    private static CelObjectTypeBuilder<DealCarrier> DealBuilder() => new CelObjectTypeBuilder<DealCarrier>("acme.crm.Deal")
        .AddField("amount", CelType.Int, carrier => CelValue.FromInt64(carrier.Amount))
        .AddField(
            "title",
            CelType.WrapperOf(CelType.String),
            carrier => carrier.Title is null ? CelValue.Null : CelValue.FromString(carrier.Title));

    private static CelObjectTypeBuilder ContactBuilder() =>
        CelObjectTypeBuilder.ForDictionary("acme.crm.Contact").AddField("name", CelType.String);

    private sealed class DealCarrier
    {
        public long Amount { get; init; }

        public string? Title { get; init; }
    }

    private sealed class OtherCarrier
    {
    }
}
