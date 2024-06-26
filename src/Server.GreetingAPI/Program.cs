using Microsoft.AspNetCore.Mvc;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
  .ConfigureResource(resource => resource.AddService("dotnet-frontend"))
  .WithTracing(tpb =>
  {
    tpb.AddAspNetCoreInstrumentation();
    tpb.AddConsoleExporter();
  });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI(config =>
  {
    config.SwaggerEndpoint("/swagger/v1/swagger.json", "Server Greeting API");
    config.RoutePrefix = string.Empty;
  });
}

app.UseHttpsRedirection();

app.MapGet("/greeting", ([FromQuery] string firstName, [FromQuery] string surname, [FromServices] ILogger<Program> logger) =>
{
  logger.LogInformation("Greeting endpoint called: {firstName} {surname}", firstName, surname);
  return $"Hello {firstName} {surname}";
})
.WithName("Greeting")
.WithOpenApi();

app.Run();