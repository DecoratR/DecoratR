using DecoratR;

namespace Examples.SimpleApi;

internal sealed record AddTodoCommand(string Title) : IRequest;
