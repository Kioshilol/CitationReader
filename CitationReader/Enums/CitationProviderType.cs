using System.ComponentModel.DataAnnotations;

namespace CitationReader.Enums;

public enum CitationProviderType
{
    [Display(Name = "Vanguard")]
    Vanguard,
    
    [Display(Name = "Professional Parking Management")]
    ProfessionalParkingManagement,
    
    [Display(Name = "Metropolis")]
    Metropolis,
    
    [Display(Name = "City of Fort Lauderdale")]
    CityOfFortLauderdale,
    
    [Display(Name = "Miami Parking")]
    MiamiParking,
    
    [Display(Name = "Parking Compliance")]
    ParkingCompliance
}
