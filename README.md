# Carotte 🥕

[![CI](https://github.com/C0deve/Carotte/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/C0deve/Carotte/actions/workflows/ci.yml)
[![Release](https://github.com/C0deve/Carotte/actions/workflows/release.yml/badge.svg)](https://github.com/C0deve/Carotte/actions/workflows/release.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Carotte.svg?logo=nuget&style=flat-square)](https://www.nuget.org/packages/Carotte)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Carotte.svg?logo=nuget&style=flat-square)](https://www.nuget.org/packages/Carotte)
[![Target Framework](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&style=flat-square)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![GitHub Stars](https://img.shields.io/github/stars/C0deve/Carotte?style=flat-square&logo=github)](https://github.com/C0deve/Carotte/stargazers)
[![GitHub Issues](https://img.shields.io/github/issues/C0deve/Carotte?style=flat-square&logo=github)](https://github.com/C0deve/Carotte/issues)

> [!WARNING]
> This library is currently under development and is not yet ready for production use.
> This is a **Proof of Concept (PoC)** created entirely using **Junie**, with a focus on writing as little code as possible.

Carotte is a high-level RabbitMQ client wrapper for .NET 10, designed for seamless microservices communication with built-in observability and a focus on simplicity and performance.

## 🚀 Features

- **Built-in Observability**: First-class support for OpenTelemetry (Tracing & Metrics).
- **Publisher/Consumer Abstractions**: Clean interfaces for message handling. A consumer can handle multiple message types.
- **Pipeline-based Processing**: Middleware support for both consumers and publishers (Logging, Tracing, Metrics).
- **Automatic Registration**: Easy dependency injection setup with assembly scanning and namespace filtering.
- **Documentation Generation**: Automated Markdown specification and Mermaid topology diagram generation via `Carotte.DocCli` and `Carotte.Documentation` for CI/CD pipelines.
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
- **Dependency Injection lifetime**: consumers are scoped. Carotte creates one dependency injection scope per received message, so scoped services such as `DbContext` can be injected directly into a consumer.
- **Package availability**: package names are listed below, but check the current NuGet/feed publication status before relying on them in another project.

## 📦 Installation

Carotte is available as a set of NuGet packages:

- `Carotte`: Core library.
- `Carotte.Documentation`: Markdown documentation and topology generator.
- `Carotte.DocCli`: CLI tool for generating documentation in CI/CD.
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

    // Optional: Set a service name for prefixing queues and exchanges (explicit or automatic from assembly)
    carotte.WithServiceName("order-service");
    // carotte.WithServiceNameFromEntryAssembly(); // automatically infers from entry assembly
    // carotte.WithServiceNameFrom<Program>(); // automatically infers from specified type/assembly

    // Register consumers and [Published] message types from this assembly
    carotte.ScanAssemblies(typeof(Program).Assembly);
    // carotte.ScanAssemblyContaining<Program>();

    // Optional: Filter by namespace
    // carotte.ScanNamespaces("MyService.Consumers");
    // carotte.ScanNamespaceOf<OrderConsumer>();

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

Carotte registers `IPublisher<TMessage>` only for message types marked with `[Published]`.

```csharp
[Published]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);
```

If a message type is only consumed by the service, do not annotate it with `[Published]`.

### 4. Consumer and Publisher Configuration

Carotte favors **Convention over Configuration**. By default, any class implementing `IConsumer<T>` is automatically registered and configured using top-level conventions.

#### Configuration Rules
- **Automatic Registration**: All classes implementing `IConsumer<T>` are automatically picked up via `ScanAssemblies`.
- **Explicit Producer Registration**: A message type is publishable only when it is annotated with `[Published]`. Consuming `TMessage` does not register `IPublisher<TMessage>`.
- **Default Queue Name**: The queue name defaults to the consumer's class name in kebab-case, formatted as `q.class-name` (or `q.service-name.class-name` if `ServiceName` is set).
- **Parallelism & Ordering**: By default, the `PrefetchCount` is set to **1**. This ensures strict message ordering (FIFO) and avoids concurrent processing of multiple messages by the same consumer instance.
- **Automatic Topology**: Carotte creates the necessary exchanges and bindings based on the message type.
- **Broker Assignment**: Consumers and publishers are assigned to the default broker unless specified otherwise via attributes.
- **Multi-Message Support**: A single class can implement multiple `IConsumer<TMessage>` interfaces to handle different message types from the same queue.
- **Message Resolution**: Carotte uses `IMessageTypeResolver` to resolve the target message type using the RabbitMQ `Type` header (matching short class name, full name, assembly-qualified name, URN, or custom `[MessageType("alias")]`). If the `Type` header is omitted, single-message consumers default to their message type. If an explicit `Type` is specified but does not match any handled type, the message is safely rejected without requeue.

> [!IMPORTANT]
> **Matching `IMessageTypeResolver` for Publishers and Consumers**:
> Ensure that publishing services and consuming services exchanging messages use the same (or compatible) `IMessageTypeResolver` implementation. The identifier generated by `GetTypeIdentifier` during publication is stamped on `BasicProperties.Type` and used by `ResolveType` on the consumer side to route messages to the correct handler. If a custom `IMessageTypeResolver` is registered in DI, Carotte automatically injects it into both publishers and consumers.
- **Poison Message Protection**: Non-transient errors (such as JSON deserialization failure `JsonException`) bypass retry loops to prevent unproductive backoff retries.

#### Validation at Startup
Carotte performs strict validation during the `AddCarotte` call to ensure the configuration is valid:
- **Broker Presence**: At least one broker must be registered using `AddBroker`.
- **Broker Reference**: If a consumer (via `[Queue]`) or a producer specifies a broker by name, that broker must exist in the configuration.
- **Duplicate Warnings**: While not a hard error, Carotte logs warnings if multiple bindings are defined for the same queue and broker.

If validation fails, a `CarotteConfigurationException` is thrown with details about the configuration errors.

#### Advanced Configuration
For advanced scenarios, you can customize your consumers and messages using attributes:
- `[Queue("name", ...)]`: Configures the queue, its primary binding, QoS, and the queue declaration flags `durable`, `exclusive`, and `autoDelete`.
- `[Binding("exchange", "routingKey", ...)]`: Adds a binding and can optionally declare its source exchange.
- `[Published(...)]`: Configures the broker, exchange, publication routing key, exchange type, and exchange declaration flags.
- `[MessageType("alias")]`: Overrides the default message type identifier for interoperability or versioning.

Exchanges are declared automatically by default (`declareExchange: true`). Set `declareExchange: false` when targeting pre-existing exchanges or running in environments with restricted permissions. The available exchange flags are `exchangeType`, `exchangeDurable` (or `durable`), and `exchangeAutoDelete` (or `autoDelete`); queue flags are `durable`, `exclusive`, and `autoDelete`.

> [!NOTE]
> In the current implementation, applying `[Queue]` switches the consumer to attribute-based topology. If you want the default E2E convention (`x.pub.*` -> `x.sub.*` -> `q.*`), do not add `[Queue]` to the consumer.

`[Published]` is applied to the **message type**, not to the consumer:

```csharp
[Published(broker: "my-broker", exchange: "orders-exchange")]
public record CreateOrderCommand(Guid OrderId);
```

For a Carotte-owned topic exchange and an explicit publication key:

```csharp
[Published(
    broker: "my-broker",
    exchange: "orders-exchange",
    routingKey: "order.created",
    exchangeType: ExchangeType.Topic,
    declareExchange: true)]
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
For a message type annotated with `[Published]`, Carotte registers `IPublisher<TMessage>`. By default, the publisher publishes to a `fanout` exchange whose name is derived from the message class name (kebab-case) with an `x.pub.` prefix. Common suffixes like `Message`, `Event`, or `Command` are automatically removed.
- **Message**: `CreateOrderCommand`
- **Exchange**: `x.pub.create-order`
- **Routing Key**: Empty (since it's a `fanout`).

#### Consumer Side (Reception)
Carotte automatically creates a two-level mesh:
1. **Message Exchange (Source)**: A global exchange for the message type. Its name is the kebab-case version of the message class prefixed by `x.pub.` (e.g., `x.pub.order-created`).
2. **Consumer Exchange (Destination)**: An internal exchange named after the consumer class in kebab-case, prefixed by `x.sub.`. If a `ServiceName` is configured, it is included in the prefix: `x.sub.{service-name}.{consumer-name}`.
3. **The Mesh (E2E)**: Carotte binds the message exchange to the consumer exchange.
4. **The Queue**: The consumer exchange is bound to the final queue: `q.{consumer-name}` (or `q.{service-name}.{consumer-name}`).

**Example of generated topology (without ServiceName):**
`[Exchange: x.pub.order-created]` --(E2E)--> `[Exchange: x.sub.order-consumer]` --(Binding)--> `[Queue: q.order-consumer]`

#### Naming Reference

| Input | Generated name |
| :--- | :--- |
| Message `OrderCreatedMessage` | `x.pub.order-created` |
| Message `OrderCreatedEvent` | `x.pub.order-created` |
| Message `CreateOrderCommand` | `x.pub.create-order` |
| Consumer `OrderConsumer` | `x.sub.order-consumer` |
| Consumer `OrderConsumer` with `ServiceName = "order-service"` | `x.sub.order-service.order-consumer` |
| Queue for `OrderConsumer` | `q.order-consumer` |
| Queue for `OrderConsumer` with `ServiceName = "order-service"` | `q.order-service.order-consumer` |

#### RabbitMQ Declaration Defaults

With convention-based topology, Carotte declares:

| Resource | Type/settings |
| :--- | :--- |
| Message exchange | `fanout`, durable, not auto-delete |
| Consumer exchange | `fanout`, durable, not auto-delete |
| Queue | durable, non-exclusive, not auto-delete |
| Message exchange -> consumer exchange binding | routing key `""` |
| Consumer exchange -> queue binding | routing key `""` |

With attribute-based topology, queue flags can be configured on `[Queue]`. Source exchanges are declared by default (`declareExchange: true`), but declaration can be disabled by setting `declareExchange: false` on `[Queue]` or `[Binding]`.

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

Apply `[Published]` to the message type:

```csharp
[Published(broker: "my-broker", exchange: "orders-exchange")]
public record OrderCreatedMessage(Guid OrderId, string CustomerName, decimal Amount);
```

Then inject `IPublisher<OrderCreatedMessage>` as usual.

With an explicit exchange, the default publication routing key remains the short CLR message type name. Set `routingKey` on `[Published]` to override it. Convention-based fanout publication uses an empty routing key.

### Existing-topology checklist

- Confirm the target project can run on .NET 10.
- Confirm the RabbitMQ exchange names, queue names, routing keys, exchange types, and declaration flags.
- Prefer `[Queue]`, `[Binding]`, and `[Published]` over conventions when connecting to shared RabbitMQ resources.
- Confirm that the message `Type` property is populated consistently when one consumer handles multiple message types.
- Confirm JSON compatibility with existing producers and consumers.

## 🏗️ Architecture (Consumers & BackgroundServices)

In the **Carotte** project, the relationship between `consumers` and `BackgroundServices` is a **host-to-guest** relationship.

### 1. The Consumer (`IConsumer<TMessage>`): Business Logic
The `Consumer` is a simple class that implements the `IConsumer<TMessage>` interface. Its sole role is to process a message once it has been received and deserialized.
- It is **passive**: it doesn't know where the message comes from or how it was retrieved.
- It is registered as a scoped service in the dependency injection (DI) container.

Carotte creates a new scope for every received message and disposes it after acknowledgment or rejection. All retry attempts for that message share the same scope and the same scoped consumer instance.

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
- **Auto-repair**: If a channel is detected as closed (`IsOpen == false`), Carotte disposes it and recreates a new channel during the next operation without registering a duplicate host.
- **Clean shutdown**: `CloseAsync` closes/disposes the current channel, unregisters the host from the connection manager, and clears local client state so the client can reconnect cleanly later.

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
    carotte.ScanAssemblies(typeof(Program).Assembly);
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

## 🧪 Testing with TestKit

Carotte provides `Carotte.TestKit` to facilitate functional in-memory testing of microservices without needing a live RabbitMQ broker.

`CarotteTestKit` executes the complete middleware pipeline (observability with OpenTelemetry tracing and metrics, deserialization, retries, and DI scope isolation) and intercepts all published messages in memory.

### Setup

Register the TestKit in your test `IServiceCollection` (e.g. in `WebApplicationFactory` or test fixture):

```csharp
services.AddCarotte(carotte =>
{
    carotte.AddBroker("my-broker", _ => { });
    carotte.ScanAssemblies(typeof(Program).Assembly);
});

// Replaces RabbitMQ publishers with InMemoryPublisher and registers CarotteTestKit
services.AddCarotteTestKit();
```

### Consuming Messages in Tests

You can simulate message consumption using various overloads of `ConsumeAsync`:

```csharp
var testKit = host.Services.GetRequiredService<CarotteTestKit>();

// 1. Explicit consumer and message types
TestDeliveryResult result = await testKit.ConsumeAsync<OrderConsumer, OrderCreatedMessage>(orderCreated);

// 2. Inferred message type from instance
TestDeliveryResult result = await testKit.ConsumeAsync<OrderConsumer>(orderCreated);

// 3. Automatic consumer discovery and dispatch (broadcasts to all matching consumers)
IReadOnlyList<TestDeliveryResult> results = await testKit.ConsumeAsync(orderCreated);
```

### Inspecting Delivery Results (`TestDeliveryResult`)

`ConsumeAsync` returns a `TestDeliveryResult` detailing how the pipeline handled the message (including retries, duration, and error status):

```csharp
var result = await testKit.ConsumeAsync<OrderConsumer>(orderCreated);

// Assert delivery outcome
Assert.True(result.IsAcked);
Assert.False(result.IsNacked);
Assert.False(result.Requeued);
Assert.Null(result.Exception);
Assert.True(result.ElapsedTime > TimeSpan.Zero);
```

### Fluent Assertions & Published Messages

`CarotteTestKit` records all messages published via `IPublisher<T>` and provides fluent assertions and reactive waiting helpers:

```csharp
// Assert that a message was published (and retrieve it)
var published = testKit.ShouldHavePublished<OrderProcessedMessage>(msg => msg.OrderId == orderId);

// Assert that no matching message was published
testKit.ShouldNotHavePublished<OrderFailedMessage>();

// Wait asynchronously for background or delayed publishing (event-driven, non-polling)
var delayed = await testKit.WaitForPublishedMessageAsync<NotificationMessage>(
    predicate: msg => msg.UserId == "user-123",
    timeout: TimeSpan.FromSeconds(2)
);

// Get all published messages of a given type
IReadOnlyList<OrderProcessedMessage> allMessages = testKit.GetPublishedMessages<OrderProcessedMessage>();

// Clear message history between test steps
testKit.Clear();
```

## 📚 Documentation Generation (Carotte.DocCli & Carotte.Documentation)

Carotte provides automated generation of Markdown documentation and interactive Mermaid topology diagrams from your compiled microservice assemblies.

### Using the CLI (`Carotte.DocCli`)

Generate a complete messaging specification for your microservice directly from the command line:

```bash
dotnet run --project Carotte.DocCli -- --assembly ./src/MyService/bin/Release/net10.0/MyService.dll --output ./docs/MESSAGING.md
```

Options:
- `-a, --assembly <path>`: (Required) Path to the compiled assembly (`.dll`).
- `-o, --output <path>`: Output path for the generated Markdown file (defaults to stdout).
- `-t, --title <title>`: Custom title for the documentation.
- `-x, --xml-doc <path>`: Path to XML documentation file (`/// <summary>`) for enriching data contract tables.
- `-n, --namespaces <list>`: Comma-separated list of namespaces to include.
- `--no-diagram`: Disable Mermaid diagram generation.
- `--no-contracts`: Disable data contracts schemas.

For more details, CI/CD pipeline integration, and Markdown rendering examples, see the [Carotte.DocCli README](Carotte.DocCli/README.md).

### Using C# Programmatically (`Carotte.Documentation`)

You can also generate documentation or run architecture tests directly in C#:

```csharp
using Carotte.Documentation;

var generator = new CarotteDocGenerator();
string markdown = generator.Generate(typeof(Program).Assembly);

// Or write directly to a file
await generator.GenerateToFileAsync(typeof(Program).Assembly, "docs/MESSAGING.md");
```

## 🏗️ Project Structure

- `Carotte/`: Core library containing the RabbitMQ client wrapper and pipeline logic.
- `Carotte.Documentation/`: Documentation generator library (Markdown, Mermaid diagrams, schemas).
- `Carotte.DocCli/`: CLI tool to generate Markdown documentation from assemblies.
- `Carotte.Documentation.Tests/`: Unit tests for the documentation generation engine.
- `Carotte.Sample/`: A sample ASP.NET Core application demonstrating usage.
- `Carotte.TestKit/`: Testing framework for mocking and simulating messages.
- `Carotte.Tests/`: Unit and integration tests for the project.
- `Carotte.Benchmarks/`: Performance benchmarks using BenchmarkDotNet.

## 📜 Commands

- **Build**: `dotnet build`
- **Run Sample**: `dotnet run --project Carotte.Sample`
- **Test**: `dotnet test`
- **Generate Documentation**: `dotnet run --project Carotte.DocCli -- --assembly Carotte.Sample/bin/Debug/net10.0/Carotte.Sample.dll`
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
Use `[Published]` on every message type produced by the service.

```csharp
// Publisher registered as IPublisher<CreateOrderCommand>
// Exchange: "x.pub.create-order"
[Published]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);
```

### 5. Publisher with Explicit Broker or Exchange
Use `[Published]` parameters when a produced message must target a specific broker or exchange.

```csharp
[Published(broker: "orders-broker", exchange: "orders-exchange")]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);
```

## 🚀 CI/CD & Releases

Automated workflows are set up via GitHub Actions:
- **CI**: Runs on every pull request and push to validate build, formatting, and tests with code coverage.
- **Release**: Automatically triggered on Git tag push (e.g., `v1.0.0`) or manually via `workflow_dispatch` to package and publish to NuGet.org with **Trusted Publishing (OIDC)** and GitHub Packages.

For the full setup instructions and step-by-step release guide, see the [CI/CD & Release Guide](docs/CICD.md).

## 📄 License

TODO: Specify License (likely MIT or Apache-2.0).

---

*Made with ❤️ and 🥕*
