using System.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Instrumentation>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.AddOpenTelemetry(o => o.AddOtlpExporter());

builder.Services.AddOpenTelemetry()
  .ConfigureResource(resource =>
  {
    resource.AddService("age-api");
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
    tracing.AddSource(Instrumentation.SourceName);
  })
  .WithMetrics(metrics =>
  {
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddOtlpExporter();
  });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI(config =>
  {
    config.SwaggerEndpoint("/swagger/v1/swagger.json", "Server Age API");
    config.RoutePrefix = string.Empty;
  });
}

app.UseHttpsRedirection();

app
  .MapGet("/generate-age", (HttpContext context, Instrumentation instrumentation) =>
  {
    var originalUserAgent = Baggage.GetBaggage("original_user_agent");
    Activity.Current?.AddTag("original_user_agent", originalUserAgent);

    using var activity = instrumentation.Source.StartActivity("GenerateRandomAge");
    var age = Random.Shared.Next(1, 100);
    activity?.SetTag("age", age);

    return Results.Ok(new { Age = age });
  })
  .WithName("GetAge")
  .WithOpenApi();

app.Run();

class Instrumentation
{
  public const string SourceName = "AgeAPI";
  public readonly ActivitySource Source = new(SourceName);
}