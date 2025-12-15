using CitationReader.Models.Huur.Requests;

namespace CitationReader.Services.Huur.Auth
{
    public interface IAuthService
    {
        Task<bool> TrySignInAsync(SignInRequest request);
    }
}
