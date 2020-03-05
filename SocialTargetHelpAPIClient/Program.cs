using Grpc.Core;
using Microsoft.Extensions.Configuration;
using SocialTargetHelpAPIClient.Models;
using SocialTargetHelpAPI.Contract;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace SocialTargetHelpAPIClient
{
    class Program
    {
        private static readonly CultureInfo cultureEnUs = CultureInfo.CreateSpecificCulture("en-US");

        static void Main(string[] args)
        {
            var endpoint = "localhost:8088";

            var clientCredentials = GetGrpcClientCredentials();

            var channel = new Channel(endpoint, clientCredentials);
            var client = new ApiService.ApiServiceClient(channel);

            var builder = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("secureSettings.json", true, true);

            var configuration = builder.Build();

            var dbContext = new STH("PostgreSQL.9.5", configuration.GetConnectionString("MyDb"));

            var res = client.GetVeteranDictionaries(new GetVeteranDictionariesRequest() { });

            do
            {
                Console.WriteLine("Type a data person:");

                #region Запрос от УФСИН 

                var cd_deals = dbContext.fsin_cd_deals;
                GetPersonsLifeStatusRequest[] persons = null;
                var cd_persons = dbContext.public_cd_persons;
                IQueryable<public_cd_persons> dbPersonsFilter = null;
                foreach (var dbDeal in cd_deals)
                {
                    if (dbPersonsFilter != null)
                        dbPersonsFilter = dbPersonsFilter.Concat(cd_persons.Where(p => p.id == dbDeal.f_cd_persons));

                    else
                        dbPersonsFilter = cd_persons.Where(p => p.id == dbDeal.f_cd_persons);
                }

                dbPersonsFilter = dbContext.public_cd_persons;
                dbPersonsFilter = dbPersonsFilter.Where(p=>p.c_first_name != null && p.c_patronymic != null && p.c_surname != null).Take(150);

                // Передадим инфу о наших челиках, персональные данные зашифруем
                var personsData = dbPersonsFilter.Select(dbPerson => new PersonLifeStatusRequest()
                {
                    LastName = dbPerson.c_surname.ToUpper(),
                    FirstName = dbPerson.c_first_name.ToUpper(),
                    MiddleName = dbPerson.c_patronymic.ToUpper(),
                    BirthDate = dbPerson.d_birthday.ToString(),
                    DocSeria = Encrypt(dbPerson.c_document_seria),
                    DocNumber = Encrypt(dbPerson.c_document_number),
                    Guid = Guid.NewGuid().ToString()
                }).ToArray();

                #region
                //byte[] toEncryptData = Encoding.ASCII.GetBytes(personsData.ToString());
                //// Generate open and close keys
                //RSACryptoServiceProvider rsaKeysGen = new RSACryptoServiceProvider();
                //string privateKey = rsaKeysGen.ToXmlString(true);
                //string publicKey = rsaKeysGen.ToXmlString(false);

                //// Encode with public key 
                //RSACryptoServiceProvider rsaPublicKey = new RSACryptoServiceProvider();
                //rsaPublicKey.FromXmlString(publicKey);
                //byte[] encryptedData = rsaPublicKey.Encrypt(toEncryptData, false);
                //string EncryptedResult = Encoding.Default.GetString(encryptedData);

                //// Decode with private key
                #endregion

                GetPersonsLifeStatusRequest FsinRequest = new GetPersonsLifeStatusRequest()
                {
                    RequestData = { personsData }
                };

                var FsinResponse = client.GetPersonsLifeStatus(FsinRequest);

                if(FsinResponse.ResponseData.First().Status == PersonLifeStatus.Alive)

                Console.WriteLine();
                Console.WriteLine($"Информация о человеке: ");
                #endregion

                Console.WriteLine("--------------------------------------------------------");

                #region Запрос от социального портала

                // Передаем период выплат за который мы хотим получить информацию о выплатах челика с передаваемым снилсом
                var socPortalReq = new GetPersonPaymentsRequest()
                {
                    PeriodBegin = "2016-10-23",
                    PeriodEnd = "2019-10-23",
                    Snils = "024-030-563-89"
                };

                var socPortalRes = client.GetPersonPayments(socPortalReq);
                #endregion

                foreach (var t in socPortalRes.Payments)
                {
                    Console.WriteLine("\nCalculationDate: " + t.DateCalculation.ToString(cultureEnUs) +
                                      "\nBeginDate: " + t.DateBegin.ToString(cultureEnUs) +
                                      "\nEndDate: " + t.DateEnd.ToString(cultureEnUs) +
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

        public static string Encrypt(string clearText)
        {
            string EncryptionKey = "abc123";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

        private static ChannelCredentials GetGrpcClientCredentials()
        {
            return ChannelCredentials.Insecure;

            var certChainPath = @"D:\a-mikhailov\!Work\Certs\OpenSSL_GrpcTest\OpenSSL_GrpcTest.crt";
            var certPrivateKeyPath = @"D:\a-mikhailov\!Work\Certs\OpenSSL_GrpcTest\OpenSSL_GrpcTest.key";
            var rootCertPath = @"D:\a-mikhailov\!Work\Certs\OpenSSL_CA_A_Mikhailov\OpenSSL_CA_A_Mikhailov.crt";

            var keyCertPair = new KeyCertificatePair(File.ReadAllText(certChainPath), File.ReadAllText(certPrivateKeyPath));
            var credentials = new SslCredentials(File.ReadAllText(rootCertPath), keyCertPair);

            return credentials;
        }
    }
}
