using System;
using System.Reflection;

namespace Great.Parts.Cel.Wasm.App;

// The control example of the check of 0 IL warnings (the job wasm of ci.yml). It is compiled only with -p:IlControl=true. The parameter
// has no annotation of the members that the call reads, so the trimmer cannot tell which members to keep: the publish reports IL2070.
internal static class IlWarning
{
    internal static MethodInfo? Find(Type type, string name) => type.GetMethod(name);
}
