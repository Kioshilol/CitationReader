using CitationReader.Models.Citation.Internal;
using CitationReader.Models.Huur;

namespace CitationReader.Mappers;

public class CitationMapper : ICitationMapper
{
    private readonly ILogger<CitationMapper> _logger;

    public CitationMapper(ILogger<CitationMapper> logger)
    {
        _logger = logger;
    }

    public ParkingViolation MapToParkingViolation(CitationModel citation, Dictionary<string, ExternalVehicleDto> vehicleLookup)
    {
        // Find the matching vehicle to get the provider value
        var vehicleKey = $"{citation.Tag}_{citation.State}";
        var provider = 0; // Default value
        
        if (vehicleLookup.TryGetValue(vehicleKey, out var vehicle))
        {
            provider = vehicle.Provider;
        }
        else
        {
            _logger.LogWarning(
                "Could not find matching vehicle for citation {CitationNumber} with tag {Tag} and state {State}. Using default provider value.", 
                citation.CitationNumber, 
                citation.Tag, 
                citation.State);
        }
        
        return new ParkingViolation
        {
            Id = citation.Id,
            CitationNumber = citation.CitationNumber,
            NoticeNumber = citation.NoticeNumber,
            Provider = provider, // Use the ExternalVehicle.Provider value
            Agency = citation.Agency,
            Address = citation.Address,
            Tag = citation.Tag,
            State = citation.State,
            IssueDate = citation.IssueDate,
            StartDate = citation.StartDate,
            EndDate = citation.EndDate,
            Amount = citation.Amount,
            Currency = citation.Currency,
            PaymentStatus = citation.PaymentStatus,
            FineType = citation.FineType,
            Note = citation.Note,
            Link = citation.Link,
            IsActive = citation.IsActive
        };
    }
}
