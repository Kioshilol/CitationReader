using System.Text;
using System.Text.Json;
using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Models.Base;
using CitationReader.Services.Huur.Auth;
using Microsoft.Extensions.Options;

namespace CitationReader.Managers.Base;

public abstract class BaseHttpManager
{
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 1000;
    private const int UnauthorizedReason = 401;
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HuurOptions _options;
    private readonly HttpClientType _httpClientType;

    protected BaseHttpManager(HttpClientType httpClientType)
    {
        _httpClientType = httpClientType;
        
        _httpClientFactory = Program.ServiceProvider.GetService<IHttpClientFactory>()!;
        _options = Program.ServiceProvider.GetService<IOptions<HuurOptions>>()!.Value;
        Logger = Program.ServiceProvider.GetService<ILogger>()!;
    }
    
    protected readonly ILogger Logger;

    protected async Task<BaseResponse<T>> RequestAsync<T>(
        HttpMethod method,
        string endpoint,
        object? requestBody = null,
        string? token = null) where T : class
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var delay = RetryDelayMs * (int)Math.Pow(2, attempt);
            try
            {
                var result = await ExecuteRequestAsync<T>(method, endpoint, requestBody, token);
                if (result.IsSuccess)
                {
                    return result;
                }

                if (attempt >= MaxRetries - 1)
                {
                    return result;
                }
                
                if (result.Reason == UnauthorizedReason)
                {
                    Logger.LogWarning(
                        "Received 401 Unauthorized, attempting token refresh. Attempt {Attempt}/{MaxRetries}",
                        attempt + 1,
                        MaxRetries);

                    var refreshed = await RefreshTokenAsync();
                    if (refreshed)
                    {
                        await Task.Delay(delay);
                        continue;
                    }

                    Logger.LogError("Failed to refresh token, returning 401 error");
                    return result;
                }
                
                Logger.LogWarning(
                    "Request failed with reason {Reason}, retrying. Attempt {Attempt}/{MaxRetries}",
                    result.Reason,
                    attempt + 1,
                    MaxRetries);
                    
                await Task.Delay(delay);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Logger.LogWarning(
                    ex, 
                    "HTTP request failed on attempt {Attempt}/{MaxRetries}, retrying...", 
                    attempt + 1,
                    MaxRetries);
                
                await Task.Delay(delay);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < MaxRetries - 1)
            {
                Logger.LogWarning(
                    ex, 
                    "Request timeout on attempt {Attempt}/{MaxRetries}, retrying...", 
                    attempt + 1, 
                    MaxRetries);
                
                await Task.Delay(delay);
            }
        }

        return await ExecuteRequestAsync<T>(method, endpoint, requestBody, token);
    }

    private async Task<BaseResponse<T>> ExecuteRequestAsync<T>(
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

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                Logger.LogDebug("Added Authorization header with Bearer token");
            }

            if (requestBody != null)
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
                    var result = JsonSerializer.Deserialize<BaseResponse<T>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = false
                    });

                    if (result != null)
                    {
                        Logger.LogInformation("Successfully deserialized response to {Type}", typeof(T).Name);
                        return result;
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

            var statusCode = (int)response.StatusCode;
            var errorMessage = HttpStatusCodeExtensions.GetErrorMessageForStatusCode(statusCode, responseContent);
            
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

    private async Task<bool> RefreshTokenAsync()
    {
        try
        {
            if (_httpClientType == HttpClientType.Auth)
            {
                Logger.LogDebug("Skipping token refresh for AuthManager to avoid circular dependency");
                return false;
            }

            var authManager = Program.ServiceProvider.GetService<IAuthService>()!;
            var isSuccess = await authManager.TryAuthorizeAsync();
            return isSuccess;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while refreshing token");
            return false;
        }
    }

}
