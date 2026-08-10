using System.Net;
using System.Text.Json;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Domain.Exceptions;
using ValidationException = KotoDibo.Application.Common.Exceptions.ValidationException;

namespace KotoDibo.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, errors) = exception switch
        {
            ValidationException validationException => (
                HttpStatusCode.BadRequest, "Validation failed.", (object?)validationException.Errors),
            FluentValidation.ValidationException fluentValidationException => (
                HttpStatusCode.BadRequest, "Validation failed.", (object?)fluentValidationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            UnauthorizedException unauthorizedException => (
                HttpStatusCode.Unauthorized, unauthorizedException.Message, null),
            ForbiddenException forbiddenException => (
                HttpStatusCode.Forbidden, forbiddenException.Message, null),
            NotFoundException notFoundException => (
                HttpStatusCode.NotFound, notFoundException.Message, null),
            DuplicateKeyException duplicateKeyException => (
                HttpStatusCode.Conflict, duplicateKeyException.Message, null),
            DomainException domainException => (
                HttpStatusCode.BadRequest, domainException.Message, null),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", null),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred while processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new
        {
            status = (int)statusCode,
            title,
            errors,
        });

        await context.Response.WriteAsync(payload);
    }
}
