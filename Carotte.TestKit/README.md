# Carotte.TestKit 🥕🧪

[![NuGet Version](https://img.shields.io/nuget/v/Carotte.TestKit.svg)](https://www.nuget.org/packages/Carotte.TestKit)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

**Carotte.TestKit** provides lightweight in-memory testing utilities, consumer pipeline simulation, and publication assertions for **Carotte** microservices without requiring a running RabbitMQ broker or Docker container.

---

## 🚀 Key Features

- ⚡ **Zero-Broker Execution**: Run hundreds of unit and integration tests in milliseconds without spinning up Docker or RabbitMQ.
- 🎯 **Full Pipeline Simulation**: `ConsumeAsync` executes the real consumer pipeline (middleware, serialization, error strategies, retries, and DI scopes).
- 📊 **Detailed Delivery Inspection**: `TestDeliveryResult` provides exact metrics on execution time, retry attempts, ACK / NACK status, requeue flags, and unhandled exceptions.
- 📬 **In-Memory Publishing & Interception**: Replaces `IPublisher<T>` with an in-memory test store (`MessageTestStore`) to capture and inspect published messages.
- 🔍 **Fluent Assertions**: Assert published messages with LINQ predicates (`ShouldHavePublished<T>`, `ShouldNotHavePublished<T>`).
- ⏳ **Async Event-Driven Waiting**: `WaitForPublishedMessageAsync<T>` allows waiting for asynchronous/background publications without flaky `Task.Delay` polling loops.

---

## 📦 Installation

Install the package into your test project:

```bash
dotnet add package Carotte.TestKit
```

---

## 🏁 Quick Start

### 1. Register Carotte TestKit in Your Test Host

You can register Carotte TestKit directly inside `AddCarotte` with `UseTestKit()` or via `services.AddCarotteTestKit()`:

```csharp
using Carotte;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var hostBuilder = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddCarotte(carotte =>
        {
            carotte.AddBroker("test-broker", _ => { });
            carotte.ScanAssemblies(typeof(OrderConsumer).Assembly);
            carotte.UseTestKit(); // Fluent integration
        });
    });

var host = hostBuilder.Build();
var testKit = host.Services.GetRequiredService<CarotteTestKit>();
```

---

### 2. Simulate Consumer Message Reception

Simulate incoming messages through the complete consumer pipeline:

```csharp
var orderMessage = new OrderCreatedEvent(Guid.NewGuid(), "Alice", 99.99m);

// Simulate receipt by a specific consumer
TestDeliveryResult result = await testKit.ConsumeAsync<OrderConsumer, OrderCreatedEvent>(orderMessage);

// Assert the message was acknowledged
Assert.True(result.IsAcked);
Assert.False(result.IsNacked);
Assert.False(result.Requeued);
Assert.Null(result.Exception);
Assert.True(result.ElapsedTime > TimeSpan.Zero);
```

---

### 3. Assert Published Messages

Verify that business logic published expected messages via `IPublisher<T>`:

```csharp
// Retrieve published message matching a condition
var published = testKit.ShouldHavePublished<OrderConfirmationSent>(msg => msg.OrderId == orderId);

// Assert that a specific message was never published
testKit.ShouldNotHavePublished<OrderFailedEvent>();

// Get all published messages of a type
IReadOnlyList<OrderConfirmationSent> allSent = testKit.GetPublishedMessages<OrderConfirmationSent>();

// Reset recorded messages between tests
testKit.Clear();
```

---

### 4. Wait for Background / Delayed Publications

When testing asynchronous event handlers or background workers:

```csharp
// Asynchronously awaits publication matching predicate (event-driven, non-polling)
var delayedEvent = await testKit.WaitForPublishedMessageAsync<InvoiceGeneratedEvent>(
    predicate: msg => msg.CustomerId == "cust-42",
    timeout: TimeSpan.FromSeconds(2)
);

Assert.NotNull(delayedEvent);
```

---

## 📄 License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).
