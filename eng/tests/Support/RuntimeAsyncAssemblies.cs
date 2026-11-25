using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Great.Parts.Cel.Eng.Checks;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>How a method of a test assembly is async.</summary>
internal enum AsyncKind
{
    /// <summary>The method is not async.</summary>
    None,

    /// <summary>The compiler turned the method into a state machine: it has <c>AsyncStateMachineAttribute</c>.</summary>
    StateMachine,

    /// <summary>The compiler built the method as a runtime-async method: it has the implementation flag.</summary>
    RuntimeAsync,

    /// <summary>The method has a builder of its own (<c>AsyncMethodBuilderAttribute</c>), so the compiler left it a state machine.</summary>
    StateMachineWithBuilder,
}

/// <summary>
/// Assemblies for the tests of the check of runtime async. The check only reads metadata, and Roslyn of the test project (it builds for
/// .NET 10) cannot make a runtime-async method, so the assemblies are written with <see cref="MetadataBuilder"/>: public static classes
/// of namespace <c>Test</c> with public static methods that have no body, and the attributes of the kind of each method.
/// </summary>
internal static class RuntimeAsyncAssemblies
{
    private const string AttributeNamespace = "System.Runtime.CompilerServices";

    // The signature of a static method without parameters that returns void; of an instance constructor without parameters; and the value
    // of an attribute without arguments (the prolog and no named arguments).
    private static readonly byte[] MethodSignature = [0x00, 0x00, 0x01];
    private static readonly byte[] ConstructorSignature = [0x20, 0x00, 0x01];
    private static readonly byte[] AttributeBlob = [0x01, 0x00, 0x00, 0x00];

    /// <summary>Makes the bytes of an assembly.</summary>
    /// <param name="methods">The methods: the name of the class, the name of the method and how it is async.</param>
    internal static byte[] Create(params (string Type, string Method, AsyncKind Kind)[] methods)
    {
        MetadataBuilder metadata = new();
        metadata.AddModule(0, metadata.GetOrAddString("Test.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("Test"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        AssemblyReferenceHandle runtime = metadata.AddAssemblyReference(metadata.GetOrAddString("System.Runtime"), new Version(11, 0, 0, 0), default, default, 0, default);
        TypeReferenceHandle objectType = metadata.AddTypeReference(runtime, metadata.GetOrAddString("System"), metadata.GetOrAddString("Object"));
        MemberReferenceHandle stateMachine = Constructor(metadata, runtime, "AsyncStateMachineAttribute");
        MemberReferenceHandle builder = Constructor(metadata, runtime, "AsyncMethodBuilderAttribute");
        BlobHandle signature = metadata.GetOrAddBlob(MethodSignature);
        BlobHandle attributeValue = metadata.GetOrAddBlob(AttributeBlob);

        metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        foreach (IGrouping<string, (string Type, string Method, AsyncKind Kind)> type in methods.GroupBy(method => method.Type))
        {
            MethodDefinitionHandle first = default;
            foreach ((string _, string name, AsyncKind kind) in type)
            {
                MethodImplAttributes flags = kind == AsyncKind.RuntimeAsync ? AssemblyFacts.RuntimeAsyncFlag : MethodImplAttributes.IL;
                MethodDefinitionHandle handle = metadata.AddMethodDefinition(
                    MethodAttributes.Public | MethodAttributes.Static,
                    flags,
                    metadata.GetOrAddString(name),
                    signature,
                    -1,
                    MetadataTokens.ParameterHandle(1));
                first = first.IsNil ? handle : first;

                // The rows of the table of custom attributes are sorted by the owner, and the methods are made in order.
                if (kind is AsyncKind.StateMachine or AsyncKind.StateMachineWithBuilder)
                {
                    metadata.AddCustomAttribute(handle, stateMachine, attributeValue);
                }

                if (kind == AsyncKind.StateMachineWithBuilder)
                {
                    metadata.AddCustomAttribute(handle, builder, attributeValue);
                }
            }

            metadata.AddTypeDefinition(
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed,
                metadata.GetOrAddString("Test"),
                metadata.GetOrAddString(type.Key),
                objectType,
                MetadataTokens.FieldDefinitionHandle(1),
                first);
        }

        BlobBuilder image = new();
        _ = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder()).Serialize(image);
        return image.ToArray();
    }

    // The constructor without parameters of an attribute of System.Runtime.CompilerServices: the check reads only the type of the attribute.
    private static MemberReferenceHandle Constructor(MetadataBuilder metadata, AssemblyReferenceHandle runtime, string attribute)
    {
        TypeReferenceHandle type = metadata.AddTypeReference(runtime, metadata.GetOrAddString(AttributeNamespace), metadata.GetOrAddString(attribute));
        return metadata.AddMemberReference(type, metadata.GetOrAddString(".ctor"), metadata.GetOrAddBlob(ConstructorSignature));
    }
}
