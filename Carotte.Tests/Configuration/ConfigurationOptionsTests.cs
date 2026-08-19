using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Carotte.Tests.Configuration;

public class SampleMessage;

public class SampleConsumer : IConsumer<SampleMessage>
{
    public Task HandleAsync(SampleMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ConfigurationOptionsTests
{
    [Fact]
    public void AddCarotte_WithConfiguration_ShouldBindOptionsAndRegisterServices()
    {
        var json = """
        {
          "Carotte": {
            "ClientName": "order-service",
            "Brokers": {
              "primary": {
                "Host": "rabbit-prod",
                "Port": 5672,
                "VirtualHost": "/sales",
                "UserName": "app_user",
                "Password": "secret_password",
                "DefaultPrefetchCount": 15,
                "ClientProvidedName": "OrderService"
              }
            }
          }
        }
        """;

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        var services = new ServiceCollection();
        services.AddCarotte(configuration.GetSection("Carotte"), carotte =>
        {
            carotte.AddAssemblies(typeof(ConfigurationOptionsTests).Assembly)
                   .AddNamespaces("Carotte.Tests.Configuration");
        });

        var sp = services.BuildServiceProvider();

        var options = sp.GetService<IOptions<CarotteOptions>>()?.Value;
        options.ShouldNotBeNull();
        options.ClientName.ShouldBe("order-service");
        options.Brokers.ShouldContainKey("primary");
        options.Brokers["primary"].Host.ShouldBe("rabbit-prod");
        options.Brokers["primary"].VirtualHost.ShouldBe("/sales");
        options.Brokers["primary"].DefaultPrefetchCount.ShouldBe((ushort)15);
        options.Brokers["primary"].ClientProvidedName.ShouldBe("OrderService");

        var connectionManager = sp.GetService<IConnectionManager>();
        connectionManager.ShouldNotBeNull();
    }

    [Fact]
    public void AddCarotte_WithOptionsAction_ShouldConfigureOptions()
    {
        var services = new ServiceCollection();
        services.AddCarotte(opt =>
        {
            opt.ClientName = "custom-client";
            opt.Brokers["default"] = new RabbitMqOptions
            {
                Host = "remote-host",
                Port = 5672
            };
        }, carotte =>
        {
            carotte.AddAssemblies(typeof(ConfigurationOptionsTests).Assembly)
                   .AddNamespaces("Carotte.Tests.Configuration");
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetService<IOptions<CarotteOptions>>()?.Value;

        options.ShouldNotBeNull();
        options.ClientName.ShouldBe("custom-client");
        options.Brokers.ShouldContainKey("default");
        options.Brokers["default"].Host.ShouldBe("remote-host");
    }
}
