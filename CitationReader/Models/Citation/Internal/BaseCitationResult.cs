using CitationReader.Enums;

namespace CitationReader.Models.Citation.Internal;

public class BaseCitationResult<T>
{
    public bool Success { get; set; }

    public T? Data { get; set; }

    public CitationError? Error { get; set; }

    public string State { get; set; } = string.Empty;

    public bool IsSuccess => Success && Error == null;

    public bool HasError => !Success || Error != null;

    public static BaseCitationResult<T> CreateSuccess(T data, string state = "")
    {
        return new BaseCitationResult<T>
        {
            Success = true,
            Data = data,
            Error = null,
            State = state
        };
    }

    public static BaseCitationResult<T> CreateError(
        string errorMessage,
        CitationProviderType providerType,
        string carDetails = "", 
        string state = "",
        int errorCode = 0)
    {
        return new BaseCitationResult<T>
        {
            Success = false,
            Data = default,
            Error = new CitationError
            {
                Message = errorMessage,
                CarDetails = carDetails,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow,
                CitationProviderType = providerType
            },
            State = state
        };
    }
}
