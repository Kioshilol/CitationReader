using CitationReader.Enums;
using CitationReader.Exceptions;
using CitationReader.Models.Huur;

namespace CitationReader.Models.Citation;

public class BulkCitationResponse
{
    public bool IsSuccess { get; set; }
    public List<CitationDto> Citations { get; set; } = new();
    public List<CitationProcessingError> Errors { get; set; } = new();
    public BulkProcessingSummary Summary { get; set; } = new();
    public string? FatalError { get; set; }

    public static BulkCitationResponse CreateFatalError(string error)
    {
        return new BulkCitationResponse
        {
            IsSuccess = false,
            FatalError = error,
            Summary = new BulkProcessingSummary
            {
                TotalVehicles = 0,
                TotalProviders = 0,
                SuccessfulOperations = 0,
                FailedOperations = 1,
                ProcessingStartTime = DateTime.UtcNow,
                ProcessingEndTime = DateTime.UtcNow
            }
        };
    }

    public static BulkCitationResponse CreateSuccess(
        List<CitationDto> citations, 
        List<CitationProcessingError> errors,
        BulkProcessingSummary summary)
    {
        return new BulkCitationResponse
        {
            IsSuccess = true,
            Citations = citations,
            Errors = errors,
            Summary = summary
        };
    }
}

public class CitationProcessingError
{
    public string VehicleTag { get; set; } = string.Empty;
    public string VehicleState { get; set; } = string.Empty;
    public CitationType Provider { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? AdditionalDetails { get; set; }

    public static CitationProcessingError FromException(
        string vehicleTag, 
        string vehicleState, 
        CitationType provider, 
        Exception exception)
    {
        var error = new CitationProcessingError
        {
            VehicleTag = vehicleTag,
            VehicleState = vehicleState,
            Provider = provider,
            ErrorMessage = exception.Message,
            Timestamp = DateTime.UtcNow,
            AdditionalDetails = new Dictionary<string, object>()
        };

        if (exception is CitationException citationEx)
        {
            error.ErrorCode = citationEx.ErrorCode;
            if (citationEx.AdditionalDetails != null)
            {
                foreach (var detail in citationEx.AdditionalDetails)
                {
                    error.AdditionalDetails[detail.Key] = detail.Value;
                }
            }
        }
        else
        {
            error.ErrorCode = -1;
            error.AdditionalDetails["ExceptionType"] = exception.GetType().Name;
            if (exception.InnerException != null)
            {
                error.AdditionalDetails["InnerException"] = exception.InnerException.Message;
            }
        }

        return error;
    }

    public static CitationProcessingError FromCitationError(
        string vehicleTag,
        string vehicleState,
        CitationType provider,
        CitationError citationError)
    {
        return new CitationProcessingError
        {
            VehicleTag = vehicleTag,
            VehicleState = vehicleState,
            Provider = provider,
            ErrorMessage = citationError.Message,
            ErrorCode = citationError.ErrorCode,
            Timestamp = citationError.Timestamp,
            AdditionalDetails = citationError.AdditionalDetails
        };
    }
}

public class BulkProcessingSummary
{
    public int TotalVehicles { get; set; }
    public int TotalProviders { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public int TotalCitations { get; set; }
    public DateTime ProcessingStartTime { get; set; }
    public DateTime ProcessingEndTime { get; set; }
    public TimeSpan ProcessingDuration => ProcessingEndTime - ProcessingStartTime;
    public Dictionary<CitationType, int> CitationsByProvider { get; set; } = new();
    public Dictionary<CitationType, int> ErrorsByProvider { get; set; } = new();

    public double SuccessRate => TotalProviders * TotalVehicles == 0 ? 0 : 
        (double)SuccessfulOperations / (TotalProviders * TotalVehicles) * 100;
}
