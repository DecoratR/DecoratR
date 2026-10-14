using DecoratR;

// GenerateDecoratRRegistrations emits AddDecoratR() for the resolve benchmarks; GenerateDecoratRMetadata emits the
// public Apply… methods of DecoratRDecoratorRegistry that the startup benchmarks call for thousands of service types.
[assembly: GenerateDecoratRMetadata]
[assembly: GenerateDecoratRRegistrations]
