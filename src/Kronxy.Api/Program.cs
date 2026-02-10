using Kronxy.Api.Extensions;
using Kronxy.Application;
using Kronxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger (opcional, no afecta endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Kronxy API",
        Version = "v1"
    });
});

// Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Swagger (opcional)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kronxy API v1");
});

// Solo DEV: migraciones/seed
if (app.Environment.IsDevelopment())
{
    app.ApplyMigrations();
    // app.SeedData();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCustomExceptionHandler();

app.MapControllers();

app.Run();
