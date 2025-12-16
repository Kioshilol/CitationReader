using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CitationReader.Providers.Cache
{
    public class TokenCacheProvider : ITokenCacheProvider
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<TokenCacheProvider> _logger;
        private const string TokenCacheKey = "auth_token";
        private const string ExpirationCacheKey = "auth_token_expiration";

        public TokenCacheProvider(IMemoryCache memoryCache, ILogger<TokenCacheProvider> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public void CacheToken(string token, DateTime expiration)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Attempted to cache null or empty token");
                return;
            }

            _logger.LogInformation("Caching token with expiration: {Expiration}", expiration);
            
            _memoryCache.Set(TokenCacheKey, token, expiration);
            _memoryCache.Set(ExpirationCacheKey, expiration, expiration);
        }

        public string? GetCachedToken()
        {
            var token = _memoryCache.Get<string>(TokenCacheKey);
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No cached token found");
                return null;
            }

            if (!IsTokenValid())
            {
                _logger.LogInformation("Cached token has expired, clearing cache");
                ClearToken();
                return null;
            }

            _logger.LogDebug("Retrieved valid cached token");
            return token;
        }

        public bool IsTokenValid()
        {
            var expiration = _memoryCache.Get<DateTime?>(ExpirationCacheKey);
            
            if (!expiration.HasValue)
            {
                _logger.LogDebug("No token expiration found in cache");
                return false;
            }

            var isValid = DateTime.UtcNow < expiration.Value;
            _logger.LogDebug("Token validity check: {IsValid}, expires at: {Expiration}", isValid, expiration.Value);
            
            return isValid;
        }

        public void ClearToken()
        {
            try
            {
                _logger.LogInformation("Clearing cached token");

                _memoryCache.Remove(TokenCacheKey);
                _memoryCache.Remove(ExpirationCacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache token");
            }
        }
    }
}
