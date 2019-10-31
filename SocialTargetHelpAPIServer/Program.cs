using Grpc.Core;
using System;
using System.Linq;
using SocialTargetHelpAPIContract;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace SocialTargetHelpAPIServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", true, true)
                .AddJsonFile("secureSettings.json", true, true);
            var configuration = builder.Build();

            var listenPorts = configuration.GetSection("listen_ports").GetChildren()
                .Select(p => new ServerPort(p.GetSection("host").Value, Convert.ToInt32(p.GetSection("port").Value), ServerCredentials.Insecure))
                .ToArray();
            var connectionString = configuration.GetConnectionString("MyDb");

            Server server = new Server
            {
                Services = { RouteGuide.BindService(new RouteGuideImpl("PostgreSQL.9.5", connectionString)) }
            };
            foreach (var listenPort in listenPorts)
                server.Ports.Add(listenPort);
            server.Start();

            var listening = String.Join(Environment.NewLine, listenPorts.Select(p => $"\t{p.Host}:{p.Port}"));
            Console.WriteLine($"Server is running and listening to:{Environment.NewLine}{listening}");
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}

