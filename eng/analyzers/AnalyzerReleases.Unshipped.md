; Rules of the architecture analyzer that are not yet in a release of the repository.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARCH001 | Architecture | Error | NamespaceGraphAnalyzer: namespace cycle
ARCH002 | Architecture | Error | ApiConceptListAnalyzer: public type missing from the API concept list
ARCH003 | Architecture | Error | ApiConceptListAnalyzer: API concept list row without a public type
ARCH004 | Architecture | Error | ApiConceptListAnalyzer: empty cell or malformed row of the API concept list
ARCH005 | Architecture | Error | NamespaceGraphAnalyzer: reference outside the layer table
ARCH006 | Architecture | Error | PublicNameAnalyzer: reserved public type name
ARCH007 | Architecture | Error | PublicNameAnalyzer: public member requires unreferenced or dynamic code
ARCH008 | Architecture | Error | QualifiedNameAnalyzer: qualified name starting with Cel without global::
