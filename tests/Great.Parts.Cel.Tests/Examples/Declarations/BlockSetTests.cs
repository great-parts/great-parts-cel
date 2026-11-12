using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The internal set block (the page of the extension sets, "block", and the decision on the internal set of the runner): it has no
// member of CelExtensions, so the public interface of the catalog has no way to add it; the runner gets it as the internal entry
// of the library (that no member of CelExtensions is named for it is the check of the snapshot of the public API, and no example
// repeats it). The set depends on bindings 1, because its macro cel.block expands to cel.@block, a function of bindings: an
// environment that has block and no bindings is MissingExtensionDependency (4005) at Build, with the text "extension block v1
// requires extension 'bindings', which is not added to the environment", in either order of the calls, and an environment that
// has both builds. The dependency is never added by itself. A build error has the stage Build and no place in an expression.
[Trait("Requirement", "F-3")]
[Trait("Requirement", "+R-8")]
[Trait("Requirement", "F-8")]
public sealed class BlockSetTests
{
    [Fact]
    public void SetWithoutBindings_IsMissingExtensionDependency()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(BlockExample.Set()).Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.MissingExtensionDependency, CelErrorStage.Build, "extension block v1 requires extension 'bindings', which is not added to the environment"),
            (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
        Assert.Null(error.SourceLocation);
    }

    // The set with the set optional and without bindings is the same one error: the other sets do not stand for the dependency.
    [Fact]
    public void OtherSets_DoNotStandForBindings()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(BlockExample.Set())
            .AddExtension(CelExtensions.Optional(2))
            .AddExtension(CelExtensions.Lists(4))
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.MissingExtensionDependency, CelErrorStage.Build), (error.Code, error.Stage));
    }

    // The dependency is met by bindings in either order of the calls.
    [Fact]
    public void SetWithBindings_Builds_InEitherOrderOfTheCalls()
    {
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Bindings(1)).AddExtension(BlockExample.Set()).Build().IsSuccess);
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(BlockExample.Set()).AddExtension(CelExtensions.Bindings(1)).Build().IsSuccess);
        Assert.True(BlockExample.Builder().Build().IsSuccess);
    }

    // The set of the runner brings only the macros and the variables @index0 to @index29: the other sets build with it as
    // they do without it.
    [Fact]
    public void SetOfTheRunner_BuildsWithTheOtherSets()
    {
        CelEnvironmentBuilder builder = BlockExample.Builder()
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(CelExtensions.TwoVarComprehensions(0));

        Assert.True(builder.Build().IsSuccess);
    }

    // The same set again is one set, and so is bindings added again.
    [Fact]
    public void SameSetAddedAgain_IsOneSet()
    {
        Assert.True(BlockExample.Builder().AddExtension(BlockExample.Set()).AddExtension(CelExtensions.Bindings(1)).Build().IsSuccess);
    }
}
