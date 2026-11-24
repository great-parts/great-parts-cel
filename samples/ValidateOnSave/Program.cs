using Great.Parts.Cel;
using Great.Parts.Cel.Diagnostics;

// The environment of the rules that users write: two variables and the standard library.
CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddVariable<double>("total")
    .AddVariable<string>("country"));

string[] rules =
[
    "total > 100.0 && country == 'DE'",
    "total > 100.0 && contry == 'DE'",
    "country in ['DE', 'FR']",
];

// What the application does when a user saves a rule: it validates the text and does not run it.
foreach (string rule in rules)
{
    CelValidationResult check = environment.Validate<bool>(rule);
    if (check.IsValid)
    {
        Console.WriteLine($"saved, reads: {string.Join(", ", check.ReferencedVariables)}");
        continue;
    }

    foreach (CelError error in check.Errors)
    {
        Console.WriteLine(error.ToDisplayString(rule));
    }
}
