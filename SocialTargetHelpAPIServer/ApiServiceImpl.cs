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
using System.Text.RegularExpressions;

namespace SocialTargetHelpAPIServer
{
    // Серверная часть это "ЕГИС АСП Единая государственная информационная система «Адресная социальная помощь»"
    // Она имеет информацию о всех родившихся и умерших, информацию она получает от Загса, и содержит все выплаты человека в рамках социальной помощи
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

            foreach (var d in req.RequestData)
            { }

            GetPersonsLifeStatusResponse result = new GetPersonsLifeStatusResponse();
            String personDocData = null;
            PersonLifeStatusRequest[] persons;
            IQueryable<fatalzp_fatalzp_sv_cd_umer> men = null;
            IQueryable<fatalzp_fatalzp_sv_cd_umer> docMen = null;
            IQueryable<fatalzp_fatalzp_sv_cd_umer> menWithDoc = null;

            try
            {
                foreach (var dbPerson in req.RequestData)
                {

                    var decriptedDoc = Decrypt(dbPerson.DocSeria) + Decrypt(dbPerson.DocNumber);

                    // Найдем людей по ФИО и дате рождения
                    men = dbContext.fatalzp_sv_cd_umer.Where(p =>
                        //p.Id.ToString() == dbPerson.Guid &&
                        p.Фамилия.ToUpper().Trim() == dbPerson.LastName.ToUpper().Trim() &&
                        p.Имя.ToUpper().Trim() == dbPerson.FirstName.ToUpper().Trim() &&
                        p.Отчество.ToUpper().Trim() == dbPerson.MiddleName.ToUpper().Trim() &&
                        p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate)
                        );

                    // Найдем всех людей подходяжих по ФИО и дате рождения и по документу
                    menWithDoc = men.Where(p =>
                        //p.Id.ToString() == dbPerson.Guid &&
                        p.Фамилия.ToUpper().Trim() == dbPerson.LastName.ToUpper().Trim() &&
                        p.Имя.ToUpper().Trim() == dbPerson.FirstName.ToUpper().Trim() &&
                        p.Отчество.ToUpper().Trim() == dbPerson.MiddleName.ToUpper().Trim() &&
                        p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate) &&
                        p.СерНомДок.Replace(" ", "").Replace("-", "") == Regex.Replace(decriptedDoc, @"[^\d]", "")
                        /*&&
                        Regex.Replace(p.СерНомДок, @"[^\d]", "") == decriptedDoc*/
                        );

                    // Если такой человек один, то мы отправим информацию по этому человеку, персональные данные зашифруем
                    if (menWithDoc.Count() == 1)
                    {
                        var filteredMen = menWithDoc.SingleOrDefault();
                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = filteredMen.Фамилия,
                                FirstName = filteredMen.Имя,
                                MiddleName = filteredMen.Отчество,
                                BirthDate = filteredMen.BirthDate.Value.ToString("yyyy-MM-dd"),
                                DocSeria = Encrypt(filteredMen.СерНомДок.Trim().Substring(0, 4)),
                                DocNumber = Encrypt(filteredMen.СерНомДок.Trim().Substring(4, 6)),
                                Status = PersonLifeStatus.Dead
                            });
                    }

                    #region То что было до 22/11/2019
                    //// Если таких несколько то мы попытаемся поискать по паспортным данным
                    //else if (men.Count() > 1)
                    //{
                    //    string docSeriaNumber = null;
                    //    docMen = men.Where(p =>
                    //        p.Фамилия.ToUpper() == dbPerson.LastName &&
                    //        p.Имя.ToUpper() == dbPerson.FirstName &&
                    //        p.Отчество.ToUpper() == dbPerson.MiddleName &&
                    //        p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate) &&
                    //        p.СерНомДок == Decrypt(dbPerson.DocSeria) + Decrypt(dbPerson.DocNumber)
                    //        );
                    //    // Если такой человек нашелся то отправим по нему инфу, персональные данные зашифруем
                    //    if (docMen != null)
                    //    {
                    //        var tmp = docMen.SingleOrDefault();
                    //        result.ResponseData.Add(
                    //        new PersonLifeStatusResponse
                    //        {
                    //            LastName = tmp.Фамилия,
                    //            FirstName = tmp.Имя,
                    //            MiddleName = tmp.Отчество,
                    //            BirthDate = Convert.ToDateTime(tmp.BirthDate).ToString("yyyy-MM-dd"),
                    //            DocSeria = Encrypt(tmp.СерНомДок.Trim().Substring(0, 4)),
                    //            DocNumber = Encrypt(tmp.СерНомДок.Trim().Substring(4, 6)),
                    //            Status = PersonLifeStatus.Dead
                    //        });
                    //    }
                    //    // Если такой человек не нашелся в списке умерших, то все хорошо, он жив, наверное, может быть, маловероятно, но это не точно
                    //    else
                    //    {
                    //        foreach (var i in men)
                    //        {
                    //            result.ResponseData.Add(
                    //               new PersonLifeStatusResponse
                    //               {
                    //                   LastName = i.Фамилия,
                    //                   FirstName = i.Имя,
                    //                   MiddleName = i.Отчество,
                    //                   BirthDate = i.BirthDate.ToString(),
                    //                   Status = PersonLifeStatus.NotSure
                    //               });
                    //        }
                    //    }
                    //}
                    #endregion

                    // Если человека с такими ФИО и датой рождения, документом, нет, то попытаемся найти по ФИО и дате рождения
                    else if (menWithDoc.Count() == 0)
                    {
                        // Если нашлось больше нуля людей с таким ФИО и ДР, то передаем информацию по ним "Мы, не уверены, что это те люди о ком вы хотели узнать"  
                        if (men.Count() != 0)
                        {
                            int check = 0;
                            string guid = dbPerson.Guid;
                            foreach (var oneMen in men)
                            {
                                if (check != 0)
                                    guid = Guid.NewGuid().ToString();
                                result.ResponseData.Add(
                                    new PersonLifeStatusResponse
                                    {
                                        Guid = guid,
                                        LastName = oneMen.Фамилия,
                                        FirstName = oneMen.Имя,
                                        MiddleName = oneMen.Отчество,
                                        BirthDate = oneMen.BirthDate.ToString(),
                                        Status = PersonLifeStatus.NotSure
                                    });
                                check++;
                            }
                        }

                        // Ну а если же, людей с таким ФИО и ДР не нашлось, то мы передаем информацию о них со статусом "Жив"
                        else
                        {
                            result.ResponseData.Add(
                                new PersonLifeStatusResponse
                                {
                                    Guid = dbPerson.Guid,
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
        // Ищем выплаты человека с полученным снилс за полученный период 
        public override Task<GetPersonPaymentsResponse> GetPersonPayments(GetPersonPaymentsRequest req, ServerCallContext context)
        {
            try
            {
                var payments = new List<PersonPayment>();

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Вызываем хранимку которая возвращает выплаты по данному человеку за указанный период
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

        public override Task<GetVeteranDictionariesResponse> GetVeteranDictionaries(GetVeteranDictionariesRequest request, ServerCallContext context)
        {
            var organizations = dbContext.common_cs_orgs
                .ToArray()
                .Select(p => new Organization()
                {
                    Id = p.id.ToString(),
                    Name = p.c_name,
                    Address = p.c_address,
                    Latitude = Convert.ToDouble(p.n_latitude),
                    Longitude = Convert.ToDouble(p.n_longitude),
                    BossName = p.c_boss,
                    WorkingSchedule = p.c_graphic,
                    Phones = { p.c_phone },
                    Emails = { p.c_email },
                    Fax = p.c_fax
                });

            var awardData = dbContext.veterans_cs_awards
                .Select(p => new
                {
                    groupId = p.csawardsfkeyftype.id,
                    groupName = p.csawardsfkeyftype.c_name,
                    award = new VeteranAward()
                    {
                        Id = p.id.ToString(),
                        Name = p.c_name,
                        NameShort = p.c_name_short,
                        Code = p.c_code,
                        Archived = p.b_archive,
                        IsRegional = p.b_chr
                    }
                })
                .ToArray()
                .GroupBy(p => new { p.groupId, p.groupName })
                .Select(p => new VeteranAwardGroup()
                {
                    Id = p.Key.groupId.ToString(),
                    Name = p.Key.groupName,
                    Awards = { p.Select(pp => pp.award) }
                });

            var docTypes = dbContext.public_cs_document_types
                .Select(p => new VeteranDocumentType()
                {
                    Id = p.id,
                    Name = p.c_name
                })
                .ToArray();

            var citizenCategories = dbContext.veterans_cs_citizen_categories
                .Select(p => new CitizenCategory()
                {
                    Id = p.id.ToString(),
                    Name = p.c_name,
                    Code = p.c_alias,
                    DescriptionValue = p.c_desc ?? "",
                    DescriptionHasValue = p.c_desc != null
                })
                .ToArray();

            var serviceGroups = dbContext.veterans_cs_service_group
                .Select(p => new SocialServiceGroup()
                {
                    Id = p.id.ToString(),
                    Name = p.c_name,
                    Description = p.c_desc
                })
                .ToArray();

            //var services = dbContext.veterans_cs_services
            //   .Select(p => new SocialService()
            //   {
            //       Id = p.id.ToString(),
            //       Name = p.c_name,
            //       Description = p.c_desc,
            //       PaymentPeriod = null,
            //       Conditions = p.c_provisions,
            //       PaymentForm = null,
            //       PaymentSource = null,
            //       SocialServiceGroupIds = null,
            //       SocialServiceOrgIds = null
            //   })
            //   .ToArray();

            var result = new GetVeteranDictionariesResponse()
            {
                Organizations = { organizations },
                AwardGroupsWithAwards = { awardData },
                DocumentTypes = { docTypes },
                CitizenCategories = { citizenCategories },
                ServiceGroups = { serviceGroups },
                Services = { },
                ServiceCitizenCategories = { }
            };

            return Task.FromResult(result);
        }

        //public object JsonGenerate(string where, string fullName, string serial, string number)
        //{
        //    object result = new
        //    {
        //        Person = fullName,
        //        Passport = new
        //        {
        //            Serial = serial,
        //            Number = number
        //        }
        //    }
        //};

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

        // Сохранение запроса и ответа в базу данных
        public void LogInDB(string req, string response)
        {
            var apiRequest = new api_req_api_req_requests
            {
                req_date = DateTime.Now,
                request = req,
                response = response,
                from_whom = this.GetType().ToString()
            };
            dbContext.Insert(apiRequest);
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
