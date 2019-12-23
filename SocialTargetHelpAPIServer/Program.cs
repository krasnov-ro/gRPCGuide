using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SocialTargetHelpAPI.Contract;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Topshelf;

namespace SocialTargetHelpAPIServer
{
    class Program
    {
        internal static ServiceProvider AppServiceProvider = null;

        private static IConfigurationRoot ApplicationConfig = null;

        static void Main(string[] args)
        {
            var runAsService = !(Debugger.IsAttached || args.Contains("--console"));
            if (runAsService)
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }

            ApplicationConfig = GetApplicationConfiguration();

            ConfigureLogging();

            AppServiceProvider = ConfigureServices();

            var logger = AppServiceProvider.GetService<ILogger<Program>>();

            var grpcServer = CreateGrpcServer();

            if (runAsService)
            {
                logger.LogInformation($"Main. Starting service...");

                HostFactory.Run(serviceConfig =>
                {
                    serviceConfig.Service<GrpcHostedService>(serviceInstance =>
                    {
                        serviceInstance.ConstructUsing(() => new GrpcHostedService(grpcServer));
                        serviceInstance.WhenStarted(execute => execute.Start());
                        serviceInstance.WhenStopped(execute => execute.Stop());
                    });
                    serviceConfig.SetServiceName("_SocialTargetHelpAPI");
                    serviceConfig.SetDisplayName("_SocialTargetHelpAPI");
                    serviceConfig.StartAutomatically();
                });

                logger.LogInformation($"Main. Service started");
            }
            else
            {
                logger.LogInformation($"Main. Starting console app...");

                grpcServer.Start();

                logger.LogInformation($"Main. Console app started");

                Console.ReadKey();
            }

            Log.CloseAndFlush();
        }

        private static ServiceProvider ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging(configure => configure.AddSerilog());

            //serviceCollection.AddSingleton<ApiService.ApiServiceBase, ApiServiceImpl>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            return serviceProvider;
        }

        private static IConfigurationRoot GetApplicationConfiguration()
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", true, true)
                .AddJsonFile("secureSettings.json", true, true)
                .AddJsonFile("serilogConfig.json");
            var configuration = configBuilder.Build();

            return configuration;
        }

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(ApplicationConfig)
                .CreateLogger();
        }

        private static Server CreateGrpcServer()
        {
            var connectionString = Program.ApplicationConfig.GetConnectionString("MyDb");
            var listenPorts = Program.ApplicationConfig.GetSection("listenPorts").GetChildren()
                .Select(p => new ServerPort(p.GetSection("host").Value, Convert.ToInt32(p.GetSection("port").Value), ServerCredentials.Insecure))
                .ToArray();

            var server = new Server
            {
                Services = { ApiService.BindService(new ApiServiceImpl("PostgreSQL.9.5", connectionString)) }
            };
            foreach (var listenPort in listenPorts)
                server.Ports.Add(listenPort);

            return server;
        }
    }
}

