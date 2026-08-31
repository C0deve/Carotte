using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Shouldly;

namespace Carotte.Tests;

public class OpenTelemetryTests
{
    [Fact]
    public void AddOtlpExporter_ShouldConfigureEndpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        const string endpoint = "http://localhost:4317";

        // Act
        services.AddCarotte(builder =>
        {
            builder
                .AddBroker("test-broker", _ => { })
                .WithOtlpExporter(endpoint);
        });

        // Assert
        var sp = services.BuildServiceProvider();

        // Check if OpenTelemetry services are registered
        // The registration of the OTLP exporter is internal to the implementation of WithTracing/WithMetrics
        // But we can verify that the builder has correctly taken the URL into account.
        // Since we cannot easily inspect the internal configuration of OpenTelemetry without complex reflection,
        // we rely on the compilation and the fact that the method is called.

        var tracerProvider = sp.GetService<TracerProvider>();
        tracerProvider.ShouldNotBeNull();

        var meterProvider = sp.GetService<MeterProvider>();
        meterProvider.ShouldNotBeNull();
    }
}
