using System.Text.Json.Serialization;

namespace CitationReader.Models.Citation;

public class BaseCitationResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    public CitationError? Error { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsSuccess => Success && Error == null;

    [JsonIgnore]
    public bool HasError => !Success || Error != null;

    public static BaseCitationResponse<T> CreateSuccess(T data, string state = "")
    {
        return new BaseCitationResponse<T>
        {
            Success = true,
            Data = data,
            Error = null,
            State = state
        };
    }

    public static BaseCitationResponse<T> CreateError(string errorMessage, string carDetails = "", string state = "", int errorCode = 0)
    {
        return new BaseCitationResponse<T>
        {
            Success = false,
            Data = default,
            Error = new CitationError
            {
                Message = errorMessage,
                CarDetails = carDetails,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            },
            State = state
        };
    }

    public static BaseCitationResponse<T> CreateError(CitationError error, string state = "")
    {
        return new BaseCitationResponse<T>
        {
            Success = false,
            Data = default,
            Error = error,
            State = state
        };
    }
}

public class CitationError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("carDetails")]
    public string CarDetails { get; set; } = string.Empty;

    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("additionalDetails")]
    public Dictionary<string, object>? AdditionalDetails { get; set; }

    public void AddDetail(string key, object value)
    {
        AdditionalDetails ??= new Dictionary<string, object>();
        AdditionalDetails[key] = value;
    }
}
