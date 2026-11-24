using System.Net.Http.Json;
using System.Text.Json;
using Great.Parts.Cel.AspNetCore;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders(); // The sample prints only its own lines.

// One CEL environment for the application. Its type context describes CreateOrder, so the fields of the request are variables.
builder.Services.AddCel(cel => cel.AddTypes(OrderTypes.Default));

await using WebApplication app = builder.Build();

// The rule is a text: it could come from configuration or from a database.
app.MapPost("/orders", (CreateOrder order) => Results.Created($"/orders/{order.Id}", order))
    .WithCelValidation<CreateOrder>("items.size() > 0 && total >= 0.0", "The order is empty or the total is negative");

// The sample is its own client, so that it ends by itself: it listens on a free port of the loopback, sends two requests and stops.
// A service calls app.Run() instead.
app.Urls.Clear();
app.Urls.Add("http://127.0.0.1:0");
await app.StartAsync();
using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

using HttpResponseMessage accepted = await client.PostAsJsonAsync("/orders", new { id = "A-1", total = 99.0, items = new[] { "pen" } });
Console.WriteLine((int)accepted.StatusCode); // Output: 201

using HttpResponseMessage rejected = await client.PostAsJsonAsync("/orders", new { id = "A-2", total = 99.0, items = Array.Empty<string>() });
Console.WriteLine((int)rejected.StatusCode); // Output: 400
using JsonDocument problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
Console.WriteLine(problem.RootElement.GetProperty("title").GetString()); // Output: One or more validation errors occurred.
Console.WriteLine(problem.RootElement.GetProperty("errors").GetProperty("")[0].GetString()); // Output: The order is empty or the total is negative

await app.StopAsync();

[CelObject(typeof(CreateOrder))]
internal sealed partial class OrderTypes : CelTypeContext;

public sealed record CreateOrder(string Id, double Total, IReadOnlyList<string> Items);
