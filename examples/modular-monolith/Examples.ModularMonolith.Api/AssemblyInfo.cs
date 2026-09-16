using DecoratR;

// The API is the composition root: it collects the handlers and decorators of every referenced module
// (and of the shared kernel) and generates AddDecoratR().
[assembly: GenerateDecoratRRegistrations]
