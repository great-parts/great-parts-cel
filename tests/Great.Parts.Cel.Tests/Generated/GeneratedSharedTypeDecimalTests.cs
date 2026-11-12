#if NET11_0_OR_GREATER
using System.Linq;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Generated;

// The set decimal gives the type index of the environment a flag, which is a copy of the index. The copy keeps the types that two
// contexts share as one declaration: a pair made with the description that was dropped still fits the declared list of the type that stays.
public sealed class GeneratedSharedTypeDecimalTests
{
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void PairOfTheDroppedDescription_FitsTheDeclaredListWhenTheSetDecimalIsAdded()
    {
        CelEnvironment environment = Builder(ShopContext.Default, BillingContext.Default)
            .AddExtension(CelExtensions.Decimal(1))
            .AddVariable("contacts", CelType.ListOf(CelType.ObjectNamed("shop.Contact")))
            .Build()
            .GetEnvironmentOrThrow();
        CelObjectType shop = ShopContext.Default.ObjectTypes.Single(type => type.Name == "shop.Contact");
        CelValue ofShop = CelValue.FromObject(shop, new VipContact { Name = "Ann", Level = 1 });
        CelBindings bindings = new CelBindings().Add("contacts", CelList.Create(ofShop));

        Assert.True(Evaluate<bool>(environment, "contacts[0].name == 'Ann' && type(contacts[0]) == shop.Contact", bindings));
    }
}
#endif
