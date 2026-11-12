namespace System.Runtime.CompilerServices;

// netstandard2.0 has no IsExternalInit; the compiler needs it for the init accessors of records.
internal static class IsExternalInit
{
}
