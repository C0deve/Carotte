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

## 🏗️ Architecture (Consumers & BackgroundServices)

In the **Carotte** project, the relationship between `consumers` and `BackgroundServices` is a **host-to-guest** relationship.

### 1. The Consumer (`IConsumer<TMessage>`): Business Logic
The `Consumer` is a simple class that implements the `IConsumer<TMessage>` interface. Its sole role is to process a message once it has been received and deserialized.
- It is **passive**: it doesn't know where the message comes from or how it was retrieved.
- It is registered as a standard service in the dependency injection (DI) container.

### 2. The BackgroundService (`RabbitMqConsumerHost<TConsumer>`): The Engine
For each registered `Consumer`, Carotte automatically creates a **`RabbitMqConsumerHost<TConsumer>`**. This class inherits from `BackgroundService` (a .NET base class for background tasks).
- It is the **"tireless worker"** running in a loop.
- It manages the lifecycle of the RabbitMQ connection (`StartAsync`, `StopAsync`).
- It declares the queues (`Queues`) and exchanges (`Exchanges`) on the broker.
- It actively listens for incoming messages on RabbitMQ.

### 3. The link between the two
The `BackgroundService` acts as a **bridge** between RabbitMQ and your `Consumer`:

1. **Reception**: The `RabbitMqConsumerHost` receives a raw message (bytes) from RabbitMQ.
2. **Pipeline**: It passes this message through a pipeline (Middleware) for telemetry, metrics, and **deserialization**.
3. **Invocation**: Once the message is transformed into a C# object, the `RabbitMqConsumerHost` uses a `ConsumerMediator` to instantiate your `Consumer` and call its `HandleAsync` method.
4. **Acknowledgment**: If the `Consumer` finishes its work without error, the `BackgroundService` sends an `Ack` (acknowledgment) to RabbitMQ to remove the message from the queue.

### Summary
| Component | Type | Role |
| :--- | :--- | :--- |
| **Your Consumer** | `IConsumer<T>` | **WHAT to do**: Contains business logic to process ONE message. |
| **RabbitMqConsumerHost** | `BackgroundService` | **HOW to do it**: Manages the connection, continuous listening, and calling your consumer. |

This separation allows your business code (the Consumer) to remain pure and simple, while all the complexity of network communication and RabbitMQ error handling is isolated in the `BackgroundService`.

## 🔌 Connection and Channel Management

Carotte optimizes the use of RabbitMQ resources by intelligently managing connections and channels transparently.

### 1. Connections (One per Broker)
The `ConnectionManager` acts as a registry of persistent connections.
- **Reuse**: For each configured broker (e.g., `"my-broker"`), a single TCP connection is established and shared between all producers and consumers using that broker.
- **Thread-Safety**: Connection creation is protected by asynchronous locks (`SemaphoreSlim`) to avoid redundant connections during a massive startup.
- **Lifecycle**: Connections are managed as singletons and are properly closed when the application stops.

### 2. Channels (Performance Optimization)
Channels (`IChannel`) are created on top of connections by the `RabbitMqClient`.
- **Caching**: Carotte maintains one open channel per broker for common operations (publication, acknowledgment). This avoids the high cost of creating/closing channels for each message.
- **Auto-repair**: If a channel is detected as closed (`IsOpen == false`), Carotte automatically recreates one during the next operation.

### 3. Topology Declaration
When a consumer starts, the `RabbitMqConsumerHost` ensures that:
1. The exchange (`Exchange`) exists.
2. The queue (`Queue`) exists.
3. The binding (`Binding`) between the two is correctly configured.

This ensures that no messages are lost due to a missing configuration on the RabbitMQ server.

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
