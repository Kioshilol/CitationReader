namespace CitationReader.Services.Huur.Auth
{
    public interface IAuthService
    {
        Task<bool> TryAuthorizeAsync();
    }
}
