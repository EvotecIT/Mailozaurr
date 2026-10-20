using System.Collections.Concurrent;
using System.Threading;

namespace Mailozaurr;

// Bounded lock stripes avoid retaining a semaphore for every rotated credential.
internal sealed class GraphTokenMemoryCache {
    private readonly ConcurrentDictionary<string, GraphAuthorization> _tokens;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    internal GraphTokenMemoryCache(ConcurrentDictionary<string, GraphAuthorization>? tokens = null) {
        _tokens = tokens ?? new ConcurrentDictionary<string, GraphAuthorization>();
    }

    internal async Task<GraphAuthorization> GetAsync(GraphCredential credential, string tenant, string resource,
        HttpClient client, TimeSpan skew, CancellationToken cancellationToken) {
        string key = MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, tenant, resource);
        SemaphoreSlim gate = _gates[(key.GetHashCode() & int.MaxValue) % _gates.Length];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tokens.TryGetValue(key, out GraphAuthorization? cached) &&
                cached.ExpiresOn - DateTimeOffset.UtcNow > skew) {
                return cached;
            }
            _tokens.TryRemove(key, out _);
            GraphAuthorization authorization = await MicrosoftGraphUtils.AcquireClientCredentialsAuthorizationAsync(
                credential, tenant, resource, client, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (authorization.ExpiresOn > DateTimeOffset.UtcNow) {
                _tokens[key] = authorization;
            }
            return authorization;
        } finally {
            gate.Release();
        }
    }
}
