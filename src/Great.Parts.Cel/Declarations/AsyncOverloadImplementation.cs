using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The asynchronous implementation of an overload as evaluation keeps it. It receives the argument values in the order of the
// parameters, never an error value, the call site that gives the errors of the call their place, and the token of the evaluation;
// it returns the result or an error value created through the site. The call site carries no meter and no exception boundary: the
// call runs between two passes, outside the frame of any of them.
internal delegate ValueTask<CelValue> AsyncOverloadImplementation(ImmutableArray<CelValue> arguments, CallSite site, CancellationToken cancellationToken);
