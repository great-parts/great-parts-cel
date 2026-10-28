using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The preprocessor symbols that the .NET SDK defines for a target framework.</summary>
internal static class SdkSymbols
{
    private const string OrGreater = "_OR_GREATER";

    private static readonly string[] NetStandardVersions = ["1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "2.0", "2.1"];
    private static readonly string[] NetCoreAppVersions = ["1.0", "1.1", "2.0", "2.1", "2.2", "3.0", "3.1"];
    private static readonly string[] NetFrameworkVersions =
        ["2.0", "3.5", "4.0", "4.5", "4.5.1", "4.5.2", "4.6", "4.6.1", "4.6.2", "4.7", "4.7.1", "4.7.2", "4.8", "4.8.1"];

    /// <summary>Returns the symbols for a target framework moniker such as <c>net11.0</c> or <c>netstandard2.0</c>.</summary>
    /// <param name="targetFramework">The moniker. A platform suffix, as in <c>net10.0-windows</c>, is ignored.</param>
    /// <returns>The symbols; none for a moniker of an unknown form.</returns>
    internal static IReadOnlyList<string> For(string targetFramework)
    {
        string moniker = targetFramework.Trim();
        int dash = moniker.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            moniker = moniker[..dash];
        }

        List<string> symbols = [];
        if (TryVersion(moniker, "netstandard", out Version? version))
        {
            symbols.Add("NETSTANDARD");
            AddVersions(symbols, "NETSTANDARD", "_", version, NetStandardVersions, includeExact: true);
        }
        else if (TryVersion(moniker, "netcoreapp", out version))
        {
            symbols.Add("NETCOREAPP");
            AddVersions(symbols, "NETCOREAPP", "_", version, NetCoreAppVersions, includeExact: true);
        }
        else if (TryVersion(moniker, "net", out version) && version.Major >= 5)
        {
            AddNet5Symbols(symbols, version);
        }
        else if (TryFrameworkVersion(moniker, out version))
        {
            symbols.Add("NETFRAMEWORK");
            AddVersions(symbols, "NET", string.Empty, version, NetFrameworkVersions, includeExact: true);
        }

        return symbols;
    }

    private static void AddNet5Symbols(List<string> symbols, Version version)
    {
        symbols.Add("NET");
        symbols.Add("NETCOREAPP");
        symbols.Add($"NET{version.Major}_{version.Minor}");
        for (int major = 5; major <= version.Major; major++)
        {
            symbols.Add($"NET{major}_0{OrGreater}");
        }

        for (int minor = 1; minor <= version.Minor; minor++)
        {
            symbols.Add($"NET{version.Major}_{minor}{OrGreater}");
        }

        // The SDK keeps the symbols of the older frameworks of the same line.
        AddVersions(symbols, "NETCOREAPP", "_", new Version(3, 1), NetCoreAppVersions, includeExact: false);
    }

    private static void AddVersions(List<string> symbols, string prefix, string separator, Version version, string[] known, bool includeExact)
    {
        if (includeExact)
        {
            symbols.Add(prefix + Format(version, separator));
        }

        foreach (string candidate in known)
        {
            Version knownVersion = Version.Parse(candidate);
            if (knownVersion.CompareTo(version) <= 0)
            {
                symbols.Add(prefix + Format(knownVersion, separator) + OrGreater);
            }
        }
    }

    private static string Format(Version version, string separator) => version.ToString().Replace(".", separator, StringComparison.Ordinal);

    private static bool TryVersion(string moniker, string prefix, [NotNullWhen(true)] out Version? version)
    {
        version = null;
        return moniker.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Version.TryParse(moniker[prefix.Length..], out version);
    }

    // net48 is .NET Framework 4.8: the digits are the version without dots, net20 is 2.0.
    private static bool TryFrameworkVersion(string moniker, [NotNullWhen(true)] out Version? version)
    {
        version = null;
        if (!moniker.StartsWith("net", StringComparison.OrdinalIgnoreCase) || moniker.Length < 4)
        {
            return false;
        }

        string digits = moniker[3..];
        foreach (char digit in digits)
        {
            if (!char.IsAsciiDigit(digit))
            {
                return false;
            }
        }

        string dotted = digits.Length == 1 ? digits + ".0" : string.Join('.', digits.ToCharArray());
        return Version.TryParse(dotted, out version);
    }
}
