using System;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Builds a <see cref="CelProtobufTypes"/> provider from the descriptors of <c>.proto</c> files and from
/// typed proto2 extensions.
/// </summary>
/// <remarks>
/// <para>
/// Add every file whose messages expressions read, and every extension they read, to one builder: one
/// provider then describes them all, together with the well-known types, and resolves the type URL of a
/// <c>google.protobuf.Any</c> among all of them.
/// </para>
/// <para>
/// The builder is not thread-safe; the providers it builds are immutable.
/// </para>
/// </remarks>
public sealed class CelProtobufTypesBuilder
{
    /// <summary>
    /// Starts an empty builder.
    /// </summary>
    /// <param name="sourceName">The name of the source, which build errors of an environment show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty.</exception>
    public CelProtobufTypesBuilder(string sourceName)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds the messages and enumerations of a <c>.proto</c> file, including nested ones, and of the files
    /// it imports, directly or not. Adding a file again has no effect.
    /// </summary>
    /// <param name="file">The descriptor of the file, such as the <c>Descriptor</c> property of the reflection class <c>protoc</c> generates for it.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is <see langword="null"/>.</exception>
    public CelProtobufTypesBuilder AddFile(FileDescriptor file)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds a proto2 extension field to the description of the message it extends. Expressions select it
    /// by the full name of the extension, as the macros <c>proto.getExt</c> and <c>proto.hasExt</c> of the
    /// <c>protos</c> extension set do.
    /// </summary>
    /// <remarks>
    /// Extensions are registered with their typed objects, which <c>protoc</c> generates, because finding
    /// extensions through a registry and reflection does not work under Native AOT. Both the file that
    /// declares the extension and the file of the extended message must be added with
    /// <see cref="AddFile"/> before <see cref="Build"/>.
    /// </remarks>
    /// <typeparam name="TMessage">The extended message.</typeparam>
    /// <typeparam name="TValue">The .NET type of the value of the extension.</typeparam>
    /// <param name="extension">The extension object that <c>protoc</c> generates.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> is <see langword="null"/>.</exception>
    public CelProtobufTypesBuilder AddProtoExtension<TMessage, TValue>(Extension<TMessage, TValue> extension)
        where TMessage : IExtendableMessage<TMessage>
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds a repeated proto2 extension field to the description of the message it extends; expressions
    /// read it as a list.
    /// </summary>
    /// <remarks>
    /// The rules of <see cref="AddProtoExtension{TMessage, TValue}(Extension{TMessage, TValue})"/> apply.
    /// </remarks>
    /// <typeparam name="TMessage">The extended message.</typeparam>
    /// <typeparam name="TValue">The .NET type of one element of the extension.</typeparam>
    /// <param name="extension">The extension object that <c>protoc</c> generates.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> is <see langword="null"/>.</exception>
    public CelProtobufTypesBuilder AddProtoExtension<TMessage, TValue>(RepeatedExtension<TMessage, TValue> extension)
        where TMessage : IExtendableMessage<TMessage>
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the provider from the files and extensions added so far. The builder can be used further;
    /// later additions do not change providers built before.
    /// </summary>
    /// <returns>The provider.</returns>
    /// <exception cref="InvalidOperationException">An added extension is declared in a file, or extends a message of a file, that was not added.</exception>
    public CelProtobufTypes Build()
    {
        throw new NotImplementedException();
    }
}
