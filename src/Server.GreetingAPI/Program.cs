using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.AspNetCore.Mvc;

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Instrumentation>();

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
    tracing.AddSource(Instrumentation.SourceName);
  })
  .WithMetrics(metrics =>
  {
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddHttpClientInstrumentation();
    metrics.AddRuntimeInstrumentation();
    metrics.AddOtlpExporter();
    metrics.AddMeter(Instrumentation.Meter.Name);
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
    HttpContext context,
    Instrumentation instrumentation
  ) =>
  {
    Activity.Current?.SetTag("firstName", req.FirstName);
    Activity.Current?.SetTag("surname", req.Surname);
    logger.LogInformation("Greeting endpoint called: {firstName} {surname}", req.FirstName, req.Surname);
    Baggage.SetBaggage("original_user_agent", context.Request.Headers.UserAgent.ToString());
    Instrumentation.GreetingCounter.Add(1);


    var client = clientFactory.CreateClient(AgeApiKey);

    var response = await client.GetFromJsonAsync<AgeResponse>("/generate-age");

    return response is null
      ? Results.Problem("Failed to get age from Age API", statusCode: StatusCodes.Status500InternalServerError)
      : Results.Ok($"Hello my name is {req.FirstName} {req.Surname} and I am {response.Age} years old.");
  })
  .WithName("Greeting")
  .WithOpenApi();

app.Run();

record GreetingRequest(
  [FromQuery] string FirstName,
  [FromQuery] string Surname
);

record AgeResponse(int Age);

class Instrumentation
{
  public const string SourceName = "GreetingAPI";
  public const string MeterName = "GreetingAPI";
  public readonly ActivitySource Source = new(SourceName);
  public static Meter Meter = new(MeterName);
  public static Counter<int> GreetingCounter = Meter.CreateCounter<int>("greeting_counter");
}