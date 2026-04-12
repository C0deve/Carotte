# Carotte 🥕

> [!WARNING]
> This library is currently under development and is not yet ready for production use.
> This is a **Proof of Concept (PoC)** created entirely using **Junie**, with a focus on writing as little code as possible.

Carotte is a high-level RabbitMQ client wrapper for .NET 10, designed for seamless microservices communication with built-in observability and a focus on simplicity and performance.

## 🚀 Features

- **Built-in Observability**: First-class support for OpenTelemetry (Tracing & Metrics).
- **Producer/Consumer Abstractions**: Clean interfaces for message handling.
- **Pipeline-based Processing**: Middleware support for both consumers and producers.
- **Automatic Registration**: Easy dependency injection setup with assembly scanning.
- **Test-Driven Design**: Includes a dedicated `TestKit` for easy integration testing.
- **High Performance**: Optimized for .NET 10 with a lightweight footprint.

## 🛠️ Requirements

- **SDK**: .NET 10.0+
- **Broker**: RabbitMQ (standard installations or Docker)

## 📦 Installation

Carotte is available as a set of NuGet packages:

- `Carotte`: Core library.
- `Carotte.TestKit`: Testing utilities.

## 🏁 Quick Start

### 1. Configure Carotte in your Service

In your `Program.cs` (or `Startup.cs`):

```csharp
using Carotte;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarotte(carotte =>
{
    // Configure RabbitMQ broker
    carotte.AddBroker("my-broker", options =>
    {
        options.Host = "localhost";
        options.UserName = "guest";
        options.Password = "guest";
    });

    // Register Producers
    carotte.AddProducer<OrderCreatedMessage>("my-broker", "orders-exchange");

    // Register Consumers (Automatic scan in this assembly)
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Optional: Add OpenTelemetry
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();
app.Run();
```

### 2. Define a Message and a Consumer

```csharp
public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount);

// The [Queue] attribute is mandatory for all consumers
[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
public class OrderConsumer(ILogger<OrderConsumer> logger) : IConsumer<OrderCreatedMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order received: {OrderId} for {CustomerName}", message.OrderId, message.CustomerName);
        return Task.CompletedTask;
    }
}
```

### 3. Consumer Configuration & Validation

Carotte enforces strict configuration for its consumers to avoid runtime issues:

- **Mandatory Configuration**: Every class implementing `IConsumer<T>` must have at least one `[Queue]` attribute or be explicitly configured during setup. Failure to do so will result in a `CarotteConfigurationException` at startup.
- **Duplicate Warnings**: If a consumer defines multiple `[Queue]` attributes with the same name and broker, a warning will be logged to the console during registration.
- **Programmatic Configuration**: For consumers without attributes (e.g., from external libraries), you can use the `ConsumerConfigs` dictionary in the builder:

```csharp
builder.Services.AddCarotte(carotte =>
{
    // ...
    carotte.ConsumerConfigs[typeof(ExternalConsumer)] = ("my-broker", "external-queue");
});
```

### 4. Send a Message

Inject `IProducer<TMessage>` and call `SendAsync`:

```csharp
app.MapPost("/order", async (IProducer<OrderCreatedMessage> producer) =>
{
    var order = new OrderCreatedMessage(Guid.NewGuid(), "Jean Dupont", 42.50m);
    await producer.SendAsync(order);
    return Results.Accepted();
});
```

## 🧪 Testing

Carotte provides a `TestKit` to simplify testing your consumers and producers without a live RabbitMQ broker.

```csharp
// Use CarotteTestKit in your integration tests
var testKit = host.Services.GetRequiredService<CarotteTestKit>();

// Simulate receiving a message
await testKit.SimulateReceiveAsync<OrderConsumer, OrderCreatedMessage>(new OrderCreatedMessage(...));

// Verify messages sent by producers
var sentMessages = testKit.GetSentMessages<OrderCreatedMessage>();
```

## 🏗️ Project Structure

- `Carotte/`: Core library containing the RabbitMQ client wrapper and pipeline logic.
- `Carotte.Sample/`: A sample ASP.NET Core application demonstrating usage.
- `Carotte.TestKit/`: Testing framework for mocking and simulating messages.
- `Carotte.Tests/`: Unit and integration tests for the project.
- `Carotte.Benchmarks/`: Performance benchmarks using BenchmarkDotNet.

## 📜 Commands

- **Build**: `dotnet build`
- **Run Sample**: `dotnet run --project Carotte.Sample`
- **Test**: `dotnet test`
- **Benchmarks**: `dotnet run -c Release --project Carotte.Benchmarks`

## 📄 License

TODO: Specify License (likely MIT or Apache-2.0).

---

*Made with ❤️ and 🥕*
