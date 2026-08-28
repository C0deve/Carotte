using Testcontainers.RabbitMq;

namespace Carotte.Tests.EndToEnd;

public abstract class EndToEndTestBase
{
    protected static RabbitMqContainer CreateContainer()
    {
        return new RabbitMqBuilder("rabbitmq:4.2.5")
            .WithImage("rabbitmq:4.0-management")
            .Build();
    }
}
