﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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
        services.AddSingleton(TimeProvider.System);
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
        var group = builder.MapGroup("/api/items")
                           .AddEndpointFilter<RateLimitFilter>()
                           .RequireAuthorization();
        {
            // Get all Todo items
            group.MapGet("/", async (
                ClaimsPrincipal user,
                ITodoService service,
                CancellationToken cancellationToken) =>
                {
                    return Results.Ok(await service.GetListAsync(user.GetUserId(), cancellationToken));
                })
                .ProducesProblem(StatusCodes.Status429TooManyRequests);

            // Get a specific Todo item
            group.MapGet("/{id}", async (
                Guid id,
                ClaimsPrincipal user,
                ITodoService service,
                CancellationToken cancellationToken) =>
                {
                    var model = await service.GetAsync(user.GetUserId(), id, cancellationToken);
                    return model is null ? Results.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound) : Results.Json(model);
                })
                .Produces<TodoItemModel>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);

            // Create a new Todo item
            group.MapPost("/", async (
                CreateTodoItemModel model,
                ClaimsPrincipal user,
                ITodoService service,
                CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(model.Text))
                    {
                        return Results.Problem("No item text specified.", statusCode: StatusCodes.Status400BadRequest);
                    }

                    var id = await service.AddItemAsync(user.GetUserId(), model.Text, cancellationToken);
                    return Results.Created($"/api/items/{id}", new { id });
                })
                .Produces(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);

            // Mark a Todo item as completed
            group.MapPost("/{id}/complete", async (
                Guid id,
                ClaimsPrincipal user,
                ITodoService service,
                CancellationToken cancellationToken) =>
                {
                    var wasCompleted = await service.CompleteItemAsync(user.GetUserId(), id, cancellationToken);

                    return wasCompleted switch
                    {
                        true => Results.NoContent(),
                        false => Results.Problem("Item already completed.", statusCode: StatusCodes.Status400BadRequest),
                        _ => Results.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound),
                    };
                })
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);

            // Delete a Todo item
            group.MapDelete("/{id}", async (
                Guid id,
                ClaimsPrincipal user,
                ITodoService service,
                CancellationToken cancellationToken) =>
                {
                    var wasDeleted = await service.DeleteItemAsync(user.GetUserId(), id, cancellationToken);
                    return wasDeleted ? Results.NoContent() : Results.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound);
                })
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);
        }

        return builder;
    }
}
