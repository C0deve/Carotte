using Carotte;
using Carotte.Sample;

var builder = WebApplication.CreateBuilder(args);

// Carotte configuration
builder.Services.AddCarotte(carotte =>
{
    // Configure RabbitMQ broker (uses default values if localhost)
    carotte.AddBroker("my-broker", options =>
    {
        options.Host = "localhost";
        options.UserName = "guest";
        options.Password = "guest";
    });

    // Register a producer to be able to send test commands
    carotte.AddProducer<OrderCreatedMessage>("my-broker", "orders-exchange");
    carotte.AddProducer<NotificationMessage>("my-broker", "notifications-exchange");

    // Automatic consumer scan in this assembly
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Add an OTLP exporter for observability
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();

app.MapGet("/", () => "Carotte Sample API is running. Use POST /order to simulate a message.");

// Endpoint to simulate sending a message
app.MapPost("/order", async (IProducer<OrderCreatedMessage> producer) =>
{
    var order = new OrderCreatedMessage(Guid.NewGuid(), "Jean Dupont", 42.50m);
    await producer.SendAsync(order);
    return Results.Accepted(value: order);
});

// Endpoint to simulate sending a notification
app.MapPost("/notify", async (IProducer<NotificationMessage> producer) =>
{
    var notification = new NotificationMessage(Guid.NewGuid(), "Votre commande est expédiée !", "client@example.com");
    await producer.SendAsync(notification);
    return Results.Accepted(value: notification);
});

app.Run();
