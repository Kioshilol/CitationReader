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
    CityOfFortLauderdale
}
