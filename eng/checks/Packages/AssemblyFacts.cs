using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// What the check of runtime async (<c>docs/design/eng-checks-artifacts.md</c>, "packages") reads of a managed assembly with
/// <c>System.Reflection.Metadata</c>: the methods with the runtime-async flag, the async methods that the compiler turned into a state
/// machine, and the public surface (the names of the visible types, methods and fields).
/// </summary>
internal sealed class AssemblyFacts
{
    /// <summary>
    /// The implementation flag of a method that the compiler built as a runtime-async method: <c>MethodImplAttributes.Async</c> of .NET 11,
    /// the value 0x2000. The project of the check builds for .NET 10, whose enumeration has no such member.
    /// </summary>
    internal const MethodImplAttributes RuntimeAsyncFlag = (MethodImplAttributes)0x2000;

    private const string StateMachineAttribute = "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    private const string BuilderAttribute = "System.Runtime.CompilerServices.AsyncMethodBuilderAttribute";

    private AssemblyFacts(IReadOnlyList<string> runtimeAsyncMethods, IReadOnlyList<string> stateMachineMethods, IReadOnlyList<string> surface)
    {
        RuntimeAsyncMethods = runtimeAsyncMethods;
        StateMachineMethods = stateMachineMethods;
        Surface = surface;
    }

    /// <summary>The methods with the runtime-async flag, as <c>Type.Method</c> in the order of the metadata.</summary>
    internal IReadOnlyList<string> RuntimeAsyncMethods { get; }

    /// <summary>
    /// The async methods that the compiler turned into a state machine (they have <c>AsyncStateMachineAttribute</c>) and that have no
    /// builder of their own (<c>AsyncMethodBuilderAttribute</c> on the method), as <c>Type.Method</c>. A method with a builder of its own is
    /// left as a state machine by the compiler even when runtime async is on, so it is not a finding.
    /// </summary>
    internal IReadOnlyList<string> StateMachineMethods { get; }

    /// <summary>
    /// The public surface in the ordinal order: <c>T:Type</c> for a visible type, <c>M:Type.Method(generic parameters,parameters)</c> for a
    /// public or protected method, <c>F:Type.Field</c> for a public or protected field. An overload with the same shape appears again.
    /// </summary>
    internal IReadOnlyList<string> Surface { get; }

    /// <summary>Reads an assembly.</summary>
    /// <param name="image">The bytes of the <c>.dll</c> file.</param>
    /// <exception cref="BadImageFormatException">The bytes are not a managed assembly.</exception>
    internal static AssemblyFacts Read(ImmutableArray<byte> image)
    {
        using PEReader reader = new(image);
        if (!reader.HasMetadata)
        {
            throw new BadImageFormatException("the file has no metadata");
        }

        MetadataReader metadata = reader.GetMetadataReader();
        List<string> runtimeAsync = [];
        List<string> stateMachines = [];
        List<string> surface = [];
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string typeName = TypeName(metadata, handle);
            bool visible = IsVisible(metadata, type);
            if (visible)
            {
                surface.Add("T:" + typeName);
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                string name = typeName + "." + metadata.GetString(method.Name);
                Classify(metadata, method, name, runtimeAsync, stateMachines);
                if (visible && IsExposed((int)(method.Attributes & MethodAttributes.MemberAccessMask)))
                {
                    surface.Add($"M:{name}({Shape(metadata, method)})");
                }
            }

            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = metadata.GetFieldDefinition(fieldHandle);
                if (visible && IsExposed((int)(field.Attributes & FieldAttributes.FieldAccessMask)))
                {
                    surface.Add($"F:{typeName}.{metadata.GetString(field.Name)}");
                }
            }
        }

        surface.Sort(StringComparer.Ordinal);
        return new AssemblyFacts(runtimeAsync, stateMachines, surface);
    }

    // The access values of a method and of a field are the same numbers: public (6), protected (4) and protected or internal (5).
    private static bool IsExposed(int access) => access is 6 or 4 or 5;

    // A type is visible outside the assembly when it is public, or protected and nested in a visible type, or public and nested in one.
    private static bool IsVisible(MetadataReader metadata, TypeDefinition type) => (type.Attributes & TypeAttributes.VisibilityMask) switch
    {
        TypeAttributes.Public => true,
        TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem =>
            IsVisible(metadata, metadata.GetTypeDefinition(type.GetDeclaringType())),
        _ => false,
    };

    private static void Classify(MetadataReader metadata, MethodDefinition method, string name, List<string> runtimeAsync, List<string> stateMachines)
    {
        if ((method.ImplAttributes & RuntimeAsyncFlag) != 0)
        {
            runtimeAsync.Add(name);
            return;
        }

        bool stateMachine = false;
        bool ownBuilder = false;
        foreach (CustomAttributeHandle handle in method.GetCustomAttributes())
        {
            string attribute = AttributeName(metadata, metadata.GetCustomAttribute(handle));
            stateMachine |= attribute == StateMachineAttribute;
            ownBuilder |= attribute == BuilderAttribute;
        }

        if (stateMachine && !ownBuilder)
        {
            stateMachines.Add(name);
        }
    }

    // The full name of the type of the attribute, from the type reference or the type definition of its constructor.
    private static string AttributeName(MetadataReader metadata, CustomAttribute attribute)
    {
        EntityHandle owner = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default,
        };
        switch (owner.Kind)
        {
            case HandleKind.TypeReference:
                TypeReference reference = metadata.GetTypeReference((TypeReferenceHandle)owner);
                return Join(metadata.GetString(reference.Namespace), metadata.GetString(reference.Name));
            case HandleKind.TypeDefinition:
                return TypeName(metadata, (TypeDefinitionHandle)owner);
            default:
                return string.Empty;
        }
    }

    // The number of generic parameters and the number of parameters, from the signature of the method.
    private static string Shape(MetadataReader metadata, MethodDefinition method)
    {
        BlobReader blob = metadata.GetBlobReader(method.Signature);
        SignatureHeader header = blob.ReadSignatureHeader();
        int generics = header.IsGeneric ? blob.ReadCompressedInteger() : 0;
        int parameters = blob.ReadCompressedInteger();
        return string.Create(CultureInfo.InvariantCulture, $"{generics},{parameters}");
    }

    private static string TypeName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        TypeDefinition type = metadata.GetTypeDefinition(handle);
        string name = metadata.GetString(type.Name);
        TypeDefinitionHandle declaring = type.GetDeclaringType();
        return declaring.IsNil
            ? Join(metadata.GetString(type.Namespace), name)
            : TypeName(metadata, declaring) + "+" + name;
    }

    private static string Join(string space, string name) => space.Length == 0 ? name : space + "." + name;
}
