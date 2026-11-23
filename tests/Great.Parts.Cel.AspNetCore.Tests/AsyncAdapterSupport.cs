using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Generator.Samples.Orders;

namespace Great.Parts.Cel.AspNetCore.Tests;

// The environment of the tests of the adapters with a function that waits: the types of the shop, isMember(role, group), positive(total)
// and check(city). The first two wait for a task that is not complete, the last completes at once.
internal static class AsyncAdapterSupport
{
    internal static void Configure(CelEnvironmentBuilder environment) => environment
        .AddTypes(ShopContext.Default)
        .AddAsyncFunction("isMember", async (string role, string group, CancellationToken token) =>
        {
            await Task.Yield();
            return role == "manager" && group == "managers";
        })
        .AddAsyncFunction("positive", async (double total, CancellationToken token) =>
        {
            await Task.Yield();
            return total > 0.0;
        })
        .AddAsyncFunction("check", (string city, CancellationToken token) => ValueTask.FromResult(city == "Paris"));
}

// The functions of the tests of the token of a request: hang(text) waits for the cancellation of the token it received, capture(text)
// completes at once, and both tell the test what token they received.
internal sealed class TokenProbe
{
    private readonly TaskCompletionSource<CancellationToken> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Completes with the token of the first call of hang or capture.
    internal Task<CancellationToken> Received => _received.Task;

    internal void Configure(CelEnvironmentBuilder environment)
    {
        AsyncAdapterSupport.Configure(environment);
        _ = environment
            .AddAsyncFunction("hang", async (string text, CancellationToken token) =>
            {
                _ = _received.TrySetResult(token);
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return text.Length > 0;
            })
            .AddAsyncFunction("capture", (string text, CancellationToken token) =>
            {
                _ = _received.TrySetResult(token);
                return ValueTask.FromResult(text.Length > 0);
            });
    }
}
