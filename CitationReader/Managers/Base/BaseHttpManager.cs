using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Models.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitationReader.Managers.Base;

public abstract class BaseHttpManager
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HuurOptions _options;
    private readonly HttpClientType _httpClientType;

    protected readonly ILogger Logger;

    protected BaseHttpManager(
        HttpClientType httpClientType,
        IHttpClientFactory httpClientFactory,
        IOptions<HuurOptions> options,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        Logger = logger;
        _httpClientType = httpClientType;
    }

    protected async Task<BaseResponse<T>> RequestAsync<T>(
        HttpMethod method,
        string endpoint,
        object? requestBody = null,
        string? token = null) where T : class
    {
        var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        Logger.LogInformation("Making {Method} request to {Uri}", method.Method, requestUri);

        try
        {
            using var request = new HttpRequestMessage(method, requestUri);

            // Add Authorization header if token is provided
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                Logger.LogDebug("Added Authorization header with Bearer token");
            }

            if (requestBody != null &&
                (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
            {
                var jsonContent = JsonSerializer.Serialize(requestBody);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Logger.LogDebug("Request body: {RequestBody}", jsonContent);
            }

            using var httpClient = _httpClientFactory.CreateClient(_httpClientType.ToString());
            using var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            Logger.LogInformation("Response status: {StatusCode}", response.StatusCode);
            Logger.LogDebug("Response content: {ResponseContent}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrEmpty(responseContent))
                {
                    Logger.LogWarning("Received empty response content");
                    return BaseResponse<T>.Failure(204, "Empty response content");
                }

                try
                {
                    var result = JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null)
                    {
                        Logger.LogInformation("Successfully deserialized response to {Type}", typeof(T).Name);
                        return BaseResponse<T>.Success(result, "Request completed successfully");
                    }

                    Logger.LogWarning("Deserialized response is null");
                    return BaseResponse<T>.Failure(422, "Failed to deserialize response");
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, "Failed to deserialize response as {Type}", typeof(T).Name);
                    return BaseResponse<T>.Failure(422, $"JSON deserialization failed: {ex.Message}", ex.StackTrace);
                }
            }

            // Handle HTTP error status codes
            var statusCode = (int)response.StatusCode;
            var errorMessage = GetErrorMessageForStatusCode(statusCode, responseContent);
            
            Logger.LogError("API request failed with status {StatusCode}: {ResponseContent}", statusCode, responseContent);
            return BaseResponse<T>.Failure(statusCode, errorMessage);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP request exception occurred");
            return BaseResponse<T>.Failure(500, $"HTTP request failed: {ex.Message}", ex.StackTrace);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Logger.LogError(ex, "Request timeout occurred");
            return BaseResponse<T>.Failure(408, "Request timeout");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error occurred during HTTP request");
            return BaseResponse<T>.Failure(-1, $"Unexpected error: {ex.Message}", ex.StackTrace);
        }
    }

    private static string GetErrorMessageForStatusCode(int statusCode, string responseContent)
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
