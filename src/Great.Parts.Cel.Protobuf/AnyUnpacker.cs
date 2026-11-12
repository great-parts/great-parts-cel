using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Why an <c>Any</c> does not unpack.
/// </summary>
internal enum UnpackFailure
{
    None,
    EmptyUrl,
    NoSlash,
    NotDescribed,
    BadContent,
    TooDeep,
}

/// <summary>
/// Reads a <c>google.protobuf.Any</c> as the message it contains.
/// </summary>
internal static class AnyUnpacker
{
    /// <summary>
    /// The longest chain of <c>Any</c> messages, each holding the next, that is unpacked.
    /// </summary>
    internal const int MaxDepth = 16;

    /// <summary>
    /// Unpacks an <c>Any</c> that is set, for a field that is read or a literal that is evaluated.
    /// </summary>
    /// <param name="owner">The description of the message that holds the field.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="any">The message.</param>
    /// <returns>The value of the content, or an error value.</returns>
    internal static CelValue Unpack(MessageType owner, int number, Any any)
    {
        if (TryUnpack(owner.Registry, any, out IMessage? content, out UnpackFailure failure, out Any failed, out string? message))
        {
            return KnownValues.FromMessage(content, owner, number);
        }

        return FieldErrors.InvalidAny(owner, number, failed.TypeUrl, Detail(failure, failed.TypeUrl, owner.Registry.SourceName, message));
    }

    /// <summary>
    /// Unpacks an <c>Any</c> to the message at the end of its chain, which is not an <c>Any</c> itself. Nothing is charged here and
    /// nothing is cached: each call parses the content again.
    /// </summary>
    /// <param name="registry">The registry of the provider.</param>
    /// <param name="any">The message.</param>
    /// <param name="content">The message at the end of the chain when the method returns <see langword="true"/>.</param>
    /// <param name="failure">Why the unpacking failed; <see cref="UnpackFailure.None"/> on success.</param>
    /// <param name="failed">The <c>Any</c> of the chain that failed: the one whose address is not described, whose content does not parse, or the seventeenth.</param>
    /// <param name="message">For a content that does not parse, the message of the exception.</param>
    /// <returns><see langword="true"/> when the chain unpacks.</returns>
    internal static bool TryUnpack(
        ProviderTypes registry,
        Any any,
        [NotNullWhen(true)] out IMessage? content,
        out UnpackFailure failure,
        out Any failed,
        out string? message)
    {
        content = null;
        message = null;
        failed = any;
        for (int depth = 1; ; depth++)
        {
            IMessage? parsed = null;
            failure = depth > MaxDepth ? UnpackFailure.TooDeep : Parse(registry, failed, out parsed, out message);
            if (failure != UnpackFailure.None)
            {
                return false;
            }

            if (parsed is Any inner)
            {
                failed = inner;
                continue;
            }

            content = parsed!;
            return true;
        }
    }

    // Looks up the name after the last slash of the address and parses the content with the parser of the message.
    private static UnpackFailure Parse(ProviderTypes registry, Any any, out IMessage? content, out string? message)
    {
        content = null;
        message = null;
        string url = any.TypeUrl;
        if (url.Length == 0)
        {
            return UnpackFailure.EmptyUrl;
        }

        int slash = url.AsSpan().LastIndexOf('/');
        if (slash < 0)
        {
            return UnpackFailure.NoSlash;
        }

        if (!registry.TryGetParser(url.AsSpan(slash + 1), out MessageParser? parser))
        {
            return UnpackFailure.NotDescribed;
        }

        try
        {
            content = parser.ParseFrom(any.Value.Span);
        }
        catch (InvalidProtocolBufferException exception)
        {
            message = exception.Message;
            return UnpackFailure.BadContent;
        }

        return UnpackFailure.None;
    }

    // The free part of the text of the error 3020.
    private static string Detail(UnpackFailure failure, string url, string sourceName, string? message)
    {
        string name = url[(url.AsSpan().LastIndexOf('/') + 1)..];
        return failure switch
        {
            UnpackFailure.EmptyUrl => "the type URL is empty",
            UnpackFailure.NoSlash => "the type URL has no '/'",
            UnpackFailure.NotDescribed => "the message type '" + name + "' is not described by the type source '" + sourceName + "'",
            UnpackFailure.BadContent => "the content is not a valid " + name + ": " + message,
            _ => "Any is nested deeper than " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels",
        };
    }
}
