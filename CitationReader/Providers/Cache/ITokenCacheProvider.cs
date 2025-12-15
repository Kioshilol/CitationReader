namespace CitationReader.Providers.Cache
{
    public interface ITokenCacheProvider
    {
        void CacheToken(string token, DateTime expiration);
        string? GetCachedToken();
        bool IsTokenValid();
        void ClearToken();
    }
}
