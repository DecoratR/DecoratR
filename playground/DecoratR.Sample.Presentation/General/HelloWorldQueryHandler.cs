namespace DecoratR.Sample.Presentation.General;

internal sealed class HelloWorldQueryHandler: IRequestHandler<HelloWorldQuery, string>
{
    public ValueTask<string> HandleAsync(HelloWorldQuery request, CancellationToken cancellationToken = default) => ValueTask.FromResult("Hello World!");
}