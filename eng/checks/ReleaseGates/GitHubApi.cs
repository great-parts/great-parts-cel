using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The API of GitHub that the gates read. A test gives its own answers, so no gate goes to the network.</summary>
internal interface IGitHubApi
{
    /// <summary>Reads one resource and returns its JSON text.</summary>
    /// <param name="path">The path and the query of the resource, such as <c>repos/great-parts/great-parts-cel/actions/runs/1/jobs</c>.</param>
    /// <exception cref="CheckInputException">The API did not answer or answered with an error.</exception>
    string Get(string path);
}

/// <summary>
/// The API of GitHub over HTTP. A token, when there is one, is sent as the bearer token; without it the requests are anonymous.
/// </summary>
internal sealed class HttpGitHubApi : IGitHubApi
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private readonly string _address;
    private readonly string? _token;

    /// <summary>Creates the access to the API.</summary>
    /// <param name="address">The address of the API, such as <c>https://api.github.com</c>.</param>
    /// <param name="token">The token of the job, or <see langword="null"/> or an empty text for anonymous requests.</param>
    internal HttpGitHubApi(string address, string? token)
    {
        _address = address.TrimEnd('/');
        _token = string.IsNullOrEmpty(token) ? null : token;
    }

    /// <inheritdoc/>
    public string Get(string path)
    {
        Uri address = new(_address + "/" + path.TrimStart('/'), UriKind.Absolute);
        using HttpClient client = new() { Timeout = Limit };
        using HttpRequestMessage request = new(HttpMethod.Get, address);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("great-parts-cel-eng-checks", "1"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        try
        {
            using HttpResponseMessage response = client.Send(request);
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            string status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            return response.IsSuccessStatusCode
                ? body
                : throw new CheckInputException($"the API of GitHub answered {status} to {path}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new CheckInputException($"the API of GitHub did not answer {path}: {exception.Message}", exception);
        }
    }
}
