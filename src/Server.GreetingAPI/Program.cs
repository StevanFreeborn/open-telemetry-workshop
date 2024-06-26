using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
  .ConfigureResource(resource =>
  {
    resource.AddService("dotnet-backend");
    resource.AddAttributes(new Dictionary<string, object>()
    {
      { "environment", builder.Environment.EnvironmentName },
    });
  })
  .WithTracing(tracing =>
  {
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddConsoleExporter();
    tracing.AddOtlpExporter();
    tracing.AddOtlpExporter(opt =>
    {
      opt.Endpoint = new Uri("http://localhost:5341/ingest/otlp/v1/traces");
      opt.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
  })
  .WithMetrics(metrics =>
  {
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddOtlpExporter();
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

app
  .MapGet("/greeting", ([FromQuery] string firstName, [FromQuery] string surname, [FromServices] ILogger<Program> logger) =>
  {
    Activity.Current?.SetTag("firstName", firstName);
    Activity.Current?.SetTag("surname", surname);
    logger.LogInformation("Greeting endpoint called: {firstName} {surname}", firstName, surname);
    return $"Hello {firstName} {surname}";
  })
  .WithName("Greeting")
  .WithOpenApi();

app.Run();