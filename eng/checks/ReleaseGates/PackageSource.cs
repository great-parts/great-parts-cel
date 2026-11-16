using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The answer of nuget.org to a request for a package.</summary>
/// <param name="Found">Whether the service gave the package.</param>
/// <param name="Content">The bytes of the package as the service gave them; empty when it did not give one.</param>
/// <param name="Reason">When it did not: the status or the failure, in a few words.</param>
internal sealed record PackageAnswer(bool Found, byte[] Content, string Reason);

/// <summary>Where the packages of a release are downloaded from. A test gives its own, so no test goes to the network.</summary>
internal interface IPackageSource
{
    /// <summary>Asks for a package once.</summary>
    /// <param name="address">The address of the package.</param>
    PackageAnswer Fetch(Uri address);
}

/// <summary>nuget.org over HTTP.</summary>
internal sealed class HttpPackageSource : IPackageSource
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);

    /// <inheritdoc/>
    public PackageAnswer Fetch(Uri address)
    {
        using HttpClient client = new() { Timeout = Limit };
        using HttpRequestMessage request = new(HttpMethod.Get, address);
        try
        {
            using HttpResponseMessage response = client.Send(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new PackageAnswer(false, [], "the status is " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            }

            return new PackageAnswer(true, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult(), string.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new PackageAnswer(false, [], exception.Message);
        }
    }
}
