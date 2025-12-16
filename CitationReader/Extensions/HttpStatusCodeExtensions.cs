namespace CitationReader.Extensions;

public static class HttpStatusCodeExtensions
{
    public static string GetErrorMessageForStatusCode(int statusCode, string responseContent)
    {
        return statusCode switch
        {
            400 => "Bad request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not found",
            409 => "Conflict",
            422 => "Unprocessable entity",
            429 => "Too many requests",
            500 => "Internal server error",
            502 => "Bad gateway",
            503 => "Service unavailable",
            504 => "Gateway timeout",
            _ => $"HTTP {statusCode}: {responseContent}"
        };
    }
}
