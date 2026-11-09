# Time zone database

An expression can read the local time of a moment in a named zone: `timestamp.getHours('Europe/Moscow')`. To find the offset of `Europe/Moscow` at that moment the environment needs the time zone database, and the library has none of its own: it reads no file, nothing of the operating system, no variable of the environment, and it does not use `TimeZoneInfo`, whose data and names differ between machines. The application supplies the data, and the same expression then gives the same result on every machine.

## What the library accepts

- The names `UTC` and fixed offsets `[+-]HH:MM` need no database.
- Every other name is an identifier of the database, compared exactly, with the case. A name that the database does not have, and any such name when the environment has no database, is the evaluation error `TimeZoneNotFound` (3022). The error occurs when the expression is evaluated, also for a name written in the text.
- The data is a set of TZif files (RFC 9636), version 2 or later, one per identifier. A link such as `US/Central` is a pair of its own with the bytes of its target.
- `CelTimeZoneDatabase.Create(zones, version)` reads and checks every file at once, so a file that is not valid is reported there and not in an evaluation, as `ArgumentException`. It refuses a file with leap second records, an empty identifier, an identifier with `:` and one identifier with two different files. The `version`, such as `2026c`, is only a text for messages: `TimeZoneNotFound` shows it.
- `CelEnvironmentBuilder.UseTimeZones(database)` adds the database to an environment. The same object added again changes nothing; another database is the build error `TimeZoneDatabaseConflict` (4012), since a database is never replaced silently. Two databases made from the same files are two objects. `CelEnvironment.ToBuilder` keeps the database. An [environment description in JSON](../environment-json.md) does not carry it: add it in code.
- The database is immutable and is shared by every environment and every thread. Make it once, when the application starts.

## Where the files come from

- On Linux and macOS, the zoneinfo directory of the system. Its files change with the updates of the system, so the offsets of a future moment can change between two runs of the application.
- On Windows, and for results that must not change, the files of a release of the IANA database that the application ships and pins, for example the directory of a `tzdata` package or the archive `zoneinfo.zip` of a Go release. The offsets of the zones are then those of that release, whatever the machine.

## From memory

A database from the bytes of one file, here the zone `Asia/Kathmandu` with the fixed offset +05:45:

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Values;

// A TZif file of the zone Asia/Kathmandu, +05:45; an application reads such files from the data it ships.
byte[] kathmandu = Convert.FromBase64String(
    "VFppZjIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAVFppZjIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAFDcAAAACjwrMDU0NT4tNTo0NQo=");
CelTimeZoneDatabase zones = CelTimeZoneDatabase.Create([KeyValuePair.Create("Asia/Kathmandu", (ReadOnlyMemory<byte>)kathmandu)], "2026c");

CelEnvironment env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .UseTimeZones(zones)
    .Build()
    .GetEnvironmentOrThrow();

CelBindings none = new();
Console.WriteLine(env.Evaluate<long>("timestamp('2023-01-01T00:00:00Z').getHours('Asia/Kathmandu')", none).GetValueOrThrow()); // Output: 5
Console.WriteLine(env.Evaluate<long>("timestamp('2023-01-01T00:00:00Z').getMinutes('Asia/Kathmandu')", none).GetValueOrThrow()); // Output: 45
Console.WriteLine(env.Evaluate<long>("timestamp(0).getHours('UTC')", none).GetValueOrThrow()); // Output: 0

CelEvalResult<long> missing = env.Evaluate<long>("timestamp(0).getHours('Mars/Base')", none);
Console.WriteLine(missing.Errors[0].Code); // Output: TimeZoneNotFound
```

## From a directory

A directory of TZif files, one file per identifier, as the zoneinfo directory of a system or an unpacked `tzdata` package. The program writes such a directory itself, so that it runs anywhere; the function `LoadZones` is the part to copy:

```csharp
using Great.Parts.Cel.Values;

string directory = Path.Combine(Path.GetTempPath(), "zoneinfo-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(directory, "Asia"));
File.WriteAllBytes(
    Path.Combine(directory, "Asia", "Kathmandu"),
    Convert.FromBase64String(
        "VFppZjIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAVFppZjIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAFDcAAAACjwrMDU0NT4tNTo0NQo="));
File.WriteAllText(Path.Combine(directory, "zone.tab"), "not a zone file");
try
{
    CelTimeZoneDatabase zones = LoadZones(directory, "2026c");
    Console.WriteLine(string.Join(", ", zones.ZoneIds)); // Output: Asia/Kathmandu
    Console.WriteLine(zones.Version); // Output: 2026c
    Console.WriteLine(zones.Contains("Asia/Kathmandu")); // Output: True
}
finally
{
    Directory.Delete(directory, recursive: true);
}

static CelTimeZoneDatabase LoadZones(string root, string version)
{
    List<KeyValuePair<string, ReadOnlyMemory<byte>>> pairs = [];
    foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
    {
        byte[] data = File.ReadAllBytes(file);

        // A zoneinfo directory also holds files that are no zones, such as zone.tab and tzdata.zi: only a TZif file is a zone.
        if (data.AsSpan().StartsWith("TZif"u8))
        {
            string identifier = Path.GetRelativePath(root, file).Replace('\\', '/');
            pairs.Add(KeyValuePair.Create(identifier, (ReadOnlyMemory<byte>)data));
        }
    }

    return CelTimeZoneDatabase.Create(pairs, version);
}
```

The zoneinfo directory of a system can also have a directory `right`, whose files have leap second records, which `Create` refuses: leave it out.

## Resolving a name

The page [Standard library](../standard-library.md#time-zone-names) has the rules for a name, and the page [Error codes](../error-codes.md) has the codes `TimeZoneNotFound`, `InvalidTimeZone` (3023) and `TimeZoneDatabaseConflict`.
