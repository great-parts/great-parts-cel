using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Eng.Difftest;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// Step 1 of a run of eng/difftest and its options (docs/design/eng-difftest.md, "A run", "Invocation"). Every test writes its
// manifest into a temporary repository.
public sealed class ManifestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 23, 30, 0, TimeSpan.FromHours(-5));

    [Theory]
    [InlineData("greatPartsCel", """{ "maxDepth": 10 }""")]
    [InlineData("validators", """[ { "name": "cel.validator.comprehension_nesting_limit" } ]""")]
    public static void ManifestWithAMemberThatTheOracleReadsDifferently_IsRefusedWithExitCode2(string member, string value)
    {
        using TempDirectory root = new();
        root.Write(ProfileTables.ManifestPath, $$"""{ "name": "difftest", "{{member}}": {{value}}, "variables": [] }""");
        using StringWriter error = new();

        int exitCode = Program.Run([], root.Path, Now, error);

        Assert.Equal(2, exitCode);
        Assert.Equal(
            $"eng/difftest: the run manifest {ProfileTables.ManifestPath} has '{member}', which the oracle reads differently; a run manifest has neither 'greatPartsCel' nor 'validators'" + Environment.NewLine,
            error.ToString());
    }

    [Theory]
    [InlineData(null, "does not exist")]
    [InlineData("[]", "is not a JSON object")]
    [InlineData("{ \"name\": ", "is not valid JSON")]
    public static void MissingOrMalformedManifest_IsRefusedWithExitCode2(string? text, string problem)
    {
        using TempDirectory root = new();
        if (text is not null)
        {
            root.Write(ProfileTables.ManifestPath, text);
        }

        using StringWriter error = new();

        int exitCode = Program.Run([], root.Path, Now, error);

        Assert.Equal(2, exitCode);
        Assert.StartsWith($"eng/difftest: the run manifest {ProfileTables.ManifestPath} {problem}", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestOfTheRepository_IsAcceptedAndDeclaresTheVariablesOfTheProfile()
    {
        string root = RepositoryRoot.Find();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ProfileTables.ManifestPath)));

        Assert.True(RunManifest.TryRead(root, out string problem), problem);
        Assert.Equal(
            ProfileTables.Variables.Select(variable => variable.Name + ": " + variable.TypeRecord),
            manifest.RootElement.GetProperty("variables").EnumerateArray()
                .Select(variable => variable.GetProperty("name").GetString() + ": "
                    + (variable.TryGetProperty("type", out JsonElement record) ? record : variable.GetProperty("type_name")).GetString()));
        Assert.Equal(ProfileTables.Container, manifest.RootElement.GetProperty("container").GetString());
    }

    // The manifest holds the keys that both sides read — the library with Apply, the oracle with cel.FromConfig — and no other.
    [Fact]
    public void ManifestOfTheRepository_HoldsOnlyTheKeysOfTheProfile()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath)));

        Assert.Equal(
            ["container", "description", "extensions", "name", "variables"],
            manifest.RootElement.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(manifest.RootElement.EnumerateObject(), member => RunManifest.RefusedMembers.Contains(member.Name));
    }

    // The manifest names the eight sets of the profile with their versions; the library reads the names and the versions with the
    // catalog, the oracle with cel.FromConfig, so the two sides cannot hold different sets.
    [Fact]
    public void ManifestOfTheRepository_NamesTheSetsOfTheProfileWithTheVersionsOfTheCatalog()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath)));
        List<string> declared =
        [
            .. manifest.RootElement.GetProperty("extensions").EnumerateArray()
                .Select(extension => extension.GetProperty("name").GetString() + "@" + extension.GetProperty("version").GetString()),
        ];
        CelExtension[] catalog =
        [
            CelExtensions.Strings(5), CelExtensions.Math(3), CelExtensions.Lists(4), CelExtensions.Sets(0), CelExtensions.Encoders(1),
            CelExtensions.Bindings(1), CelExtensions.TwoVarComprehensions(0), CelExtensions.Network(1),
        ];

        Assert.Equal(ProfileTables.Extensions.Select(set => set.Name + "@" + set.Version), declared);
        Assert.Equal(catalog.Select(set => set.Name + "@" + set.Version.ToString(CultureInfo.InvariantCulture)), declared);
        Assert.DoesNotContain(declared, set => set.StartsWith("decimal@", StringComparison.Ordinal) || set.StartsWith("block@", StringComparison.Ordinal));
    }

    // The library side reads the very file that the oracle reads: its bytes go to Apply, its path to the oracle.
    [Fact]
    public void ManifestOfTheRepository_BuildsTheEnvironmentOfTheLibrarySide()
    {
        byte[] manifest = File.ReadAllBytes(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath));

        string zoneInfoFile = Path.Combine(RepositoryRoot.Find(), ProfileTables.ZoneInfoPath);

        Assert.True(LibrarySide.TryBuild(manifest, zoneInfoFile, out LibrarySide? side, out string problem), problem);
        Assert.NotNull(side);
        Assert.Equal(string.Empty, problem);
    }

    [Fact]
    public void Options_AreParsedAndTheSeedDefaultsToTheDateInUtc()
    {
        Assert.True(Program.TryParse(["--seed", "7", "--budget", "2h", "--case", "1207", "--numbers-only"], Now, out RunOptions? options, out _));
        Assert.Equal(new RunOptions(7, TimeSpan.FromHours(2), 1207, NumbersOnly: true), options);

        Assert.True(Program.TryParse([], Now, out RunOptions? defaults, out _));
        Assert.Equal(new RunOptions(20261105, Program.DefaultBudget, null, NumbersOnly: false), defaults);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--seed")]
    [InlineData("--seed -1")]
    [InlineData("--case x")]
    [InlineData("--budget 5")]
    [InlineData("--budget 0m")]
    [InlineData("--budget 5d")]
    public static void InvalidCommandLine_ExitCodeIs2BeforeTheManifestIsRead(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        string[] args = commandLine.Split(' ');
        using TempDirectory root = new();
        using StringWriter error = new();

        int exitCode = Program.Run(args, root.Path, Now, error);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("eng/difftest: ", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("manifest", error.ToString(), StringComparison.Ordinal);
    }
}
