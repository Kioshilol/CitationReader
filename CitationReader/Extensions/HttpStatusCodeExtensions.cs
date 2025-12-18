using System.Text.Json;

namespace CitationReader.Extensions;

public static class HttpStatusCodeExtensions
{
    public static string GetErrorMessageForStatusCode(int statusCode, string responseContent)
    {
        var jsonErrorMessage = TryParseJsonError(responseContent);
        if (!string.IsNullOrEmpty(jsonErrorMessage))
        {
            return jsonErrorMessage;
        }

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

    private static string? TryParseJsonError(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement))
            {
                var errorMessage = errorElement.GetString();
                
                if (root.TryGetProperty("details", out var detailsElement))
                {
                    var details = detailsElement.GetString();
                    return $"{errorMessage}, details: {details}";
                }
                
                return errorMessage;
            }
            
            if (root.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }

            if (root.TryGetProperty("detail", out var detailElement))
            {
                return detailElement.GetString();
            }

            if (root.TryGetProperty("title", out var titleElement))
            {
                return titleElement.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
