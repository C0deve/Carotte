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

## Compatibility & Integration Status

Carotte is currently a **Proof of Concept**. It is useful for experiments, prototypes, and early feedback, but it should not be considered production-ready yet.

Before integrating Carotte into an existing service, check these constraints:

- **.NET target**: Carotte targets .NET 10. Existing .NET 8 LTS or .NET 9 applications must be upgraded before they can reference it.
- **RabbitMQ topology ownership**: Carotte declares queues, exchanges, and bindings automatically. This is convenient for greenfield services, but it must be reviewed carefully when connecting to an existing RabbitMQ topology.
- **Serialization contract**: messages are serialized as JSON using `System.Text.Json`.
- **Dependency Injection lifetime**: consumers are registered as singleton services. Avoid injecting scoped services such as `DbContext` directly into a consumer until scoped consumer execution is supported.
- **Package availability**: package names are listed below, but check the current NuGet/feed publication status before relying on them in another project.

## 📦 Installation

Carotte is available as a set of NuGet packages:

- `Carotte`: Core library.
- `Carotte.TestKit`: Testing utilities.

Install the runtime package in an application project:

```bash
dotnet add package Carotte
```

Install the test package in a test project:

```bash
dotnet add package Carotte.TestKit
```

If the packages are not published to nuget.org yet, reference the projects directly from this repository while evaluating the library:

```bash
dotnet add reference ../Carotte/Carotte.csproj
dotnet add reference ../Carotte.TestKit/Carotte.TestKit.csproj
```

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

    // Register consumers and [Publisher] message types from this assembly
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Optional: Filter by namespace
    // carotte.AddNamespaces("MyService.Consumers");

    // Optional: Add OpenTelemetry
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();
app.Run();
```

### 2. Define a Consumed Message and a Consumer

```csharp
public record OrderCreatedEvent(Guid OrderId, string CustomerName, decimal Amount);

// Convention-based consumer: queue named after the class, automatically bound to message exchange
public class OrderConsumer(ILogger<OrderConsumer> logger) : IConsumer<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order received: {OrderId} for {CustomerName}", message.OrderId, message.CustomerName);
        return Task.CompletedTask;
    }
}
```

### 3. Define a Produced Message

Carotte registers `IPublisher<TMessage>` only for message types marked with `[Publisher]`.

```csharp
[Publisher]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);
```

If a message type is only consumed by the service, do not annotate it with `[Publisher]`.

### 4. Consumer and Publisher Configuration

Carotte favors **Convention over Configuration**. By default, any class implementing `IConsumer<T>` is automatically registered and configured using top-level conventions.

#### Configuration Rules
- **Automatic Registration**: All classes implementing `IConsumer<T>` are automatically picked up via `AddAssemblies`.
- **Explicit Producer Registration**: A message type is publishable only when it is annotated with `[Publisher]`. Consuming `TMessage` does not register `IPublisher<TMessage>`.
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
- `[Queue("name", broker: "name", exchange: "exchange", routingKey: "key", prefetchCount: 10)]`: Specifies the queue name, broker, source exchange, routing key, and parallelism limit (QoS).
- `[Binding("exchange", "routingKey")]`: Adds additional bindings to an explicitly configured consumer queue.
- `[Publisher(broker: "name", exchange: "name")]`: Customizes the broker or exchange used when publishing a message type.

> [!NOTE]
> In the current implementation, applying `[Queue]` switches the consumer to attribute-based topology. If you want the default E2E convention (`x.pub.*` -> `x.sub.*` -> `q.*`), do not add `[Queue]` to the consumer.

`[Publisher]` is applied to the **message type**, not to the consumer:

```csharp
[Publisher(broker: "my-broker", exchange: "orders-exchange")]
public record CreateOrderCommand(Guid OrderId);
```

(See **Configuration Examples** at the bottom for more details.)

#### Consumer Error Strategy

By default, Carotte applies a production-oriented error strategy by convention:

- retries failed message handling up to 3 times in-process;
- nacks with `requeue: false` after retries are exhausted;
- declares a durable dead-letter exchange and queue;
- configures the consumer queue with RabbitMQ dead-letter arguments.

For a queue named `q.order-consumer`, the convention creates:

| Resource | Generated name |
| :--- | :--- |
| Dead-letter exchange | `x.dlx.order-consumer` |
| Dead-letter queue | `q.dlq.order-consumer` |
| Dead-letter routing key | `q.order-consumer` |

You can configure the error strategy on `[Queue]`:

```csharp
[Queue(
    "order-processing-queue",
    broker: "my-broker",
    exchange: "orders-exchange",
    routingKey: "order.created",
    maxRetryAttempts: 2,
    deadLetterExchange: "orders.dlx",
    deadLetterRoutingKey: "order.failed")]
public class OrderConsumer : IConsumer<OrderCreatedEvent> { ... }
```

Error strategy options:

| Option | Default | Behavior |
| :--- | :--- | :--- |
| `maxRetryAttempts` | `3` | Number of in-process retries after the first failed attempt. `2` means up to 3 total attempts. Use `0` to disable retries. |
| `failureAction` | `ConsumerFailureAction.DeadLetter` | Uses `BasicNack(..., requeue: false)` after retries are exhausted. The convention configures a DLX/DLQ so RabbitMQ dead-letters the message. |
| `ConsumerFailureAction.Requeue` | n/a | Uses `BasicNack(..., requeue: true)` after retries are exhausted. Use carefully because poison messages can loop forever. |
| `deadLetterExchange` | `x.dlx.{queue-name}` | Carotte declares this exchange as durable `fanout` and configures the queue with `x-dead-letter-exchange`. |
| `deadLetterRoutingKey` | consumer queue name | Queue argument `x-dead-letter-routing-key` and binding key for the DLQ. |
| `deadLetterQueue` | `q.dlq.{queue-name}` | Carotte declares this queue as durable and binds it to the DLX. |

Unknown message types for multi-message consumers are never retried. Carotte nacks them with `requeue: false` so they can be dead-lettered if the queue has a DLX.

### 5. Topology Conventions (E2E Binding)

Carotte uses a **"Convention over Configuration"** approach to simplify RabbitMQ setup. If you don't specify an exchange or routing key, Carotte automatically applies the following rules based on **Exchange-to-Exchange (E2E)** binding.

#### Why this convention?
- **Total Decoupling**: The publisher publishes to a "message type", not to a destination.
- **Flexibility**: A consumer can listen to multiple message types without changing its queue configuration.
- **Simplicity**: Fewer attributes to write.

#### Publisher Side (Publication)
For a message type annotated with `[Publisher]`, Carotte registers `IPublisher<TMessage>`. By default, the publisher publishes to a `fanout` exchange whose name is derived from the message class name (kebab-case) with an `x.pub.` prefix. Common suffixes like `Message`, `Event`, or `Command` are automatically removed.
- **Message**: `CreateOrderCommand`
- **Exchange**: `x.pub.create-order`
- **Routing Key**: Empty (since it's a `fanout`).

#### Consumer Side (Reception)
Carotte automatically creates a two-level mesh:
1. **Message Exchange (Source)**: A global exchange for the message type. Its name is the kebab-case version of the message class prefixed by `x.pub.` (e.g., `x.pub.order-created`).
2. **Consumer Exchange (Destination)**: An internal exchange named after the consumer class in kebab-case, prefixed by `x.sub.`. If a `ClientName` is configured, it is included in the prefix: `x.sub.{client-name}.{consumer-name}`.
3. **The Mesh (E2E)**: Carotte binds the message exchange to the consumer exchange.
4. **The Queue**: The consumer exchange is bound to the final queue: `q.{consumer-name}` (or `q.{client-name}.{consumer-name}`).

**Example of generated topology (without ClientName):**
`[Exchange: x.pub.order-created]` --(E2E)--> `[Exchange: x.sub.order-consumer]` --(Binding)--> `[Queue: q.order-consumer]`

#### Naming Reference

| Input | Generated name |
| :--- | :--- |
| Message `OrderCreatedMessage` | `x.pub.order-created` |
| Message `OrderCreatedEvent` | `x.pub.order-created` |
| Message `CreateOrderCommand` | `x.pub.create-order` |
| Consumer `OrderConsumer` | `x.sub.order-consumer` |
| Consumer `OrderConsumer` with `ClientName = "order-service"` | `x.sub.order-service.order-consumer` |
| Queue for `OrderConsumer` | `q.order-consumer` |
| Queue for `OrderConsumer` with `ClientName = "order-service"` | `q.order-service.order-consumer` |

#### RabbitMQ Declaration Defaults

With convention-based topology, Carotte declares:

| Resource | Type/settings |
| :--- | :--- |
| Message exchange | `fanout`, durable, not auto-delete |
| Consumer exchange | `fanout`, durable, not auto-delete |
| Queue | durable, non-exclusive, not auto-delete |
| Message exchange -> consumer exchange binding | routing key `""` |
| Consumer exchange -> queue binding | routing key `""` |

With attribute-based topology, Carotte declares the queue with the same queue defaults and binds it to the exchanges you specify. It does not currently expose every RabbitMQ declaration flag in the public API.

> [!IMPORTANT]
> RabbitMQ requires an existing exchange declaration to match its original type and flags. If an existing exchange named `orders-exchange` is already declared as `topic`, do not let Carotte redeclare it as `fanout`. Use explicit attributes and verify the generated topology before connecting Carotte to shared infrastructure.

#### Simplified Example
Thanks to conventions, configuration is minimal:

```csharp
// Queue name: q.order-consumer
// Message exchange: x.pub.order-created
// Consumer exchange: x.sub.order-consumer
public class OrderConsumer : IConsumer<OrderCreatedEvent> { ... }
```

### 6. Send a Message

Inject `IPublisher<TMessage>` and call `PublishAsync`:

```csharp
app.MapPost("/order", async (IPublisher<CreateOrderCommand> publisher) =>
{
    var command = new CreateOrderCommand(Guid.NewGuid(), "Jean Dupont", 42.50m);
    await publisher.PublishAsync(command);
    return Results.Accepted();
});
```

## Integrating with an Existing RabbitMQ Topology

Carotte is easiest to integrate when it owns the topology for a service. In an existing RabbitMQ setup, prefer explicit configuration and validate the generated declarations before deploying.

### Consume from an existing exchange

Use `[Queue]` when you already know the queue, exchange, broker, and routing key:

```csharp
[Queue(
    "order-processing-queue",
    broker: "my-broker",
    exchange: "orders-exchange",
    routingKey: "order.created",
    prefetchCount: 10)]
public class OrderConsumer : IConsumer<OrderCreatedMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

Use additional `[Binding]` attributes when the same queue must receive messages from multiple exchanges:

```csharp
[Queue("order-processing-queue", broker: "my-broker", exchange: "orders-exchange", routingKey: "order.created")]
[Binding("notifications-exchange", "notification.created")]
public class MultiMessageConsumer :
    IConsumer<OrderCreatedMessage>,
    IConsumer<NotificationMessage>
{
    public Task HandleAsync(OrderCreatedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Publish to an existing exchange

Apply `[Publisher]` to the message type:

```csharp
[Publisher(broker: "my-broker", exchange: "orders-exchange")]
public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount);
```

Then inject `IPublisher<OrderCreatedMessage>` as usual.

### Existing-topology checklist

- Confirm the target project can run on .NET 10.
- Confirm the RabbitMQ exchange names, queue names, routing keys, exchange types, and declaration flags.
- Prefer `[Queue]`, `[Binding]`, and `[Publisher]` over conventions when connecting to shared RabbitMQ resources.
- Confirm that the message `Type` property is populated consistently when one consumer handles multiple message types.
- Confirm JSON compatibility with existing producers and consumers.

## 🏗️ Architecture (Consumers & BackgroundServices)

In the **Carotte** project, the relationship between `consumers` and `BackgroundServices` is a **host-to-guest** relationship.

### 1. The Consumer (`IConsumer<TMessage>`): Business Logic
The `Consumer` is a simple class that implements the `IConsumer<TMessage>` interface. Its sole role is to process a message once it has been received and deserialized.
- It is **passive**: it doesn't know where the message comes from or how it was retrieved.
- It is registered as a singleton service in the dependency injection (DI) container.

Because consumers are singletons, do not inject scoped dependencies directly into them. If your handler needs per-message scoped services, create a scope inside the handler or adapt the library before using it in production workflows.

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

This reduces startup failures caused by missing RabbitMQ resources. It does not replace broker-level durability, dead-lettering, retry, or deployment-order decisions.

## Message Contract

### Serialization

Carotte serializes messages with `System.Text.Json`.

Default behavior:

- payload format: UTF-8 JSON
- deserialization is case-insensitive for property names
- no custom naming policy is configured by default
- the default serializer is registered as `ISerializer`

You can replace the serializer before Carotte creates publishers and consumers by registering your own `ISerializer` implementation in DI:

```csharp
builder.Services.AddSingleton<ISerializer, MySerializer>();

builder.Services.AddCarotte(carotte =>
{
    carotte.AddBroker("my-broker", options => { ... });
    carotte.AddAssemblies(typeof(Program).Assembly);
});
```

When integrating with existing producers or consumers, verify that both sides agree on JSON shape, property names, enum handling, date handling, and nullability.

### Message Type Resolution

When a consumer handles a single message type, Carotte can infer the target type.

When a consumer handles multiple message types, Carotte uses `BasicProperties.Type` from the RabbitMQ message properties. The expected value is the short CLR type name, for example:

| Message type | Expected `BasicProperties.Type` |
| :--- | :--- |
| `OrderCreatedMessage` | `OrderCreatedMessage` |
| `NotificationMessage` | `NotificationMessage` |

Carotte publishers set this property automatically. External publishers must set it explicitly when publishing to a queue consumed by a multi-message consumer.

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

Here are the different ways to configure consumers and publishers, from the simplest to the most customized.

### 1. Consumer by Convention
The recommended way. Any class implementing `IConsumer<T>`.

```csharp
// Queue name: "q.order-consumer"
// Automatically bound to "x.pub.order-created" fanout exchange
public class OrderConsumer : IConsumer<OrderCreatedEvent> { ... }
```

### 2. Explicit Consumer Queue and Binding
Use `[Queue]` when you want to bind a named queue to a specific exchange and routing key.

```csharp
// Queue name: "special-consumer-queue"
// Bound to "custom-exchange" with "routing.key"
[Queue("special-consumer-queue", broker: "my-broker", exchange: "custom-exchange", routingKey: "routing.key")]
public class SpecialConsumer : IConsumer<Message> { ... }
```

### 3. Multiple Consumer Bindings
Use `[Binding]` together with `[Queue]` when the same queue must receive messages from more than one exchange or routing key.

```csharp
[Queue("my-custom-queue", broker: "secondary-broker", exchange: "orders", routingKey: "created")]
[Binding("orders-priority", "created.priority")]
public class CustomConsumer : IConsumer<OrderMessage> { ... }
```

### 4. Publisher by Attribute
Use `[Publisher]` on every message type produced by the service.

```csharp
// Publisher registered as IPublisher<CreateOrderCommand>
// Exchange: "x.pub.create-order"
[Publisher]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);
```

### 5. Publisher with Explicit Broker or Exchange
Use `[Publisher]` parameters when a produced message must target a specific broker or exchange.

```csharp
[Publisher(broker: "orders-broker", exchange: "orders-exchange")]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);
```

## 📄 License

TODO: Specify License (likely MIT or Apache-2.0).

---

*Made with ❤️ and 🥕*
