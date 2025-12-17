using System.Text;
using System.Text.Json;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Models.Base;

namespace CitationReader.Readers.Base;

public abstract class BaseHttpReader
{
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 1000;
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClientType _httpClientType;
    
    public BaseHttpReader(HttpClientType httpClientType)
    {
        _httpClientType = httpClientType;
        _httpClientFactory = Program.ServiceProvider.GetService<IHttpClientFactory>()!;
        Logger = Program.ServiceProvider.GetService<ILogger<BaseHttpReader>>()!;
    }
    
    protected readonly ILogger Logger;
    
    protected async Task<BaseResponse<T>> RequestAsync<T>(
        HttpMethod method,
        string url,
        object? requestBody = null) where T : class
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var delay = RetryDelayMs * (int)Math.Pow(2, attempt);
            try
            {
                var result = await ExecuteRequestAsync<T>(
                    method,
                    url, 
                    requestBody);
                if (result.IsSuccess)
                {
                    return result;
                }

                if (attempt >= MaxRetries - 1)
                {
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

        return await ExecuteRequestAsync<T>(method, url, requestBody);
    }

    private async Task<BaseResponse<T>> ExecuteRequestAsync<T>(
        HttpMethod method,
        string url,
        object? requestBody = null) where T : class
    {
        Logger.LogInformation("Making {Method} request to {Uri}", method.Method, url);

        try
        {
            using var request = new HttpRequestMessage(method, url);

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
                    var result = JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = false
                    });

                    if (result != null)
                    {
                        Logger.LogInformation("Successfully deserialized response to {Type}", typeof(T).Name);
                        return BaseResponse<T>.Success(result);
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
}