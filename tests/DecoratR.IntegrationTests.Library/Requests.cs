namespace DecoratR.IntegrationTests.Library;

public interface ILibraryCommand : IRequest;

public sealed record LibraryCommand(string Name) : ILibraryCommand;

public sealed record LibraryQuery : IRequest;

public sealed record LibraryStream(int Count) : IStreamRequest;
