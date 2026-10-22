
using Microsoft.OpenApi.Models;
using CoffeeNChill.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

// Register API controllers
builder.Services.AddControllers();

// Register Azure Table Storage user service
builder.Services.AddScoped<IUserService, AzureTableUserService>();

// Configure Swagger/OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoffeeNChill API Gateway",
        Version = "v1",
        Description = "Central API Gateway for CoffeeNChill menu, orders, documents and authentication."
    });

    // Include XML documentation when available
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Enable Swagger for local development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware
app.UseHttpsRedirection();

app.UseAuthorization();

// Map API endpoints
app.MapControllers();

app.Run();
