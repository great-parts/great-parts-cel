using System.IO;
using System.Text.Json;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The run manifest <c>Profile/environment.json</c> (docs/design/configuration.md, "Run manifest of the differential run").
/// Both sides read the same file; a manifest with <c>greatPartsCel</c> or <c>validators</c> is refused before the library
/// reads it, because the oracle would read it differently.
/// </summary>
internal static class RunManifest
{
    /// <summary>The members that a run manifest never has.</summary>
    internal static readonly string[] RefusedMembers = ["greatPartsCel", "validators"];

    /// <summary>Reads the manifest of a repository and checks that a run may use it.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="problem">Why the manifest is refused, one line; empty when it is not.</param>
    /// <returns><see langword="true"/> when the run may use the manifest.</returns>
    internal static bool TryRead(string root, out string problem)
    {
        string path = Path.Combine(root, ProfileTables.ManifestPath);
        if (!File.Exists(path))
        {
            problem = $"the run manifest {ProfileTables.ManifestPath} does not exist";
            return false;
        }

        return TryCheck(File.ReadAllText(path), out problem);
    }

    /// <summary>Checks the text of a manifest.</summary>
    /// <param name="json">The text of the manifest.</param>
    /// <param name="problem">Why the manifest is refused, one line; empty when it is not.</param>
    /// <returns><see langword="true"/> when the run may use the manifest.</returns>
    internal static bool TryCheck(string json, out string problem)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problem = $"the run manifest {ProfileTables.ManifestPath} is not a JSON object";
                return false;
            }

            foreach (string member in RefusedMembers)
            {
                if (document.RootElement.TryGetProperty(member, out _))
                {
                    problem = $"the run manifest {ProfileTables.ManifestPath} has '{member}', which the oracle reads differently; a run manifest has neither 'greatPartsCel' nor 'validators'";
                    return false;
                }
            }
        }
        catch (JsonException exception)
        {
            problem = $"the run manifest {ProfileTables.ManifestPath} is not valid JSON: {exception.Message.Split('\n')[0].TrimEnd('\r')}";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}
