using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ApiTester.Web.AI;

public sealed class AiRateLimiter
{
    private readonly ConcurrentDictionary<Guid, TokenBucket> _buckets = new();
    private readonly AiRateLimitOptions _options;
    private readonly TimeProvider _timeProvider;

    public AiRateLimiter(IOptions<AiRateLimitOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value ?? new AiRateLimitOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryConsume(Guid organisationId, int tokens = 1)
    {
        if (tokens <= 0)
            return true;

        var bucket = _buckets.GetOrAdd(organisationId, _ =>
            new TokenBucket(_options.Capacity, _options.RefillTokensPerMinute, _timeProvider));

        return bucket.TryConsume(tokens);
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
            _capacity = Math.Max(1, capacity);
            _refillPerMinute = Math.Max(1, refillPerMinute);
            _timeProvider = timeProvider;
            _tokens = _capacity;
            _lastRefill = _timeProvider.GetUtcNow();
        }

        public bool TryConsume(int tokens)
        {
            lock (_lock)
            {
                Refill();
                if (_tokens < tokens)
                    return false;

                _tokens -= tokens;
                return true;
            }
        }

        private void Refill()
        {
            var now = _timeProvider.GetUtcNow();
            var elapsed = (now - _lastRefill).TotalMinutes;
            if (elapsed <= 0)
                return;

            _tokens = Math.Min(_capacity, _tokens + elapsed * _refillPerMinute);
            _lastRefill = now;
        }
    }
}
