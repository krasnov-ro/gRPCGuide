using Grpc.Core;
using System;
using gRPCGuideContract.Contract;

namespace gRPCGuide
{
    class Program
    {
        static void Main(string[] args)
        {
            Channel channel = new Channel("localhost:88", ChannelCredentials.Insecure);
            var client = new RouteGuide.RouteGuideClient(channel);

            do
            {
                Console.WriteLine("Type a data person:");
                Console.Write("Last Name: ");
                var lastName = Console.ReadLine();

                Console.Write("First Name: ");
                var firstName = Console.ReadLine();

                Console.Write("Middle Name: ");
                var middleName = Console.ReadLine();

                Console.Write("Birth Date: ");
                var birthDate = Console.ReadLine();

                var request = new SendMessageRequest()
                {
                    LastName = lastName,
                    FirstName = firstName,
                    MiddleName = middleName,
                    BirthDate = birthDate
                };
                var response = client.GetMessage(request);
                var result = response.FirstName + " " + response.BirthDate;

                Console.WriteLine();
                Console.WriteLine($"Message send result: {result}");
                Console.WriteLine();
                Console.WriteLine("Press <Escape> to exit, press any other key to repeat...");
            }
            while (Console.ReadKey().Key != ConsoleKey.Escape);

            channel.ShutdownAsync().Wait();
        }
    }
}
