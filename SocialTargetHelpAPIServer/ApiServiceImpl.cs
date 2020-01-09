using Grpc.Core;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using SocialTargetHelpAPI.Contract;
using SocialTargetHelpAPIServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialTargetHelpAPIServer
{
    class ApiServiceImpl : ApiService.ApiServiceBase
    {
        private readonly String _connectionString = null;
        private readonly STH dbContext = null;
        private readonly ILogger _logger = null;

        public ApiServiceImpl(String providerName, String connectionString)
        {
            _connectionString = connectionString;
            dbContext = new STH(providerName, connectionString);
            _logger = Program.AppServiceProvider.GetService<ILogger<ApiServiceImpl>>();
        }

        #region Формировнаие ответа для УФСИН

        public override Task<GetPersonsLifeStatusResponse> GetPersonsLifeStatus(GetPersonsLifeStatusRequest req, ServerCallContext context)
        {
            var result = new GetPersonsLifeStatusResponse();

            try
            {
                foreach (var reqPerson in req.RequestData)
                {
                    var dbPersons = dbContext.fatalzp_sv_cd_umer
                        .Where(p => p.Фамилия.ToUpper() == reqPerson.LastName)
                        .Where(p => p.Имя.ToUpper() == reqPerson.FirstName)
                        .Where(p => p.Отчество.ToUpper() == reqPerson.MiddleName)
                        .Where(p => p.BirthDate == Convert.ToDateTime(reqPerson.BirthDate));

                    if (dbPersons.Count() == 1)
                    {
                        var dbPerson = dbPersons.SingleOrDefault();

                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = dbPerson.Фамилия,
                                FirstName = dbPerson.Имя,
                                MiddleName = dbPerson.Отчество,
                                BirthDate = Convert.ToDateTime(dbPerson.BirthDate).ToString("yyyy-MM-dd"),
                                Status = PersonLifeStatus.Dead
                            });
                    }
                    else if (dbPersons.Count() > 1)
                    {
                        foreach (var dbPerson in dbPersons)
                        {
                            result.ResponseData.Add(
                               new PersonLifeStatusResponse
                               {
                                   LastName = dbPerson.Фамилия,
                                   FirstName = dbPerson.Имя,
                                   MiddleName = dbPerson.Отчество,
                                   BirthDate = dbPerson.BirthDate.ToString(),
                                   Status = PersonLifeStatus.NotSure
                               });
                        }
                    }
                    else if (dbPersons.Count() == 0)
                    {
                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = reqPerson.LastName,
                                FirstName = reqPerson.FirstName,
                                MiddleName = reqPerson.MiddleName,
                                BirthDate = reqPerson.BirthDate.ToString(),
                                Status = PersonLifeStatus.Alive
                            });
                    }
                }

                LogInDB(req.ToString(), result.ToString());
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "GetPersonsLifeStatus error");
            }

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
                _logger.LogError(exception, "GetPersonPayments error");

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
            try
            {
                var organizations = dbContext.common_cs_orgs
                    .ToArray()
                    .Select(p => new Organization()
                    {
                        Id = p.id.ToString(),
                        Name = p.c_name,
                        Address = p.c_address ?? "",
                        Latitude = Convert.ToDouble(p.n_latitude),
                        Longitude = Convert.ToDouble(p.n_longitude),
                        BossName = p.c_boss ?? "",
                        WorkingSchedule = p.c_graphic ?? "",
                        Phones = { p.c_phone ?? "" },
                        Emails = { p.c_email ?? "" },
                        Fax = p.c_fax ?? ""
                    });

                var citizenCategories = dbContext.veterans_cs_citizen_categories
                    .ToArray()
                    .Select(p => new CitizenCategory()
                    {
                        Id = p.id.ToString(),
                        Name = p.c_name,
                        Code = p.c_alias ?? "",
                        DescriptionValue = p.c_desc ?? "",
                        DescriptionHasValue = p.c_desc != null
                    })
                    .ToArray();

                var serviceGroups = dbContext.veterans_cs_service_group
                    .ToArray()
                    .Select(p => new SocialServiceGroup()
                    {
                        Id = p.id.ToString(),
                        Name = p.c_name,
                        Description = p.c_desc ?? ""
                    })
                    .ToArray();

                var svcGroups = dbContext.veterans_cs_services_groups.ToArray();
                var svcOrgs = dbContext.veterans_cs_services_orgs.ToArray();
                var svcNormDocs = dbContext.veterans_sv_normative_docs.ToArray();
                var svcCitizenCategoryDocs = dbContext.veterans_sv_documents.ToArray();

                var services = dbContext.veterans_sv_msp_category
                    .ToArray()
                    .GroupBy(p => p.f_service)
                    .Select(p =>
                    {
                        var first = p.First();
                        return new SocialService()
                        {
                            Id = first.f_service.Value.ToString(),
                            Name = first.c_name,
                            Conditions = first.c_provisions ?? "",
                            SocialServiceGroupIds = { svcGroups.Where(p1 => p1.f_service == first.f_service.Value).Select(p1 => p1.f_group.ToString()) },
                            SocialServiceOrgIds = { svcOrgs.Where(p1 => p1.f_service == first.f_service.Value).Select(p1 => p1.f_org.ToString()) },
                            NormDocs = { svcNormDocs.Where(p1 => p1.f_service == first.f_service.Value).Select(p1 => p1.c_full_name) },
                            CitizenCategoriesData = {
                                p.Select(p1 => new SocialServiceCitizenCategory()
                                {
                                    CitizenCategoryId = p1.f_category.Value.ToString(),
                                    PaymentType = ParsePaymentType(p1.c_payment_type),
                                    Size = p1.c_size ?? ""
                                })
                            },
                            CitizenCategoryDocuments = {
                                svcCitizenCategoryDocs.Where(p1=>p1.f_service == first.f_service.Value).Select(p1=> new SocialServiceCitizenCategoryDocument()
                                {
                                    CitizenCategoryId = p1.f_category.Value.ToString(),
                                    DocumentName = p1.c_document
                                })
                            }
                        };
                    });

                var result = new GetVeteranDictionariesResponse()
                {
                    Organizations = { organizations },
                    CitizenCategories = { citizenCategories },
                    ServiceGroups = { serviceGroups },
                    Services = { services }
                };

                return Task.FromResult(result);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "GetVeteranDictionaries error");

                var errorResult = new GetVeteranDictionariesResponse()
                {
                    Errors = { new Error() { Code = "error" } }
                };

                return Task.FromResult(errorResult);
            }
        }

        // Сохранение запроса и ответа в базу данных
        private void LogInDB(string req, string response)
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

        private readonly IDictionary<String, SocialServiceCitizenCategory.Types.PaymentType> _paymentTypesMap = new Dictionary<String, SocialServiceCitizenCategory.Types.PaymentType>()
        {
            ["monthly"] = SocialServiceCitizenCategory.Types.PaymentType.Monthly,
            ["quarter"] = SocialServiceCitizenCategory.Types.PaymentType.Quarterly,
            ["year"] = SocialServiceCitizenCategory.Types.PaymentType.Yearly,
            ["one_time"] = SocialServiceCitizenCategory.Types.PaymentType.NonRecurrent
        };

        private SocialServiceCitizenCategory.Types.PaymentType ParsePaymentType(String code)
        {
            if (!String.IsNullOrEmpty(code) && _paymentTypesMap.ContainsKey(code))
                return _paymentTypesMap[code];
            else
                return SocialServiceCitizenCategory.Types.PaymentType.None;
        }
    }
}
