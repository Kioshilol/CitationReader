using CitationReader.Managers.Huur.Auth;
using CitationReader.Models.Huur.Requests;
using CitationReader.Providers.Cache;

namespace CitationReader.Services.Huur.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IAuthManager _authManager;
        private readonly ITokenCacheProvider _tokenCacheProvider;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IAuthManager authManager, 
            ITokenCacheProvider tokenCacheProvider,
            ILogger<AuthService> logger)
        {
            _authManager = authManager;
            _tokenCacheProvider = tokenCacheProvider;
            _logger = logger;
        }

        public async Task<bool> TrySignInAsync(SignInRequest request)
        {
            var response = await _authManager.SignInAsync(request);
            var result = response.Result;
            if (!response.IsSuccess || result is null)
            { 
                return false;
            }
            
            _logger.LogInformation("Sign-in successful for user: {Email}", request.Email);
            _tokenCacheProvider.CacheToken(
                result.Token,
                result.TokenExpired);
            return true;
        }
    }
}
