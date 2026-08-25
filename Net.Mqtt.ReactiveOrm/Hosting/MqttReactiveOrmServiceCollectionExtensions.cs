using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Net.Mqtt.ReactiveOrm.Bus;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.CloudEvents;
using Net.Mqtt.ReactiveOrm.Contracts;

namespace Microsoft.Extensions.DependencyInjection;

public static class MqttReactiveOrmServiceCollectionExtensions
{
    public static IServiceCollection AddMqttEventContracts(this IServiceCollection services,
        Action<EventContractRegistryBuilder> configureContracts, IJsonSchemaResolver schemaResolver, int schemaCacheCapacity = 64)
    {
        ArgumentNullException.ThrowIfNull(configureContracts);
        ArgumentNullException.ThrowIfNull(schemaResolver);
        var builder = new EventContractRegistryBuilder();
        configureContracts(builder);
        services.AddSingleton<IEventContractRegistry>(builder.Build());
        services.AddSingleton<IJsonSchemaResolver>(new CachingJsonSchemaResolver(schemaResolver, schemaCacheCapacity));
        services.AddSingleton<IEventDataValidator, JsonSchemaEventDataValidator>();
        return services;
    }

    public static IServiceCollection AddMqttReactiveOrm(this IServiceCollection services, Action<MqttReactiveOrmOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new MqttReactiveOrmOptions { ClientId = string.Empty };
        configure(options);
        options.Validate();
        services.TryAddSingleton(options);
        services.TryAddSingleton<ICloudEventFactory, CloudEventFactory>();
        services.TryAddSingleton<ICloudEventCodec, JsonCloudEventCodec>();
        services.TryAddSingleton<IMqttBus>(provider => new MqttNetBus(provider.GetRequiredService<MqttReactiveOrmOptions>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MqttReactiveOrmHostedService>());
        return services;
    }
}

internal sealed class MqttReactiveOrmHostedService(IMqttBus bus) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => bus.ConnectAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => bus.DisconnectAsync(cancellationToken);
}
