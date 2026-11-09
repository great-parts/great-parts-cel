using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The time zone data file of the repository (<see cref="ProfileTables.ZoneInfoPath"/>): the names of its zones, which the
/// generator uses for zone arguments, and the entries that the library side turns into its database. Every entry of the
/// archive that is not a directory is a zone, in the order of the archive.
/// </summary>
internal static class ZoneData
{
    private static readonly Lazy<IReadOnlyList<string>> Located = new(() => Names(Locate()));

    /// <summary>
    /// The names of the zones of the file of the repository that holds the running program: the nearest parent directory of the
    /// program or of the working directory that has the file.
    /// </summary>
    internal static IReadOnlyList<string> Zones => Located.Value;

    /// <summary>The names of the zones of a time zone data file, in the order of the archive.</summary>
    /// <param name="file">The path of the archive.</param>
    internal static IReadOnlyList<string> Names(string file)
    {
        using ZipArchive archive = ZipFile.OpenRead(file);
        return [.. archive.Entries.Select(entry => entry.FullName).Where(name => !name.EndsWith('/'))];
    }

    /// <summary>The zones of a time zone data file with their TZif bytes, in the order of the archive.</summary>
    /// <param name="file">The path of the archive.</param>
    internal static IReadOnlyList<KeyValuePair<string, byte[]>> Entries(string file)
    {
        using ZipArchive archive = ZipFile.OpenRead(file);
        List<KeyValuePair<string, byte[]>> entries = [];
        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => !entry.FullName.EndsWith('/')))
        {
            using Stream source = entry.Open();
            using MemoryStream bytes = new();
            source.CopyTo(bytes);
            entries.Add(new KeyValuePair<string, byte[]>(entry.FullName, bytes.ToArray()));
        }

        return entries;
    }

    private static string Locate()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, ProfileTables.ZoneInfoPath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new IOException($"{ProfileTables.ZoneInfoPath} is not in a parent directory of {AppContext.BaseDirectory} or of the working directory");
    }
}
