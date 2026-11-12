using System.Text.Json.Serialization;
using Google.Protobuf;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Naming;

// The field names of the type mapping ("Names of fields"), one rule per member. The same class under the four policies and under
// IgnoreJsonAttributes, so the names that each gives can be told apart.

public sealed class Names
{
    // Step 1: [CelName] gives the name, [CelIgnore] hides the member.
    [CelName("renamed")]
    public long Original { get; set; }

    [CelIgnore]
    public long Hidden { get; set; }

    // Step 1 beats step 2: [CelName] and [JsonPropertyName] on one member.
    [CelName("fromCel")]
    [JsonPropertyName("fromJson")]
    public long Both { get; set; }

    // Step 2: [JsonPropertyName] gives the name; [JsonIgnore] hides the member; a condition other than Always does not.
    [JsonPropertyName("wire_name")]
    public long Wire { get; set; }

    [JsonIgnore]
    public long Skipped { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Conditional { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public long AlwaysSkipped { get; set; }

    // Step 3: the policy. The names are those of JsonNamingPolicy.CamelCase and JsonNamingPolicy.SnakeCaseLower.
    public long TotalAmount { get; set; }

    public long URLPath { get; set; }

    public long HTTPServer { get; set; }

    public long Order2Line { get; set; }

    public long ID { get; set; }

    // A word reserved by CEL is a field name; an expression writes it in backquotes.
    public long In { get; set; }

    public long As { get; set; }
}

[CelObject(typeof(Names), Name = "samples.CamelNames")]
public sealed partial class CamelContext : CelTypeContext
{
}

[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.AsIs)]
[CelObject(typeof(Names), Name = "samples.AsIsNames")]
public sealed partial class AsIsContext : CelTypeContext
{
}

[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.SnakeCaseLower)]
[CelObject(typeof(Names), Name = "samples.SnakeNames")]
public sealed partial class SnakeContext : CelTypeContext
{
}

// With IgnoreJsonAttributes the attributes of System.Text.Json do not count: Wire and Skipped are read under their own
// names (the policy applies), and the hidden-by-JSON members are fields.
[CelTypeContextOptions(IgnoreJsonAttributes = true)]
[CelObject(typeof(Names), Name = "samples.PlainNames")]
public sealed partial class PlainContext : CelTypeContext
{
}

// IgnoreMembers of a root hides members of that root type by their C# names, also for a type that cannot be marked.
[CelObject(typeof(ByteString), Name = "samples.Buffer", IgnoreMembers = new[] { "Span", "IsEmpty" })]
public sealed partial class ForeignContext : CelTypeContext
{
}
