namespace Carotte.Benchmarks.Messages;

// --- Basic Message Payloads ---

public record SimpleBenchmarkMessage(string Content);

[MessageType("custom.benchmark.order")]
public record CustomAliasedMessage(Guid Id, string CustomerName);

public record OrderItem(string Sku, int Quantity, decimal Price);

public record OrderCreatedBenchmarkMessage(
    Guid OrderId,
    string CustomerName,
    List<OrderItem> Items,
    decimal TotalAmount);

public record LargePayloadBenchmarkMessage(
    Guid Id,
    string Title,
    byte[] Data,
    Dictionary<string, string> Metadata);

// --- Numbered Messages for Multi-Type Resolution Benchmarks (1..20) ---

public record BenchmarkMessage1(string Text);
public record BenchmarkMessage2(string Text);
public record BenchmarkMessage3(string Text);
public record BenchmarkMessage4(string Text);
public record BenchmarkMessage5(string Text);
public record BenchmarkMessage6(string Text);
public record BenchmarkMessage7(string Text);
public record BenchmarkMessage8(string Text);
public record BenchmarkMessage9(string Text);
public record BenchmarkMessage10(string Text);
public record BenchmarkMessage11(string Text);
public record BenchmarkMessage12(string Text);
public record BenchmarkMessage13(string Text);
public record BenchmarkMessage14(string Text);
public record BenchmarkMessage15(string Text);
public record BenchmarkMessage16(string Text);
public record BenchmarkMessage17(string Text);
public record BenchmarkMessage18(string Text);
public record BenchmarkMessage19(string Text);
public record BenchmarkMessage20(string Text);

// --- Consumers for Invocation and Mediation Benchmarks ---

public class SimpleMessageConsumer : IConsumer<SimpleBenchmarkMessage>
{
    public Task HandleAsync(SimpleBenchmarkMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class MultiMessageConsumer :
    IConsumer<SimpleBenchmarkMessage>,
    IConsumer<CustomAliasedMessage>,
    IConsumer<OrderCreatedBenchmarkMessage>
{
    public Task HandleAsync(SimpleBenchmarkMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task HandleAsync(CustomAliasedMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task HandleAsync(OrderCreatedBenchmarkMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

[Queue("benchmark-queue", exchange: "benchmark-exchange")]
public class ExplicitQueueConsumer : IConsumer<SimpleBenchmarkMessage>
{
    public Task HandleAsync(SimpleBenchmarkMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
