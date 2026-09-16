using Examples.ModularMonolith.SharedKernel.Validation;
using Microsoft.AspNetCore.Diagnostics;

namespace Examples.ModularMonolith.Api;

/// <summary>Turns a <see cref="ValidationException"/> thrown by the validation decorator into a 400 problem response.</summary>
internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation) return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [.. validation.Errors] }),
        });
    }
}
