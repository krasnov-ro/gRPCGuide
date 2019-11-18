using System;
using Grpc.Core;
using System.Collections.Generic;
using System.Text;
using SocialTargetHelpAPI.Contract;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Linq;
using SocialTargetHelpAPIServer.Models;
using Npgsql;
using NLog;
using NpgsqlTypes;
using LinqToDB;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.IO;

namespace SocialTargetHelpAPIServer
{
    class ApiServiceImpl : ApiService.ApiServiceBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private String _connectionString = null;
        private STH dbContext = null;

        public ApiServiceImpl(String providerName, String connectionString)
        {
            _connectionString = connectionString;
            dbContext = new STH(providerName, connectionString);
        }

        #region Формировнаие ответа для УФСИН
        public override Task<GetPersonsLifeStatusResponse> GetPersonsLifeStatus(GetPersonsLifeStatusRequest req, ServerCallContext context)
        {
            GetPersonsLifeStatusResponse result = new GetPersonsLifeStatusResponse();
            String personDocData = null;
            PersonLifeStatusRequest[] persons;
            IQueryable<fatalzp_sv_cd_umer> men = null;
            IQueryable<fatalzp_sv_cd_umer> docMen = null;

            try
            {
                foreach (var dbPerson in req.RequestData)
                {
                    men = dbContext.fatalzp_sv_cd_umer.Where(p =>
                        //p.Id.ToString() == dbPerson.Guid &&
                        p.Фамилия.ToUpper() == dbPerson.LastName &&
                        p.Имя.ToUpper() == dbPerson.FirstName &&
                        p.Отчество.ToUpper() == dbPerson.MiddleName &&
                        p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate)
                        );

                    if (men.Count() == 1)
                    {
                        var tmp = men.SingleOrDefault();
                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = tmp.Фамилия,
                                FirstName = tmp.Имя,
                                MiddleName = tmp.Отчество,
                                BirthDate = Convert.ToDateTime(tmp.BirthDate).ToString("yyyy-MM-dd"),
                                DocSeria = Encrypt(tmp.СерНомДок.Trim().Substring(0, 4)),
                                DocNumber = Encrypt(tmp.СерНомДок.Trim().Substring(4, 6)),
                                Status = PersonLifeStatus.Dead
                            });
                    }
                    else if (men.Count() > 1)
                    {
                        string docSeriaNumber = null;
                        docMen = men.Where(p =>
                            p.Фамилия.ToUpper() == dbPerson.LastName &&
                            p.Имя.ToUpper() == dbPerson.FirstName &&
                            p.Отчество.ToUpper() == dbPerson.MiddleName &&
                            p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate) &&
                            p.СерНомДок == Decrypt(dbPerson.DocSeria) + Decrypt(dbPerson.DocNumber)
                            );
                        if (docMen != null)
                        {
                            var tmp = docMen.SingleOrDefault();
                            result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = tmp.Фамилия,
                                FirstName = tmp.Имя,
                                MiddleName = tmp.Отчество,
                                BirthDate = Convert.ToDateTime(tmp.BirthDate).ToString("yyyy-MM-dd"),
                                DocSeria = Encrypt(tmp.СерНомДок.Trim().Substring(0, 4)),
                                DocNumber = Encrypt(tmp.СерНомДок.Trim().Substring(4, 6)),
                                Status = PersonLifeStatus.Dead
                            });
                        }
                        else
                        {
                            foreach (var i in men)
                            {
                                result.ResponseData.Add(
                                   new PersonLifeStatusResponse
                                   {
                                       LastName = i.Фамилия,
                                       FirstName = i.Имя,
                                       MiddleName = i.Отчество,
                                       BirthDate = i.BirthDate.ToString(),
                                       Status = PersonLifeStatus.NotSure
                                   });
                            }
                        }
                    }
                    else if (men.Count() == 0)
                    {
                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = dbPerson.LastName,
                                FirstName = dbPerson.FirstName,
                                MiddleName = dbPerson.MiddleName,
                                BirthDate = dbPerson.BirthDate.ToString(),
                                DocSeria = dbPerson.DocSeria,
                                DocNumber = dbPerson.DocNumber,
                                Status = PersonLifeStatus.Alive
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            LogInDB(req.ToString(), result.ToString());
            return Task.FromResult(result);
        }
        #endregion

        #region Формировнаие ответа для Соц. портала
        public override Task<GetPersonPaymentsResponse> GetPersonPayments(GetPersonPaymentsRequest req, ServerCallContext context)
        {
            string log = req.ToString();
            try
            {
                var payments = new List<PersonPayment>();

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.CommandText = "public.get_msp";
                        cmd.Parameters.Add(new NpgsqlParameter("_c_snils", req.Snils));
                        cmd.Parameters.Add(new NpgsqlParameter("_d_datebegin", NpgsqlDbType.Date) { Value = Convert.ToDateTime(req.PeriodBegin) });
                        cmd.Parameters.Add(new NpgsqlParameter("_d_dateend", NpgsqlDbType.Date) { Value = Convert.ToDateTime(req.PeriodEnd) });

                        using (var dbPayments = cmd.ExecuteReader())
                        {
                            while (dbPayments.Read())
                            {
                                String CalcDate = null;
                                DateTime calculationDate = new DateTime();

                                try
                                {
                                    var date1 = dbPayments.GetDate(dbPayments.GetOrdinal("d_date_calculation"));
                                    var date2 = new DateTime(date1.Year, date1.Month, date1.Day);
                                    calculationDate = date2;
                                    CalcDate = calculationDate.ToString("yyyy-MM-dd");
                                }
                                catch
                                {
                                    CalcDate = "";
                                }

                                payments.Add(
                                    new PersonPayment()
                                    {
                                        DateCalculation = CalcDate,
                                        DateBegin = ReadDate(dbPayments, "d_begin").ToString("yyyy-MM-dd"),
                                        DateEnd = ReadDate(dbPayments, "d_end").ToString("yyyy-MM-dd"),
                                        Title = dbPayments[3].ToString(),
                                        Name = dbPayments[4].ToString(),
                                        PaymentSum = Convert.ToDouble(dbPayments[5].ToString())
                                    });
                            }
                        }
                    }
                    conn.Close();
                }

                var result = new GetPersonPaymentsResponse()
                {
                    Payments = { payments }
                };
                LogInDB(req.ToString(), result.Payments.ToString());
                return Task.FromResult(result);
            }
            catch (Exception exception)
            {
                var result = new GetPersonPaymentsResponse()
                {
                    Errors = { new Error() { Code = "unknown_error", Description = "Непредвиденная ошибка" } }
                };
                logger.Error(exception);
                return Task.FromResult(result);
            }
        }

        private DateTime ReadDate(NpgsqlDataReader reader, String fieldName)
        {
            NpgsqlDate? d = reader.GetDate(reader.GetOrdinal(fieldName));
            var d2 = new DateTime(d.Value.Year, d.Value.Month, d.Value.Day);
            return d2;
        }
        #endregion

        public object JsonGenerate(string where, string fullName, string serial, string number)
        {
            object result = new
            {
                Person = fullName,
                Passport = new
                {
                    Serial = serial,
                    Number = number
                }
            };

            #region Формирование Json в ручную
            //if (where != null)
            //{
            //    where = where.Substring(0, where.Length - 3) +
            //        ",\n'Person': '" + fullName + "', " +
            //        "'Passport': " +
            //            "{ 'Serial': '" + serial + "'," +
            //            "'Number': '" + number + "' }" +
            //        "}";
            //    result = JsonConvert.DeserializeObject(where);
            //}
            //else
            //{
            //    where = "{ " + "'Person': '" + fullName + "', " +
            //            "'Passport': " +
            //                "{ 'Serial': '" + serial + "'," +
            //                " 'Number': '" + number + "' }," +
            //            "}";
            //    result = JsonConvert.DeserializeObject(where);
            //}
            #endregion

            return result;
        }

        // Сохранение запроса и ответа в базу данных
        public void LogInDB(string req, string response)
        {
            api_req_requests requests = new api_req_requests
            {
                req_date = DateTime.Now,
                request = req,
                response = response,
                from_whom = this.GetType().ToString()
            };
            dbContext.Insert(requests);
            dbContext.Update<api_req_requests>(requests);
        }

        // Decrypt
        public static string Decrypt(string cipherText)
        {
            string EncryptionKey = "abc123";
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }

        // Encrypt
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
    }
}
