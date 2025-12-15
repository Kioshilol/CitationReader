namespace CitationReader.Models.Huur;

public class ExternalVehicleDto
{
    public int Id { get; set; }

    public string? Label { get; set; }

    public string? Vin { get; set; }

    public string? Tag { get; set; }

    public string? State { get; set; }

    public string? OwnerId { get; set; }

    public string? LicensePlate { get; set; }

    public string? ProviderVehicleId { get; set; }

    public int Provider { get; set; }

    public string? Country { get; set; }

    public int? Year { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? Color { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}