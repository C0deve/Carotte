# Carotte 🥕

[![NuGet Version](https://img.shields.io/nuget/v/Carotte.svg)](https://www.nuget.org/packages/Carotte)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

> [!WARNING]
> This library is currently under development and is not yet ready for production use.
> This is a **Proof of Concept (PoC)** created entirely using **Junie**, with a focus on writing as little code as possible.

**Carotte** is a high-level, high-performance RabbitMQ client wrapper for **.NET 10**, designed for seamless microservices communication with built-in observability, convention-based topologies, resilient error handling, and zero-allocation execution pipelines.

---

## 🚀 Key Features

- 🏎️ **Optimized for .NET 10 & C# 14**: Modern patterns, zero-allocation typed invokers (`IMessageInvoker`), and minimal heap footprint on the hot path.
- 🔭 **Built-in Observability**: Native OpenTelemetry integration (Distributed Tracing `ActivitySource` & Prometheus/OTLP Metrics).
- 🔄 **Convention over Configuration**: Automatic queue, exchange, and binding topologies (`x.pub.*` $\rightarrow$ `x.sub.*` $\rightarrow$ `q.*`).
- 🛡️ **Resilience & Dead-Letter Handling**: Built-in exponential backoff retries, non-transient poison message rejection, and automated Dead-Letter Exchanges/Queues (DLX/DLQ).
- 🧩 **Flexible Consumers & Publishers**: Strongly-typed `IConsumer<TMessage>` (single or multi-message handling) and explicit `IPublisher<TMessage>`.
- 🔌 **Extensible Pipelines**: Interceptor middlewares for publishers and consumers (Tracing, Metrics, Serialization, Validation).
- 📦 **Automatic DI Registration**: Assembly scanning with namespace filtering for automatic discovery of handlers.

---

## 📦 Installation

Install the core package via NuGet:

```bash
dotnet add package Carotte
```

### Complementary Packages

| Package | Description |
| :--- | :--- |
| [`Carotte.Documentation`](https://www.nuget.org/packages/Carotte.Documentation) | Automated Markdown, AsyncAPI v3.0 & Mermaid topology generator |
| [`Carotte.TestKit`](https://www.nuget.org/packages/Carotte.TestKit) | In-memory testing utilities and pipeline simulation |

---

## 🏁 Quick Start

### 1. Register Carotte in Dependency Injection

In your `Program.cs`:

```csharp
using Carotte;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarotte(carotte =>
{
    // Configure RabbitMQ broker connection
    carotte.AddBroker("default", options =>
    {
        options.Host = "localhost";
        options.UserName = "guest";
        options.Password = "guest";
        options.DefaultPrefetchCount = 10; // Optional: Default is 1 (strict FIFO)
    });

    // Optional: Prefix queues and exchanges with client/service name
    carotte.SetClientName("order-service");

    // Scan assembly for IConsumer<T> handlers and [Published] message types
    carotte.AddAssemblies(typeof(Program).Assembly);

    // Optional: Configure OpenTelemetry OTLP Exporter
    carotte.AddOtlpExporter("http://localhost:4317");
});

var app = builder.Build();
app.Run();
```

---

### 2. Define a Message and a Consumer

```csharp
// Define message contract
public record OrderCreatedEvent(Guid OrderId, string CustomerName, decimal Amount);

// Define consumer (queue is automatically named 'q.order-service.order-consumer')
public class OrderConsumer(ILogger<OrderConsumer> logger) : IConsumer<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing order {OrderId} for {Customer}", message.OrderId, message.CustomerName);
        return Task.CompletedTask;
    }
}
```

---

### 3. Publish Messages

Mark messages with `[Published]` to enable publishing:

```csharp
[Published]
public record CreateOrderCommand(Guid OrderId, string CustomerName, decimal Amount);

public class OrderService(IPublisher<CreateOrderCommand> publisher)
{
    public async Task CreateOrderAsync(Guid orderId, string customer, decimal amount)
    {
        await publisher.PublishAsync(new CreateOrderCommand(orderId, customer, amount));
    }
}
```

---

## ⚙️ Advanced Topologies & Dead Letters

Customize queues, routing keys, and dead-letter strategies with attributes:

```csharp
[Queue(
    Name = "custom-orders-queue",
    Exchange = "orders-exchange",
    RoutingKey = "orders.created",
    MaxRetryAttempts = 3,
    DeadLetterExchange = "x.orders-dlx",
    DeadLetterQueue = "q.orders-dlq")]
public class CustomOrderConsumer : IConsumer<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
    {
        // Handling logic
        return Task.CompletedTask;
    }
}
```

---

## 📄 License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).
