using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Grpc.Core;
using Topshelf;
using SocialTargetHelpAPI.Contract;

namespace SocialTargetHelpAPIServer
{
    class Program
    {
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

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var logger = serviceProvider.GetService<ILogger<Program>>();

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

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddSerilog());
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

