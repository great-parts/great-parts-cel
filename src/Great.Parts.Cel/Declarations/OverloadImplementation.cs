using System;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The implementation of an overload. It receives the argument values in the order of the parameters,
// never an error value, and returns the result or an error value created through the call site; it
// does not throw.
internal delegate CelValue OverloadImplementation(ReadOnlySpan<CelValue> arguments, in CallSite site);
