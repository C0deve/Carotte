using Carotte;
using Carotte.Sample;

var builder = WebApplication.CreateBuilder(args);

// Configuration de Carotte
builder.Services.AddCarotte(carotte =>
{
    // Configuration du broker RabbitMQ (utilise les valeurs par défaut si localhost)
    carotte.AddBroker("my-broker", options =>
    {
        options.Host = "localhost";
        options.UserName = "guest";
        options.Password = "guest";
    });

    // Enregistrement d'un producteur pour pouvoir envoyer des commandes de test
    carotte.AddProducer<OrderCreatedMessage>("my-broker", "orders-exchange");
    carotte.AddProducer<NotificationMessage>("my-broker", "notifications-exchange");

    // Scan automatique des consommateurs dans cet assembly
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Ajout d'un exportateur OTLP pour l'observabilité
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();

app.MapGet("/", () => "Carotte Sample API is running. Use POST /order to simulate a message.");

// Endpoint pour simuler l'envoi d'un message
app.MapPost("/order", async (IProducer<OrderCreatedMessage> producer) =>
{
    var order = new OrderCreatedMessage(Guid.NewGuid(), "Jean Dupont", 42.50m);
    await producer.SendAsync(order);
    return Results.Accepted(value: order);
});

// Endpoint pour simuler l'envoi d'une notification
app.MapPost("/notify", async (IProducer<NotificationMessage> producer) =>
{
    var notification = new NotificationMessage(Guid.NewGuid(), "Votre commande est expédiée !", "client@example.com");
    await producer.SendAsync(notification);
    return Results.Accepted(value: notification);
});

app.Run();
