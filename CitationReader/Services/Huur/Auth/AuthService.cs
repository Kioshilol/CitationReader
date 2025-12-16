using CitationReader.Configuration;
using CitationReader.Managers.Huur.Auth;
using CitationReader.Models.Huur.Requests;
using CitationReader.Providers.Cache;
using Microsoft.Extensions.Options;

namespace CitationReader.Services.Huur.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IAuthManager _authManager;
        private readonly ITokenCacheProvider _tokenCacheProvider;
        private readonly ILogger<AuthService> _logger;
        private readonly HuurOptions _options;

        public AuthService(
            IAuthManager authManager, 
            ITokenCacheProvider tokenCacheProvider,
            ILogger<AuthService> logger,
            IOptions<HuurOptions> options)
        {
            _authManager = authManager;
            _tokenCacheProvider = tokenCacheProvider;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<bool> TryAuthorizeAsync()
        {
            _tokenCacheProvider.ClearToken();
            
            var request = new SignInRequest(
                _options.Auth.Email, 
                _options.Auth.Password);
            var response = await _authManager.AuthorizeAsync(request);
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
