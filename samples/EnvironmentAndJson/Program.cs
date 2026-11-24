using System.Text;
using System.Text.Json;
using Great.Parts.Cel;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;

// The environment says which names an expression may use. Here a part of it comes from JSON text, as it could from a file.
const string description = """
    {
      "extensions": [ { "name": "strings", "version": "5" } ],
      "variables": [ { "name": "limit", "type": "double" } ]
    }
    """;

CelEnvironment environment = CelEnvironment.Create(builder =>
{
    var errors = CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(description));
    if (errors.Length > 0)
    {
        throw new CelException(errors);
    }

    // The rest is declared in code: a JSON document is one variable of any shape.
    builder.AddVariable<JsonElement>("payload");
});

// Compile once; the program is immutable and evaluates any number of times.
CelProgram<bool> program = environment.Compile<bool>(
    "payload.total > limit && payload.items.exists(i, i.sku == 'A-1') && payload.customer.upperAscii() == 'ACME'")
    .GetProgramOrThrow();

using JsonDocument document = JsonDocument.Parse("""
    { "customer": "acme", "total": 250.5, "items": [ { "sku": "A-1" }, { "sku": "B-2" } ] }
    """);
CelBindings bindings = new CelBindings().Add("limit", 100.0).Add("payload", document.RootElement);
Console.WriteLine(program.Evaluate(bindings).GetValueOrThrow()); // Output: True
