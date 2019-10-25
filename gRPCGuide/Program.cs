using Grpc.Core;
using gRPCGuideContract.Contract;
using System;

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

                Console.Write("Sender Code: ");
                var senderCode = Console.ReadLine();

                #region Запрос от УФСИН 
                var FsinRequest = new GetPersonLifeStatusRequest()
                {
                    LastName = lastName,
                    FirstName = firstName,
                    MiddleName = middleName,
                    BirthDate = birthDate,
                    SenderCode = senderCode
                };
                var FsinResponse = client.GetPersonLifeStatus(FsinRequest);
                var FsinResult =
                    "\nФИО: " + FsinResponse.LastName + " " + FsinResponse.FirstName + " " + FsinResponse.MiddleName + "\n" +
                    "Дата рождения: " + FsinResponse.BirthDate + "\n" +
                    "Статус: " + FsinResponse.Status;

                Console.WriteLine();
                Console.WriteLine($"Информация о человеке: {FsinResult}");
                #endregion

                Console.WriteLine("--------------------------------------------------------");

                #region Запрос от социального портала
                var SocCapRequest = new SocialCapRequest()
                {
                    LastName = lastName,
                    FirstName = firstName,
                    MiddleName = middleName,
                    BirthDate = birthDate
                };
                var SocCapResponse = client.SocialCapMessage(SocCapRequest);
                var SocCapResult =
                    "Статус: " + SocCapResponse.Status;
                Console.WriteLine($"\nSocialCap send result:\n {SocCapResult}");

                #endregion

                Console.WriteLine("________________________________________________________");
                Console.WriteLine("\nPress <Escape> to exit, press any other key to repeat...\n");
            }
            while (Console.ReadKey().Key != ConsoleKey.Escape);

            channel.ShutdownAsync().Wait();
        }
    }
}
