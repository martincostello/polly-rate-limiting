// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;

namespace TodoApp;

public static class ResultExtensions
{
    public static IResult RateLimited(this IResultExtensions extensions, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        return new RateLimitedResult(retryAfter);
    }

    private sealed class RateLimitedResult : IResult
    {
        private readonly TimeSpan _retryAfter;

        public RateLimitedResult(TimeSpan retryAfter)
        {
            _retryAfter = retryAfter;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            var value = new ProblemDetails
            {
                Title = "Too Many Requests",
                Detail = "Too many requests.",
                Status = StatusCodes.Status429TooManyRequests,
            };

            int retryAfterSeconds = Math.Max(1, (int)_retryAfter.TotalSeconds);

            httpContext.Response.StatusCode = value.Status.Value;
            httpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            return httpContext.Response.WriteAsJsonAsync(value, value.GetType(), options: null, contentType: "application/problem+json");
        }
    }
}
