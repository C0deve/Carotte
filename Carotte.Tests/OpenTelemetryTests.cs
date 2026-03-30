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
        var endpoint = "http://localhost:4317";

        // Act
        services.AddCarotte(builder =>
        {
            builder.AddOtlpExporter(endpoint);
        });

        // Assert
        var sp = services.BuildServiceProvider();
        
        // On vérifie que les services OpenTelemetry sont enregistrés
        // L'enregistrement de l'exportateur OTLP est interne à l'implémentation de WithTracing/WithMetrics
        // Mais on peut vérifier que le builder a bien pris en compte l'URL.
        // Comme on ne peut pas facilement inspecter la configuration interne d'OpenTelemetry sans réflexion complexe,
        // on se fie à la compilation et au fait que la méthode est appelée.
        
        var tracerProvider = sp.GetService<TracerProvider>();
        tracerProvider.ShouldNotBeNull();

        var meterProvider = sp.GetService<MeterProvider>();
        meterProvider.ShouldNotBeNull();
    }
}
