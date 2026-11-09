using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// The time zone data of the tests: the archive <c>zoneinfo.zip</c> of the pinned Go, embedded as a resource and read once per
/// process. The file is compiled into every test project as a linked source, so the library tests, the suite runner and the
/// differential run read the same bytes and no test reads a file.
/// </summary>
internal static class ZoneInfoFile
{
    /// <summary>The name of the embedded archive: its path from the root of the repository.</summary>
    internal const string ResourceName = "tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip";

    /// <summary>The release of the IANA time zone database in the archive.</summary>
    internal const string Version = "2026c";

    private static readonly Lazy<IReadOnlyList<KeyValuePair<string, byte[]>>> Archive = new(ReadArchive);

    private static readonly Lazy<CelTimeZoneDatabase> Zones = new(CreateDatabase);

    /// <summary>
    /// The time zone database of the whole archive, with the version of its release.
    /// </summary>
    /// <returns>The database; the same object on every call.</returns>
    internal static CelTimeZoneDatabase Database() => Zones.Value;

    /// <summary>
    /// The entries of the archive that are not directories, in the order of the archive: the entry name and its TZif bytes.
    /// </summary>
    /// <returns>The pairs; the same list on every call, so a test must not change the arrays.</returns>
    internal static IReadOnlyList<KeyValuePair<string, byte[]>> Entries() => Archive.Value;

    private static CelTimeZoneDatabase CreateDatabase()
    {
        IReadOnlyList<KeyValuePair<string, byte[]>> entries = Archive.Value;
        List<KeyValuePair<string, ReadOnlyMemory<byte>>> pairs = new(entries.Count);
        foreach (KeyValuePair<string, byte[]> entry in entries)
        {
            pairs.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(entry.Key, entry.Value));
        }

        return CelTimeZoneDatabase.Create(pairs, Version);
    }

    private static List<KeyValuePair<string, byte[]>> ReadArchive()
    {
        Assembly assembly = typeof(ZoneInfoFile).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"the embedded resource '{ResourceName}' is missing");
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        List<KeyValuePair<string, byte[]>> entries = new(archive.Entries.Count);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            // A directory entry has a name that ends with a slash and no data.
            if (entry.FullName.Length == 0 || entry.FullName[^1] == '/')
            {
                continue;
            }

            using Stream source = entry.Open();
            using MemoryStream bytes = new((int)entry.Length);
            source.CopyTo(bytes);
            entries.Add(new KeyValuePair<string, byte[]>(entry.FullName, bytes.ToArray()));
        }

        return entries;
    }
}
