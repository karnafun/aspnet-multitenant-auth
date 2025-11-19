using AuthMastery.API.DTO;
using Microsoft.AspNetCore.Mvc;

namespace AuthMastery.API.Extensions
{
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
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, title, detail) = exception switch
            {

                NotFoundException nfEx => (404, "Not Found", nfEx.Message),
                BadRequestException brEx => (400, "Bad Request", brEx.Message),
                UnauthorizedException uEx => (401, "Unauthorized", uEx.Message),
                ConflictException cEx => (409, "Conflict", cEx.Message),
                TagOperationException toEx => (500, "Internal Server Error", "Failed tag operation"),
                _ => (500, "Internal Server Error", "An unexpected error occurred")
            };

            if (statusCode == 500)
            {
                _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
            }
            else
            {
                _logger.LogWarning(exception, "{ExceptionType}: {Message}",
                    exception.GetType().Name, exception.Message);
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail, 
                Instance = $"{context.Request.Method} {context.Request.Path}",
                Extensions =
            {
                ["traceId"] = context.TraceIdentifier
            }
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
