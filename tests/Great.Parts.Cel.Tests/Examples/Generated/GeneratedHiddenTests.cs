using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Special;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// A hidden member — [CelIgnore] — has no warning and is not a field at all: selecting it is NoSuchField, as for a name that no member
// has, and not FieldNotAccessible. The rest of the type is read. The members that the generator excludes with GPCEL002 (FieldNotAccessible,
// 2007, with the reason) are read in the generator tests, in the compilation in memory, because a sample type with such a member would
// not build with warnings as errors.
public sealed class GeneratedHiddenTests
{
    private static readonly CelEnvironment Environment = Builder(HidingContext.Default)
        .AddVariable<Hiding>("h")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    public void HiddenMember_IsNoSuchField()
    {
        var hiding = new Hiding { Visible = 5 };
        CelBindings bindings = new CelBindings().Add("h", hiding);

        Assert.Equal(["visible"], HidingContext.Default.ObjectTypes.Single().Fields.Select(field => field.Name));
        Assert.True(Evaluate<bool>(Environment, "h.visible == 5", bindings));
        AssertError(CompileError(Environment, "h.hiddenStream"), CelErrorCode.NoSuchField, 1);
        AssertError(CompileError(Environment, "h.missing"), CelErrorCode.NoSuchField, 1);
    }
}
