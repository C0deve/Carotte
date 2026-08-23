using Carotte;
using Carotte.Sample.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Configure Carotte with conventions and broker settings
builder.Services.AddCarotte(carotte =>
{
    carotte.AddBroker("primary-broker", options =>
    {
        builder.Configuration.GetSection("Carotte:Brokers:primary-broker").Bind(options);
    });

    // Automatic consumer & publisher scan
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Optional OpenTelemetry exporter
    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        carotte.AddOtlpExporter(otlpEndpoint);
    }
});

var app = builder.Build();

app.MapGet("/", () => "Carotte Sample API is running. Use POST /api/orders to place an order.");
app.MapOrderEndpoints();

app.Run();
