using Grpc.Core;
using System;
using SocialTargetHelpAPIContract;
using SocialTargetHelpAPIServer.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using SocialTargetHelpAPIServer;

namespace SocialTargetHelpAPIServer
{
    class Program
    {

        const int Port = 88;

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile("secureSettings.json", true, true);

            var configuration = builder.Build();

            Server server = new Server
            {
                Services = { RouteGuide.BindService(new RouteGuideImpl(configuration)) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("Greeter server listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}

