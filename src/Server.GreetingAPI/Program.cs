using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.DescribeAllParametersInCamelCase());

const string AgeApiKey = "AgeApi";

builder.Services.AddHttpClient(AgeApiKey, client => client.BaseAddress = new Uri("https://localhost:7099"));

builder.Logging.AddOpenTelemetry(o => o.AddOtlpExporter());

builder.Services.AddOpenTelemetry()
  .ConfigureResource(resource =>
  {
    resource.AddService("greeting-api");
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
    tracing.AddHttpClientInstrumentation();
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
  .MapPost("/greeting", async (
    [AsParameters] GreetingRequest req,
    [FromServices] ILogger<Program> logger,
    [FromServices] IHttpClientFactory clientFactory,
    HttpContext context
  ) =>
  {
    Activity.Current?.SetTag("firstName", req.FirstName);
    Activity.Current?.SetTag("surname", req.Surname);
    logger.LogInformation("Greeting endpoint called: {firstName} {surname}", req.FirstName, req.Surname);
    Baggage.SetBaggage("original_user_agent", context.Request.Headers.UserAgent.ToString());

    var client = clientFactory.CreateClient(AgeApiKey);
    var response = await client.GetFromJsonAsync<AgeResponse>("/generate-age");

    return Results.Ok($"Hello my name is {req.FirstName} {req.Surname} and I am {response!.Age} years old.");
  })
  .WithName("Greeting")
  .WithOpenApi();

app.Run();

record GreetingRequest(
  [FromQuery] string FirstName,
  [FromQuery] string Surname
);

record AgeResponse(int Age);