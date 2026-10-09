using System.Net;

namespace Kronxy.Web.Clients;

public sealed class KronxyApiException : Exception
{
    public KronxyApiException(HttpStatusCode? statusCode, string code, string safeMessage,
        Exception? innerException = null, string? diagnosticCode = null,
        string? correlationId = null) : base(safeMessage, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        DiagnosticCode = diagnosticCode;
        CorrelationId = correlationId;
    }

    public HttpStatusCode? StatusCode { get; }
    public string Code { get; }
    public string? DiagnosticCode { get; }
    public string? CorrelationId { get; }

    public KronxyApiException WithCorrelationId(string correlationId) =>
        new(
            StatusCode,
            Code,
            Message,
            InnerException,
            DiagnosticCode,
            correlationId);

    public static string Category(HttpStatusCode? statusCode) => statusCode switch
    {
        null => "API unreachable",
        HttpStatusCode.NotFound => "Not found",
        HttpStatusCode.BadRequest => "Validation error",
        HttpStatusCode.Unauthorized => "Authentication required",
        HttpStatusCode.Forbidden => "Access denied",
        HttpStatusCode.Conflict => "Conflict",
        HttpStatusCode.RequestTimeout => "Operation timed out",
        HttpStatusCode.TooManyRequests => "Too many requests",
        HttpStatusCode.InternalServerError => "Unexpected API error",
        HttpStatusCode.ServiceUnavailable => "Service unavailable",
        _ => "Unexpected API error"
    };
}
