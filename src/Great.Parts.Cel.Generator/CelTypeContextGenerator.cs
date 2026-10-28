using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

/// <summary>
/// The incremental source generator of the application data types. The pipeline is empty so far: the generator
/// produces no output and reports no diagnostics.
/// </summary>
[Generator]
public sealed class CelTypeContextGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
