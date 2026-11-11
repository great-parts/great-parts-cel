using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

// An abstract type that an extension set declares: its full name, such as net.IP, and its type. The name is an
// identifier of the declaration index, as int is.
internal sealed record AbstractTypeDeclaration(string Name, CelType Type);
