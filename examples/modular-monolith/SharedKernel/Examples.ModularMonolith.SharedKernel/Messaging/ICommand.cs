using DecoratR;

namespace Examples.ModularMonolith.SharedKernel.Messaging;

/// <summary>A request that changes state. Commands are validated before they reach their handler.</summary>
public interface ICommand : IRequest;
