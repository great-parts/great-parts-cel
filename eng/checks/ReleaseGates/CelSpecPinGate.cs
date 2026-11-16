using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG905</c>: the pinned commit of cel-spec includes every commit of master that is older than 6 weeks on the day of the release.
/// The history of master is a bare clone without file contents in a temporary directory; the gate fails when
/// <c>git rev-list &lt;pinned&gt;..master --before=&lt;date minus 42 days, 00:00 UTC&gt;</c> is not empty. A pinned commit that is not in the
/// history of master fails it too.
/// </summary>
internal static class CelSpecPinGate
{
    private const string PinPath = "tests/cel-spec/COMMIT";
    private const int Weeks = 6;

    /// <summary>Checks the pin.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="date">The date of the release.</param>
    /// <param name="celSpec">The address or the path of the repository of cel-spec.</param>
    internal static IEnumerable<Finding> Check(string root, DateOnly date, string celSpec)
    {
        string pinned = ReadPin(root);
        using ScratchDirectory scratch = new("gpc-cel-spec-");
        GitResult clone = Git.Run(scratch.Path, "clone", "--bare", "--quiet", "--filter=blob:none", "--single-branch", "--branch", "master", "--", celSpec, scratch.Path);
        if (!clone.Succeeded)
        {
            throw new CheckInputException($"the repository of cel-spec {celSpec} cannot be cloned: {FirstLine(clone.Error)}");
        }

        string shortPin = pinned[..8];
        GitResult ancestor = Git.Run(scratch.Path, "merge-base", "--is-ancestor", pinned, "master");
        if (ancestor.ExitCode != 0)
        {
            return [PinFinding($"pinned cel-spec commit {shortPin} is not in the history of master")];
        }

        string before = date.AddDays(-Weeks * 7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00 +0000";
        GitResult missing = Git.Run(scratch.Path, "log", "--max-count=1", "--format=%H %ct", "--before=" + before, pinned + "..master");
        if (!missing.Succeeded)
        {
            throw new CheckInputException($"the history of cel-spec cannot be read: {FirstLine(missing.Error)}");
        }

        string[] newest = missing.Output.Trim().Split(' ');
        if (newest.Length != 2)
        {
            return [];
        }

        string day = DateTimeOffset.FromUnixTimeSeconds(long.Parse(newest[1], CultureInfo.InvariantCulture)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return [PinFinding($"pinned cel-spec commit {shortPin} does not include master commit {newest[0][..8]} of {day}, older than {Weeks.ToString(CultureInfo.InvariantCulture)} weeks")];
    }

    private static Finding PinFinding(string message) => new(PinPath, 1, 1, FindingSeverity.Error, EngCodes.Eng905, message);

    private static string ReadPin(string root)
    {
        string path = Path.Combine(root, PinPath);
        if (!File.Exists(path))
        {
            throw new CheckInputException($"{PinPath} does not exist");
        }

        string pinned = File.ReadAllText(path, Encoding.UTF8).Trim();
        return pinned.Length == 40 && IsHex(pinned)
            ? pinned
            : throw new CheckInputException($"{PinPath} does not hold a full commit hash of 40 hexadecimal digits");
    }

    private static bool IsHex(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static string FirstLine(string text)
    {
        string trimmed = text.Trim();
        int end = trimmed.IndexOf('\n', StringComparison.Ordinal);
        return end < 0 ? trimmed : trimmed[..end].TrimEnd();
    }
}
