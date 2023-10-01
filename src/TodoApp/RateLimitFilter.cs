// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.RateLimit;

namespace TodoApp;

public sealed class RateLimitFilter(IConfiguration configuration, IMemoryCache cache) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var userId = context.HttpContext.User.GetUserId();
        var operation = HttpMethods.IsGet(context.HttpContext.Request.Method) ? "Read" : "Write";

        var rateLimit = GetRateLimitPolicy(userId, operation);

        try
        {
            return await rateLimit.ExecuteAsync(async () => await next(context));
        }
        catch (RateLimitRejectedException ex)
        {
            return Results.Extensions.RateLimited(ex.RetryAfter);
        }
    }

    private AsyncPolicy GetRateLimitPolicy(string userId, string operation)
    {
        // Shard the rate limit policies by operation and user ID
        string key = $"{operation}-RateLimit-{userId}";

        // So we don't have policies per-user building up in memory indefinitely,
        // se an IMemoryCache to store the policies instead of a Policy Registry.
        return cache.GetOrCreate(key, (entry) =>
        {
            var numberOfExecutions = configuration.GetValue<int>($"RateLimiting:{operation}:Executions");
            var perTimeSpan = configuration.GetValue<TimeSpan>($"RateLimiting:{operation}:PerTimeSpan");
            var maxBurst = configuration.GetValue<int>($"RateLimiting:{operation}:MaxBurst");

            // If the Policy is not accessed again by the time the period elapses, we can
            // safely remove it from the cache and instead create a new one for further requests.
            entry.SlidingExpiration = perTimeSpan * 2;

            return Policy
                .RateLimitAsync(numberOfExecutions, perTimeSpan, maxBurst)
                .WithPolicyKey($"{operation} rate-limit for user {userId}");
        })!;
    }
}
