using Great.Parts.Cel;

CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddVariable<string>("country")
    .AddVariable<double>("total"));

// The cache compiles a text once and keeps the program, so a rule that comes from a database costs one compilation, not one per row.
CelCompilationCache cache = new(environment);

string[] rules = ["total > 100.0", "country == 'DE' && total > 100.0"];
(string Country, double Total)[] rows = [("DE", 250.0), ("FR", 80.0), ("DE", 40.0), ("FR", 900.0)];

// One CelBindings serves the whole loop: Clear removes the values of the previous row.
CelBindings bindings = new();
foreach (string rule in rules)
{
    int matches = 0;
    foreach ((string country, double total) in rows)
    {
        bindings.Clear();
        bindings.Add("country", country).Add("total", total);
        if (cache.Evaluate<bool>(rule, bindings).GetValueOrThrow())
        {
            matches++;
        }
    }

    Console.WriteLine($"{rule}: {matches} of {rows.Length}");
}

// Each text is one entry. When a rule changes in the database, remove its text; the next call compiles the new one.
Console.WriteLine($"entries: {cache.Count}");
Console.WriteLine($"removed: {cache.TryRemove(rules[0])}");
Console.WriteLine($"entries: {cache.Count}");
