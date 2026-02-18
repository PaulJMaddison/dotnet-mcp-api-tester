using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ApiTester.Web.AbuseProtection;

public sealed class TenantIpRateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly AbuseRateLimitOptions _options;
    private readonly TimeProvider _timeProvider;

    public TenantIpRateLimiter(IOptions<AbuseRateLimitOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value ?? new AbuseRateLimitOptions();
        _timeProvider = timeProvider;
    }

    public bool TryConsume(Guid tenantId, string ipAddress, EndpointCategory category, out TimeSpan retryAfter)
    {
        var policy = ResolvePolicy(category);
        var key = $"{tenantId:N}:{ipAddress}:{category}";
        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(policy.Capacity, policy.RefillTokensPerMinute, _timeProvider));

        return bucket.TryConsume(1, out retryAfter);
    }

    private EndpointRateLimitPolicy ResolvePolicy(EndpointCategory category)
    {
        var configured = category switch
        {
            EndpointCategory.Ai => _options.Ai,
            EndpointCategory.RunExecution => _options.RunExecution,
            _ => _options.Default
        };

        return new EndpointRateLimitPolicy(
            Math.Max(1, configured.Capacity),
            Math.Max(1, configured.RefillTokensPerMinute));
    }

    private sealed class TokenBucket
    {
        private readonly int _capacity;
        private readonly int _refillPerMinute;
        private readonly TimeProvider _timeProvider;
        private readonly object _lock = new();
        private double _tokens;
        private DateTimeOffset _lastRefill;

        public TokenBucket(int capacity, int refillPerMinute, TimeProvider timeProvider)
        {
            _capacity = capacity;
            _refillPerMinute = refillPerMinute;
            _timeProvider = timeProvider;
            _tokens = _capacity;
            _lastRefill = _timeProvider.GetUtcNow();
        }

        public bool TryConsume(int tokens, out TimeSpan retryAfter)
        {
            lock (_lock)
            {
                Refill();
                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    retryAfter = TimeSpan.Zero;
                    return true;
                }

                var tokenShortfall = tokens - _tokens;
                var refillPerSecond = _refillPerMinute / 60d;
                var seconds = refillPerSecond <= 0
                    ? 60
                    : Math.Ceiling(tokenShortfall / refillPerSecond);

                retryAfter = TimeSpan.FromSeconds(Math.Max(1, seconds));
                return false;
            }
        }

        private void Refill()
        {
            var now = _timeProvider.GetUtcNow();
            var elapsed = (now - _lastRefill).TotalMinutes;
            if (elapsed <= 0)
                return;

            _tokens = Math.Min(_capacity, _tokens + (elapsed * _refillPerMinute));
            _lastRefill = now;
        }
    }
}
