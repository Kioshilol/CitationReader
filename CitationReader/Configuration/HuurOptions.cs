namespace CitationReader.Configuration
{
    public class HuurOptions
    {
        public const string SectionName = "Huur";
        
        public string BaseUrl { get; set; } = string.Empty;
        public AuthOptions Auth { get; set; } = new();
    }

    public class AuthOptions
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
