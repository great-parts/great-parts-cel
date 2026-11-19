// The type that holds a CEL decimal on the target, as in the library: a Decimal128 on net11.0, a System.Decimal on net10.0.
#if NET11_0_OR_GREATER
global using PlatformDecimal = System.Numerics.Decimal128;
#else
global using PlatformDecimal = decimal;
#endif
