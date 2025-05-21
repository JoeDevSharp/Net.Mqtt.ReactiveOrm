using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MqttORM.Bus;
using MqttORM.Bus.Interfaces;
using MqttORM.Configuration;
using MqttORM.Serialization;

namespace MqttORM.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMqttOrm(this IServiceCollection services, Action<MqttOrmBuilder> configure)
        {
            var builder = new MqttOrmBuilder();
            configure(builder);
            var options = builder.Options;

            services.AddSingleton(options);
            services.AddSingleton<MqttSerializer>();

            // Register MqttFactory and client (v5 compatible)
            var factory = new MqttClientFactory();
            services.AddSingleton(factory);
            services.AddSingleton(sp => factory.CreateMqttClient());

            // Register MqttClientOptions (TLS aware)
            services.AddSingleton(sp =>
            {
                return new MqttClientOptionsBuilder()
                .WithTcpServer(options.Server)
                .Build();
            });

            services.AddSingleton<IMqttBus, MqttBus>();
            RegisterAllMqttHandlers(services);

            return services;
        }

        private static void RegisterAllMqttHandlers(IServiceCollection services)
        {
            var handlerInterface = typeof(IMqttHandler<>);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface);

                if (interfaces.Any())
                {
                    services.AddSingleton(type);
                }
            }
        }
    }
}
