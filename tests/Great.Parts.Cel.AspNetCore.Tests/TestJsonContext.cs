using System.Text.Json.Serialization;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.AspNetCore.Http;

namespace Great.Parts.Cel.AspNetCore.Tests;

// The JSON of the test application without reflection: the request body of the endpoints, the text they answer and the validation problem they answer.
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
