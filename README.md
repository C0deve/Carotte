# Carotte 🥕

> [!WARNING]
> This library is currently under development and is not yet ready for production use.
> This is a **Proof of Concept (PoC)** created entirely using **Junie**, with a focus on writing as little code as possible.

Carotte is a high-level RabbitMQ client wrapper for .NET 10, designed for seamless microservices communication with built-in observability and a focus on simplicity and performance.

## 🚀 Features

- **Built-in Observability**: First-class support for OpenTelemetry (Tracing & Metrics).
- **Publisher/Consumer Abstractions**: Clean interfaces for message handling. A consumer can handle multiple message types.
- **Pipeline-based Processing**: Middleware support for both consumers and publishers (Logging, Tracing, Metrics).
- **Automatic Registration**: Easy dependency injection setup with assembly scanning and namespace filtering.
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
        options.DefaultPrefetchCount = 10; // Optional: Default is 1
    });

    // Optional: Set a client name for prefixing queues and exchanges
    carotte.SetClientName("order-service");

    // Register Consumers & Publishers (Automatic scan in this assembly)
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Optional: Filter by namespace
    // carotte.AddNamespaces("MyService.Consumers");

    // Optional: Add OpenTelemetry
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();
app.Run();
```

### 2. Define a Message and a Consumer

```csharp
public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount);

// Convention-based consumer: queue named after the class, automatically bound to message exchange
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

Carotte favors **Convention over Configuration**. By default, any class implementing `IConsumer<T>` is automatically registered and configured using top-level conventions.

#### Configuration Rules
- **Automatic Registration**: All classes implementing `IConsumer<T>` are automatically picked up via `AddAssemblies`.
- **Default Queue Name**: The queue name defaults to the consumer's class name in kebab-case, formatted as `q.class-name` (or `q.client-name.class-name` if `ClientName` is set).
- **Parallelism & Ordering**: By default, the `PrefetchCount` is set to **1**. This ensures strict message ordering (FIFO) and avoids concurrent processing of multiple messages by the same consumer instance.
- **Automatic Topology**: Carotte creates the necessary exchanges and bindings based on the message type.
- **Broker Assignment**: Consumers and publishers are assigned to the default broker unless specified otherwise via attributes.
- **Multi-Message Support**: A single class can implement multiple `IConsumer<TMessage>` interfaces to handle different message types from the same queue.
- **Message Resolution**: When a queue receives messages of different types, Carotte attempts to resolve the correct message type using the `Type` property of the RabbitMQ message properties. If only one message type is handled by the consumer, it defaults to that type.

#### Validation at Startup
Carotte performs strict validation during the `AddCarotte` call to ensure the configuration is valid:
- **Broker Presence**: At least one broker must be registered using `AddBroker`.
- **Broker Reference**: If a consumer (via `[Queue]`) or a producer specifies a broker by name, that broker must exist in the configuration.
- **Duplicate Warnings**: While not a hard error, Carotte logs warnings if multiple bindings are defined for the same queue and broker.

If validation fails, a `CarotteConfigurationException` is thrown with details about the configuration errors.

#### Advanced Configuration
For advanced scenarios, you can customize your consumers and messages using attributes:
- `[Queue("name", broker: "name", prefetchCount: 10)]`: Specifies the queue name, the broker, and the parallelism limit (QoS).
- `[Binding("exchange", "routingKey")]`: Adds additional bindings to the consumer's queue.
- `[Publisher(broker: "name", exchange: "name")]`: Customizes the broker or exchange used when publishing a message type.

(See **Configuration Examples** at the bottom for more details.)

### 4. Topology Conventions (E2E Binding)

Carotte uses a **"Convention over Configuration"** approach to simplify RabbitMQ setup. If you don't specify an exchange or routing key, Carotte automatically applies the following rules based on **Exchange-to-Exchange (E2E)** binding.

#### Why this convention?
- **Total Decoupling**: The publisher publishes to a "message type", not to a destination.
- **Flexibility**: A consumer can listen to multiple message types without changing its queue configuration.
- **Simplicity**: Fewer attributes to write.

#### Publisher Side (Publication)
By default, a publisher publishes to a `fanout` exchange whose name is derived from the message class name (kebab-case) with an `x.pub.` prefix. Common suffixes like `Message`, `Event`, or `Command` are automatically removed.
- **Message**: `OrderCreatedMessage`
- **Exchange**: `x.pub.order-created`
- **Routing Key**: Empty (since it's a `fanout`).

#### Consumer Side (Reception)
Carotte automatically creates a two-level mesh:
1. **Message Exchange (Source)**: A global exchange for the message type. Its name is the kebab-case version of the message class prefixed by `x.pub.` (e.g., `x.pub.order-created`).
2. **Consumer Exchange (Destination)**: An internal exchange named after the consumer class in kebab-case, prefixed by `x.sub.`. If a `ClientName` is configured, it is included in the prefix: `x.sub.{client-name}.{consumer-name}`.
3. **The Mesh (E2E)**: Carotte binds the message exchange to the consumer exchange.
4. **The Queue**: The consumer exchange is bound to the final queue: `q.{consumer-name}` (or `q.{client-name}.{consumer-name}`).

**Example of generated topology (without ClientName):**
`[Exchange: x.pub.order-created]` --(E2E)--> `[Exchange: x.sub.order-consumer]` --(Binding)--> `[Queue: q.order-consumer]`

#### Simplified Example
Thanks to conventions, configuration is minimal:

```csharp
// Consumer with just the queue name
[Queue("order-processing-queue", broker: "my-broker")]
public class OrderConsumer : IConsumer<OrderCreatedMessage> { ... }
```

### 5. Send a Message

Inject `IPublisher<TMessage>` and call `PublishAsync`:

```csharp
app.MapPost("/order", async (IPublisher<OrderCreatedMessage> publisher) =>
{
    var order = new OrderCreatedMessage(Guid.NewGuid(), "Jean Dupont", 42.50m);
    await publisher.PublishAsync(order);
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
- **Reuse**: For each configured broker (e.g., `"my-broker"`), a single TCP connection is established and shared between all publishers and consumers using that broker.
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

Carotte provides a `TestKit` to simplify testing your consumers and publishers without a live RabbitMQ broker.

```csharp
// Use CarotteTestKit in your integration tests
var testKit = host.Services.GetRequiredService<CarotteTestKit>();

// Simulate receiving a message
await testKit.SimulateReceiveAsync<OrderConsumer, OrderCreatedMessage>(new OrderCreatedMessage(...));

// Verify messages sent by publishers
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

## 🛠️ Configuration Examples

Here are the different ways to configure your consumers, from the simplest to the most customized.

### 1. Zero Configuration (Convention)
The recommended way. Any class implementing `IConsumer<T>`.

```csharp
// Queue name: "q.order-consumer"
// Automatically bound to "x.pub.order-created" fanout exchange
public class OrderConsumer : IConsumer<OrderCreatedMessage> { ... }
```

### 2. Custom Binding Only
Use `[Binding]` if you want to use the default queue name but bind it to a specific exchange.

```csharp
// Queue name: "q.special-consumer"
// Bound to "custom-exchange" with "routing.key"
[Binding("custom-exchange", "routing.key")]
public class SpecialConsumer : IConsumer<Message> { ... }
```

### 3. Full Customization
Use `[Queue]` for full control over the queue name, broker, and bindings.

```csharp
[Queue("my-custom-queue", broker: "secondary-broker", exchange: "orders", routingKey: "created")]
public class CustomConsumer : IConsumer<OrderMessage> { ... }
```

## 📄 License

TODO: Specify License (likely MIT or Apache-2.0).

---

*Made with ❤️ and 🥕*
