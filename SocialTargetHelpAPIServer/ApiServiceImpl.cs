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

            foreach(var d in req.RequestData)
            { }

            GetPersonsLifeStatusResponse result = new GetPersonsLifeStatusResponse();
            IQueryable<fatalzp_fatalzp_sv_cd_umer> men = null;

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
                                Status = PersonLifeStatus.Dead
                            });
                    }
                    else if (men.Count() > 1)
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
                    else if (men.Count() == 0)
                    {
                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = dbPerson.LastName,
                                FirstName = dbPerson.FirstName,
                                MiddleName = dbPerson.MiddleName,
                                BirthDate = dbPerson.BirthDate.ToString(),
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
            var apiRequest = new api_req_api_req_requests
            {
                req_date = DateTime.Now,
                request = req,
                response = response,
                from_whom = this.GetType().ToString()
            };
            dbContext.Insert(apiRequest);
        }
    }
}
