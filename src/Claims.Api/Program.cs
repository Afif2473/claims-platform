using System.Text.Json.Serialization;
using Claims.Api.Data;
using Claims.Api.Errors;
using Claims.Api.Interfaces;
using Claims.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as strings ("Motor", "UnderReview") so the API contract is readable and order-independent.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Configure SQLite and Entity Framework Core
builder.Services.AddDbContext<ClaimsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ClaimsDatabase")));

// Application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IClaimService, ClaimService>();
builder.Services.AddScoped<IClaimWorkflowService, ClaimWorkflowService>();

// Consistent RFC 7807 error responses
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Configure OpenAPI, and declare the X-User-Id header so Swagger UI shows an "Authorize" button for it.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["UserId"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = CurrentUserService.HeaderName,
        Description = "Simulated identity: paste a seeded user id (see GET /api/users)."
    };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("UserId", document)] = [] }];
    return Task.CompletedTask;
}));

var app = builder.Build();

// Apply pending migrations and seed demo users at startup, so the app runs locally with no manual DB step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Generates the OpenAPI v1 JSON document endpoint at /openapi/v1.json
    app.MapOpenApi();

    // Serves the interactive Swagger UI at /swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Claims API v1");
        options.RoutePrefix = "swagger";
    });

    // Convenient redirect from root to Swagger UI during development
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
