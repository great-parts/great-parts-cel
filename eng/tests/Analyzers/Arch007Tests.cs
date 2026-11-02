using System.Collections.Generic;
using System.Threading.Tasks;
using Great.Parts.Cel.Eng.Analyzers;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Analyzers;

public sealed class Arch007Tests
{
    [Fact]
    public async Task PublicMemberMarkedRequiresCode_IsReportedForEachAttribute()
    {
        IReadOnlyList<Finding> findings = await AnalyzerRunner.RunAsync(
            new PublicNameAnalyzer(),
            "Great.Parts.Cel",
            [new RepositoryFile("src/Great.Parts.Cel/CelEnvironment.cs", """
                using System.Diagnostics.CodeAnalysis;

                namespace Great.Parts.Cel;

                public sealed class CelEnvironment
                {
                    [RequiresUnreferencedCode("Reads types by reflection.")]
                    [RequiresDynamicCode("Creates generic types at run time.")]
                    public static void Load()
                    {
                    }

                    [RequiresUnreferencedCode("Reads types by reflection.")]
                    [RequiresDynamicCode("Creates generic types at run time.")]
                    internal static void LoadInternal()
                    {
                    }
                }
                """)],
            [new RepositoryFile("docs/api-concepts.md", "# API concepts\n")],
            TestContext.Current.CancellationToken);

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH007", "src/Great.Parts.Cel/CelEnvironment.cs", 9, 24, "public member 'Load' is marked RequiresUnreferencedCode"),
            FindingAssert.Error("ARCH007", "src/Great.Parts.Cel/CelEnvironment.cs", 9, 24, "public member 'Load' is marked RequiresDynamicCode"));
    }
}
