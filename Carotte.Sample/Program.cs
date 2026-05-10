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

    // Register a Publisher to be able to send test commands
    // (Optional if messages are already marked with [Publisher])
    // carotte.AddPublisher<OrderCreatedMessage>("my-broker", "orders-exchange");
    // carotte.AddPublisher<NotificationMessage>("my-broker", "notifications-exchange");

    // Automatic consumer scan in this assembly
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Add an OTLP exporter for observability
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();

app.MapGet("/", () => "Carotte Sample API is running. Use POST /order to simulate a message.");

// Endpoint to simulate sending a message
app.MapPost("/order", async (IPublisher<OrderCreatedMessage> publisher) =>
{
    var order = new OrderCreatedMessage(Guid.NewGuid(), "Jean Dupont", 42.50m);
    await publisher.PublishAsync(order);
    return Results.Accepted(value: order);
});

// Endpoint to simulate sending a notification
app.MapPost("/notify", async (IPublisher<NotificationMessage> publisher) =>
{
    var notification = new NotificationMessage(Guid.NewGuid(), "Your order has been shipped!", "client@example.com");
    await publisher.PublishAsync(notification);
    return Results.Accepted(value: notification);
});

app.Run();
