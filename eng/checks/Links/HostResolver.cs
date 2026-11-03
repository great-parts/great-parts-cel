using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Tells whether a domain exists. The report of lychee 0.24.2 gives the same text, "Connection failed", for a domain that does not
/// resolve and for a connection that failed otherwise, so <c>links</c> asks the resolver before it names <c>ENG402</c>
/// (<c>docs/design/eng-docs.md</c>, "links").
/// </summary>
internal interface IHostResolver
{
    /// <summary>Whether the name server answered that the host has no address.</summary>
    /// <param name="host">The host name of the address.</param>
    bool IsUnknown(string host);
}

/// <summary>
/// The resolver of the machine, with a time limit. Only a definite answer, "no such host" or "no address", is unknown; an answer that
/// does not come, a name server that is unreachable or a timeout is not: the domain may exist, and the link is then a warning.
/// </summary>
internal sealed class DnsHostResolver : IHostResolver
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public bool IsUnknown(string host)
    {
        using CancellationTokenSource limit = new(Limit);
        try
        {
            Dns.GetHostAddressesAsync(host, limit.Token).GetAwaiter().GetResult();
            return false;
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
        {
            return true;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException or ArgumentException)
        {
            return false;
        }
    }
}
