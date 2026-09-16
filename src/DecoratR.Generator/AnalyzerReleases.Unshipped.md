; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DCTR004 | DecoratR.Generator | Error | Decorator does not implement exactly one handler interface
DCTR005 | DecoratR.Generator | Error | Decorator type parameters do not map to the handler interface
DCTR006 | DecoratR.Generator | Warning | Decorator is ignored
DCTR007 | DecoratR.Generator | Error | Multiple handlers for the same service type
DCTR008 | DecoratR.Generator | Warning | Type is not accessible from generated code
DCTR009 | DecoratR.Generator | Error | Microsoft.Extensions.DependencyInjection.Abstractions is not referenced
DCTR010 | DecoratR.Generator | Warning | Handler is a value type
DCTR011 | DecoratR.Generator | Warning | Decorators cannot be applied to a referenced service type with non-public types
DCTR012 | DecoratR.Generator | Warning | Decorator has no constructor accepting the inner handler
DCTR013 | DecoratR.Generator | Info | Request or response type of a handler is not public
DCTR014 | DecoratR.Generator | Warning | Decorator constraint type is not public

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
DCTR002 | DecoratR.Generator | Hidden | DecoratR.Generator | Info | Discovery counts are tooling information only
DCTR003 | DecoratR.Generator | Hidden | DecoratR.Generator | Info | Discovery counts are tooling information only
