using Grpc.Core;
using Microsoft.Extensions.Configuration;
using SocialTargetHelpAPIClient.Models;
using SocialTargetHelpAPIContract;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SocialTargetHelpAPIClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Channel channel = new Channel("localhost:88", ChannelCredentials.Insecure);
            var client = new RouteGuide.RouteGuideClient(channel);
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");

            var builder = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("secureSettings.json", true, true);

            var configuration = builder.Build();

            //var dbContext = new STH("PostgreSQL.9.5", configuration.GetConnectionString("MyDb"));

            do
            {
                Console.WriteLine("Type a data person:");

                #region Запрос от УФСИН 

                //var cd_deals = dbContext.fsin_cd_deals;
                //GetPersonLifeStatusRequest[] persons = null;
                //var cd_persons = dbContext.public_cd_persons;
                //IQueryable<public_cd_persons> dbPersonsFilter = null;
                //foreach (var dbDeal in cd_deals)
                //{
                //    if (dbPersonsFilter != null)
                //       dbPersonsFilter = dbPersonsFilter.Concat(cd_persons.Where(p => p.id == dbDeal.f_cd_persons));

                //    else
                //        dbPersonsFilter = cd_persons.Where(p => p.id == dbDeal.f_cd_persons);
                //}
                //var personsData = dbPersonsFilter.Select(dbPerson => new GetPersonLifeStatusRequest()
                //{
                //    LastName = dbPerson.c_surname,
                //    FirstName = dbPerson.c_first_name,
                //    MiddleName = dbPerson.c_patronymic,
                //    BirthDate = dbPerson.d_birthday.ToString()
                //    //Guid = id
                //})
                //    .ToArray();

                //GetPersonLifeStatusListRequest FsinRequest = new GetPersonLifeStatusListRequest()
                //{
                //    Obj = { personsData }
                //};

                //#region Заполение массива запроса через Foreach
                ////foreach (var dbPerson in dbPersons)
                ////{
                ////    string id = Guid.NewGuid().ToString();
                ////    persons = new GetPersonLifeStatusRequest[]
                ////    {
                ////        new GetPersonLifeStatusRequest()
                ////        { LastName = dbPerson.CLastName, FirstName = dbPerson.CFirstName, MiddleName = dbPerson.CMiddleName, BirthDate = dbPerson.DBirthDate.ToString("dd.MM.yyyy"), Guid = id }
                ////    };

                ////    FsinRequest.Obj.Add(persons);
                ////}
                //#endregion

                //var FsinResponse = client.GetPersonLifeStatus(FsinRequest);
                ////var FsinResult =
                ////    "\nФИО: " + FsinResponse.LastName + " " + FsinResponse.FirstName + " " + FsinResponse.MiddleName + "\n" +
                ////    "Дата рождения: " + FsinResponse.BirthDate + "\n" +
                ////    "Статус: " + FsinResponse.Status;

                //Console.WriteLine();
                //Console.WriteLine($"Информация о человеке: ");
                #endregion

                Console.WriteLine("--------------------------------------------------------");

                #region Запрос от социального портала

                var socPortalReq = new GetPersonPaymentsListRequest()
                {
                    PeriodBegin = "2016-10-23",
                    PeriodEnd = "2019-10-23",
                    Snils = "024-030-563-89"
                };

                var socPortalRes = client.GetPersonPaymentsList(socPortalReq);
                #endregion
                foreach(var t in socPortalRes.Payments)
                {
                    Console.WriteLine("\nCalculationDate: " + t.DateCalculation.ToString(culture) +
                                      "\nBeginDate: " + t.DateBegin.ToString(culture) +
                                      "\nEndDate: " + t.DateEnd.ToString(culture) +
                                      "\nTitle: " + t.Title +
                                      "\nName: " + t.Name +
                                      "\nPaymentSum: " + t.PaymentSum);
                }
                Console.WriteLine("________________________________________________________");
                Console.WriteLine("\nPress <Escape> to exit, press any other key to repeat...\n");
            }
            while (Console.ReadKey().Key != ConsoleKey.Escape);

            channel.ShutdownAsync().Wait();
        }
    }
}
