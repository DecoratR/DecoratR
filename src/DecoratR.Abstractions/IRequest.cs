namespace DecoratR;

/// <summary>
/// Marker interface for a request that returns <typeparamref name="TResponse"/>.
/// </summary>
public interface IRequest<TResponse>;

/// <summary>
/// Marker for a command (write operation) that returns <typeparamref name="TResponse"/>.
/// </summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>
/// Marker for a query (read operation) that returns <typeparamref name="TResponse"/>.
/// </summary>
public interface IQuery<TResponse> : IRequest<TResponse>;