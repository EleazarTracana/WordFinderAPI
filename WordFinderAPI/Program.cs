using FluentValidation;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WordFinderAPI;
using WordFinderAPI.Core.Models;
using WordFinderAPI.Infrastructure.CacheProvider;
using WordFinderAPI.Interfaces;
using WordFinderAPI.Interfaces.Cache;
using WordFinderAPI.Services;
using WordFinderAPI.Services.Cache;
using WordFinderAPI.Services.Validator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Business WordFinderAPI.Services.
builder.Services.AddScoped<IWordFinderService, WordFinderService>();

// Fluent Validation.
builder.Services.AddScoped<IValidator<WordFinder>, WordFinderValidator>();

// Cache.
builder.Services.Configure<RedisOptions>(options => builder.Configuration.GetSection(RedisOptions.Redis).Bind(options));
builder.Services.AddScoped<ICacheProvider, RedisCacheProvider>();
builder.Services.AddScoped<IWordFinderCache, WordFinderCache>();

builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthorization(o => o.AddPolicy("IsAdmin", b =>
{
    b.RequireRealmRoles("admin");
    b.RequireResourceRoles("r-admin"); // stands for "resource admin"
    b.RequireRole("r-admin"); 
}));

builder.Services.AddKeycloakAuthorization(builder.Configuration);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature.Error;
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(exception));
    });
});

app.MapPost("/api/word-finder", (PostWordFinder)).RequireAuthorization();
Task<IResult> PostWordFinder([FromBody] WordFinder wordFinder, IWordFinderService wordFinderService)
{
    IEnumerable<string> wordsFound = wordFinderService.Find(wordFinder);
    return Task.FromResult(Results.Ok(wordsFound));
};

app.Run();