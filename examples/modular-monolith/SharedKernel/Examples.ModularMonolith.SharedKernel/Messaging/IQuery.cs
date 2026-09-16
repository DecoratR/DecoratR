using DecoratR;

namespace Examples.ModularMonolith.SharedKernel.Messaging;

/// <summary>A side-effect free request that returns <typeparamref name="TResult"/>.</summary>
public interface IQuery<TResult> : IRequest;
