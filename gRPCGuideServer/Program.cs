using Grpc.Core;
using System;
using gRPCGuideContract.Contract;
using gRPCGuideServer.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace gRPCGuideServer
{
    class Program
    {

        const int Port = 88;

        static void Main(string[] args)
        {
            Server server = new Server
            {
                Services = { RouteGuide.BindService(new RouteGuideImpl()) },
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

