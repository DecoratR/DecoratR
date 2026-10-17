namespace DecoratR.IntegrationTests.Library;

public interface ILibraryCommand : IRequest;

public sealed record LibraryCommand(string Name) : ILibraryCommand;

public sealed record LibraryQuery : IRequest;

/// <summary>A command without a response, handled by an internal handler.</summary>
public sealed record LibraryVoidCommand : IRequest;

public sealed record LibraryStream(int Count) : IStreamRequest;

/// <summary>Marker for requests the host's <c>HostFallbackDecorator</c> applies to.</summary>
public interface ITaggedRequest : IRequest;

/// <summary>Handled by a handler whose constructor the generator does not call directly (optional parameter).</summary>
public sealed record LibraryFallbackQuery(string Name) : ITaggedRequest;
