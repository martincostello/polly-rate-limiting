// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using TodoApp.Data;
using TodoApp.Models;
using TodoApp.Services;

namespace TodoApp;

/// <summary>
/// A class containing the HTTP endpoints for the Todo API.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Adds the services for the Todo API to the application.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>
    /// A <see cref="IServiceCollection"/> that can be used to further configure the application.
    /// </returns>
    public static IServiceCollection AddTodoApi(this IServiceCollection services)
    {
        services.AddSingleton<IClock>(_ => SystemClock.Instance);
        services.AddSingleton<RateLimiter>();
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<ITodoService, TodoService>();

        services.AddDbContext<TodoContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var dataDirectory = configuration["DataDirectory"];

            if (string.IsNullOrEmpty(dataDirectory) || !Path.IsPathRooted(dataDirectory))
            {
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
                dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
            }

            // Ensure the configured data directory exists
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            var databaseFile = Path.Combine(dataDirectory, "TodoApp.db");

            options.UseSqlite("Data Source=" + databaseFile);
        });

        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Maps the endpoints for the Todo API.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <returns>
    /// A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.
    /// </returns>
    public static IEndpointRouteBuilder MapTodoApiRoutes(this IEndpointRouteBuilder builder)
    {
        const string ReadOperation = "Read";
        const string WriteOperation = "Write";

        // Get all Todo items
        builder.MapGet("/api/items", async (
            ClaimsPrincipal user,
            ITodoService service,
            RateLimiter rateLimiter,
            CancellationToken cancellationToken) =>
            {
                return await rateLimiter.LimitAsync(user.GetUserId(), ReadOperation, async userId =>
                    Results.Ok(await service.GetListAsync(userId, cancellationToken)));
            })
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();

        // Get a specific Todo item
        builder.MapGet("/api/items/{id}", async (
            Guid id,
            ClaimsPrincipal user,
            ITodoService service,
            RateLimiter rateLimiter,
            CancellationToken cancellationToken) =>
            {
                return await rateLimiter.LimitAsync(user.GetUserId(), ReadOperation, async userId =>
                {
                    var model = await service.GetAsync(userId, id, cancellationToken);
                    return model is null ? Results.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound) : Results.Json(model);
                });
            })
            .Produces<TodoItemModel>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();

        // Create a new Todo item
        builder.MapPost("/api/items", async (
            CreateTodoItemModel model,
            ClaimsPrincipal user,
            ITodoService service,
            RateLimiter rateLimiter,
            CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(model.Text))
                {
                    return Results.Problem("No item text specified.", statusCode: StatusCodes.Status400BadRequest);
                }

                return await rateLimiter.LimitAsync(user.GetUserId(), WriteOperation, async userId =>
                {
                    var id = await service.AddItemAsync(userId, model.Text, cancellationToken);
                    return Results.Created($"/api/items/{id}", new { id });
                });
            })
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();

        // Mark a Todo item as completed
        builder.MapPost("/api/items/{id}/complete", async (
            Guid id,
            ClaimsPrincipal user,
            ITodoService service,
            RateLimiter rateLimiter,
            CancellationToken cancellationToken) =>
            {
                return await rateLimiter.LimitAsync(user.GetUserId(), WriteOperation, async userId =>
                {
                    var wasCompleted = await service.CompleteItemAsync(userId, id, cancellationToken);

                    return wasCompleted switch
                    {
                        true => Results.NoContent(),
                        false => Results.Problem("Item already completed.", statusCode: StatusCodes.Status400BadRequest),
                        _ => Results.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound),
                    };
                });
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();

        // Delete a Todo item
        builder.MapDelete("/api/items/{id}", async (
            Guid id,
            ClaimsPrincipal user,
            ITodoService service,
            RateLimiter rateLimiter,
            CancellationToken cancellationToken) =>
            {
                return await rateLimiter.LimitAsync(user.GetUserId(), WriteOperation, async userId =>
                {
                    var wasDeleted = await service.DeleteItemAsync(userId, id, cancellationToken);
                    return wasDeleted ? Results.NoContent() : Results.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound);
                });
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();

        return builder;
    }
}
