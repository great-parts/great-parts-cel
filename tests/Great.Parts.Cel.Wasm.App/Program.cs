using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Great.Parts.Cel.Wasm.App;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        // The results go to the console of the browser; the application has no component.
        Console.WriteLine($"(x + 2) / 3 for x = 40: {CelSamples.Arithmetic()}");
        Console.WriteLine($"api.name + ' v' + api.version: {CelSamples.Message()}");
        Console.WriteLine($"inc(inc(x)) for x = 1: {await CelSamples.AsynchronousCallAsync().ConfigureAwait(false)}");
        Console.WriteLine($"residual of x + 1 > y with y unknown: {CelSamples.Residual()}");
        Console.WriteLine($"policy greeting for user kim: {CelSamples.Greeting()}");
        Console.WriteLine($"explanation of the policy limits for 42: {CelSamples.Explanation()}");
        Console.WriteLine($"errors with segments: {CelSamples.Located()}");
        Console.WriteLine($"filter title = \"Dune\" AND pages > 100: {CelSamples.Filter()}");
        Console.WriteLine($"simple entry with a class and a list of classes: {CelSamples.Classes()}");
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
