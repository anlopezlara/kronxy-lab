using System.Net;

namespace Kronxy.Web.Clients;

public sealed class KronxyApiException : Exception
{
    public KronxyApiException(HttpStatusCode? statusCode, string code, string safeMessage,
        Exception? innerException = null) : base(safeMessage, innerException)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode? StatusCode { get; }
    public string Code { get; }

    public static string Category(HttpStatusCode? statusCode) => statusCode switch
    {
        null => "API unreachable",
        HttpStatusCode.NotFound => "Not found",
        HttpStatusCode.BadRequest => "Validation error",
        HttpStatusCode.Conflict => "Conflict",
        HttpStatusCode.ServiceUnavailable => "Service unavailable",
        _ => "Unexpected API error"
    };
}
