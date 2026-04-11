namespace DecoratR.Sample.Application.Abstractions;

public interface IStreamQueryHandler<in TQuery, out TResponse> : IStreamRequestHandler<TQuery, TResponse>
    where TQuery : IStreamQuery;
