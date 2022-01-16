// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.RateLimit;

namespace TodoApp.Services;

public sealed class RateLimiter
{
    public RateLimiter(IConfiguration configuration, IMemoryCache cache)
    {
        Configuration = configuration;
        Cache = cache;
    }

    private IMemoryCache Cache { get; }

    private IConfiguration Configuration { get; }

    public async Task<IResult> LimitAsync(string userId, string operation, Func<string, Task<IResult>> action)
    {
        var rateLimit = GetRateLimitPolicy(userId, operation);

        try
        {
            return await rateLimit.ExecuteAsync(() => action(userId));
        }
        catch (RateLimitRejectedException)
        {
            return Results.Problem("Too many requests.", statusCode: StatusCodes.Status429TooManyRequests);
        }
    }

    private AsyncPolicy GetRateLimitPolicy(string userId, string operation)
    {
        // Shard the rate limit policies by operation and user ID
        string key = $"{operation}-RateLimit-{userId}";

        // So we don't have policies per-user building up in memory indefinitely,
        // se an IMemoryCache to store the policies instead of a Policy Registry.
        return Cache.GetOrCreate(key, (entry) =>
        {
            var numberOfExecutions = Configuration.GetValue<int>($"RateLimiting:{operation}:Executions");
            var perTimeSpan = Configuration.GetValue<TimeSpan>($"RateLimiting:{operation}:PerTimeSpan");
            var maxBurst = Configuration.GetValue<int>($"RateLimiting:{operation}:MaxBurst");

            // If the Policy is not accessed again by the time the period elapses, we can
            // safely remove it from the cache and instead create a new one for further requests.
            entry.SlidingExpiration = perTimeSpan * 2;

            return Policy
                .RateLimitAsync(numberOfExecutions, perTimeSpan, maxBurst)
                .WithPolicyKey($"{operation} rate-limit for user {userId}");
        });
    }
}
