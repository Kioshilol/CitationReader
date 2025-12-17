using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;

namespace CitationReader.Exceptions;

public class CitationException : Exception
{
    public string CarDetails { get; }
    public string State { get; }
    public int ErrorCode { get; }
    public Dictionary<string, object>? AdditionalDetails { get; }

    public CitationException(string message) : base(message)
    {
        CarDetails = string.Empty;
        State = string.Empty;
        ErrorCode = 0;
    }

    public CitationException(string message, string carDetails, string state = "", int errorCode = 0) 
        : base(message)
    {
        CarDetails = carDetails;
        State = state;
        ErrorCode = errorCode;
    }

    public CitationException(string message, Exception innerException) : base(message, innerException)
    {
        CarDetails = string.Empty;
        State = string.Empty;
        ErrorCode = 0;
    }

    public CitationException(string message, string carDetails, string state, int errorCode, Exception innerException) 
        : base(message, innerException)
    {
        CarDetails = carDetails;
        State = state;
        ErrorCode = errorCode;
    }

    public CitationException(CitationError error) : base(error.Message)
    {
        CarDetails = error.CarDetails;
        State = string.Empty;
        ErrorCode = error.ErrorCode;
        AdditionalDetails = error.AdditionalDetails;
    }

    public CitationException(CitationError error, string state) : base(error.Message)
    {
        CarDetails = error.CarDetails;
        State = state;
        ErrorCode = error.ErrorCode;
        AdditionalDetails = error.AdditionalDetails;
    }

    public CitationError ToCitationError()
    {
        var error = new CitationError
        {
            Message = Message,
            CarDetails = CarDetails,
            ErrorCode = ErrorCode,
            Timestamp = DateTime.UtcNow,
            AdditionalDetails = AdditionalDetails
        };

        if (InnerException != null)
        {
            error.AddDetail("InnerException", InnerException.Message);
            if (!string.IsNullOrEmpty(InnerException.StackTrace))
            {
                error.AddDetail("StackTrace", InnerException.StackTrace);
            }
        }

        return error;
    }

    public override string ToString()
    {
        var details = new List<string> { base.ToString() };
        
        if (!string.IsNullOrEmpty(CarDetails))
            details.Add($"Car Details: {CarDetails}");
        
        if (!string.IsNullOrEmpty(State))
            details.Add($"State: {State}");
        
        if (ErrorCode != 0)
            details.Add($"Error Code: {ErrorCode}");

        if (AdditionalDetails?.Any() == true)
        {
            details.Add("Additional Details:");
            foreach (var detail in AdditionalDetails)
            {
                details.Add($"  {detail.Key}: {detail.Value}");
            }
        }

        return string.Join(Environment.NewLine, details);
    }
}
