namespace DecoratR.IntegrationTests.Library;

public interface ILibraryCommand : IRequest;

public sealed record LibraryCommand(string Name) : ILibraryCommand;

public sealed record LibraryQuery : IRequest;

/// <summary>A command without a response, handled by an internal handler.</summary>
public sealed record LibraryVoidCommand : IRequest;

public sealed record LibraryStream(int Count) : IStreamRequest;
