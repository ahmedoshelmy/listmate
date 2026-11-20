using FluentValidation;
using Listmate.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Listmate.Api.Hosting;

/// <summary>Maps application exceptions to RFC 7807 problem responses.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException v => new ValidationProblemDetails(
                v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            { Status = StatusCodes.Status400BadRequest, Title = "The request is invalid." },
            ArgumentException a => new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "The request is invalid.", Detail = a.Message },
            ListNotFoundException n => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = n.Message },
            ConcurrencyConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The list is busy; retry the request.",
            },
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }
}
