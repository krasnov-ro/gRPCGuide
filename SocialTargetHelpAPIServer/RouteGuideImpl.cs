using System;
using Grpc.Core;
using System.Collections.Generic;
using System.Text;
using SocialTargetHelpAPIContract;
using System.Threading.Tasks;
using static SocialTargetHelpAPIContract.Person.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using SocialTargetHelpAPIServer;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using SocialTargetHelpAPIServer.Models;
using Npgsql;
using System.Globalization;
using NpgsqlTypes;

namespace SocialTargetHelpAPIServer
{
    class RouteGuideImpl : RouteGuide.RouteGuideBase
    {
        String _connectionString = null;
        STH dbContext;
        public RouteGuideImpl()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true);

            var configuration = builder.Build();

            dbContext = new STH("PostgreSQL.9.5", configuration.GetConnectionString("MyDb"));
            _connectionString = configuration.GetConnectionString("MyDb");
        }

        #region Формировнаие ответа для УФСИН
        // Простой RPC, который получает запрос от клиента и возвращает ответ 
        public override Task<GetPersonLifeStatusListResponse> GetPersonLifeStatus(GetPersonLifeStatusListRequest req, ServerCallContext context)
        {
            GetPersonLifeStatusListResponse result = new GetPersonLifeStatusListResponse();
            String personDocData = null;
            GetPersonLifeStatusRequest[] persons;
            IQueryable<fatalzp_sv_cd_umer> men = null;

            try
            {
                foreach (var dbPerson in req.Obj)
                {
                    men = dbContext.fatalzp_sv_cd_umer.Where(p =>
                        //p.Id.ToString() == dbPerson.Guid &&
                        p.Фамилия == dbPerson.LastName &&
                        p.Имя == dbPerson.FirstName &&
                        p.Отчество == dbPerson.MiddleName &&
                        p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate));

                    if (men.Count() == 1)
                    {
                        var tmp = men.SingleOrDefault();
                        result.DeadPerson.Add(
                            new GetPersonLifeStatusListResponse.Types.GetPersonLifeStatusResponse
                            {
                                LastName = tmp.Фамилия,
                                FirstName = tmp.Имя,
                                MiddleName = tmp.Отчество,
                                BirthDate = tmp.BirthDate.ToString(),
                                Status = GetPersonLifeStatusListResponse.Types.Statuses.Dead.ToString()
                            });
                    }
                    else if (men.Count() > 1)
                    {
                        foreach (var i in men)
                        {
                            result.DeadPerson.Add(
                               new GetPersonLifeStatusListResponse.Types.GetPersonLifeStatusResponse
                               {
                                   LastName = i.Фамилия,
                                   FirstName = i.Имя,
                                   MiddleName = i.Отчество,
                                   BirthDate = i.BirthDate.ToString(),
                                   Status = GetPersonLifeStatusListResponse.Types.Statuses.NotSure.ToString()
                               });
                        }
                    }
                }
            }
            catch
            {
            }
            return Task.FromResult(result);
        }
        #endregion

        public override Task<GetPersonPaymentsListResponse> GetPersonPaymentsList(GetPersonPaymentsListRequest req, ServerCallContext context)
        {
            GetPersonPaymentsListResponse result = new GetPersonPaymentsListResponse();
            //GetPersonPaymentsResponse[] Payments;
            IQueryable<fatalzp_sv_cd_umer> men = null;

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;

                    cmd.CommandText = "public.get_msp";
                    cmd.Parameters.Add(new NpgsqlParameter("_d_datebegin", Convert.ToDateTime(req.PeriodBegin))/*, new NpgsqlParameter("_d_dateend", req.PeriodEnd), new NpgsqlParameter("_c_snils", req.Snils)*/);
                    cmd.Parameters.Add(new NpgsqlParameter("_d_dateend", Convert.ToDateTime(req.PeriodEnd)));
                    cmd.Parameters.Add(new NpgsqlParameter("_c_snils", req.Snils));
                    //cmd.CommandText = "SELECT * FROM public.get_msp('"+ req.PeriodBegin + 
                    //                                            "','" + req.PeriodEnd + 
                    //                                            "','" + req.Snils + "')";
                    cmd.ExecuteNonQuery();
                    using (var personPayments = cmd.ExecuteReader())
                    {
                        while (personPayments.Read())
                        {
                            String CalcDate = null;
                            DateTime calculationDate = new DateTime();

                            try
                            {
                                var date1 = personPayments.GetDate(personPayments.GetOrdinal("d_date_calculation"));
                                var date2 = new DateTime(date1.Year, date1.Month, date1.Day);
                                calculationDate = date2;
                                CalcDate = calculationDate.ToString("yyyy-MM-dd");
                            }
                            catch
                            {
                                CalcDate = "";
                            }

                            result.Payments.Add(
                                new GetPersonPaymentsListResponse.Types.GetPersonPaymentsResponse
                                { 
                                    DateCalculation = CalcDate,
                                    DateBegin = ReadDate(personPayments, "d_begin").ToString("yyyy-MM-dd"),
                                    DateEnd = ReadDate(personPayments, "d_end").ToString("yyyy-MM-dd"),
                                    Title = personPayments[3].ToString(),
                                    Name = personPayments[4].ToString(),
                                    PaymentSum = personPayments[5].ToString()
                                });
                        }
                    }
                }
                conn.Close();
            }

            return Task.FromResult(result);
        }

        private DateTime ReadDate(NpgsqlDataReader reader, String fieldName)
        {
            NpgsqlDate? d = reader.GetDate(reader.GetOrdinal(fieldName));
            var d2 = new DateTime(d.Value.Year, d.Value.Month, d.Value.Day);
            return d2;
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

        public override Task<SocialCapResponse> SocialCapMessage(SocialCapRequest req, ServerCallContext context)
        {
            SocialCapResponse result = null;

            result = SocCapCheck(req.LastName, req.FirstName, req.MiddleName);

            return Task.FromResult(result);
        }

        public SocialCapResponse SocCapCheck(string lastName, string firstName, string middleName)
        {
            SocialCapResponse result = null;
            var men = dbContext.fatalzp_sv_cd_umer.Where(p => p.Фамилия == lastName && p.Имя == firstName && p.Отчество == middleName).FirstOrDefault();
            if (men != null)
            {
                result = new SocialCapResponse()
                {
                    Status = SocialCapResponse.Types.Statuses.Available.ToString()
                };
            }
            else
            {
                result = new SocialCapResponse()
                {
                    Status = SocialCapResponse.Types.Statuses.Absent.ToString()
                };
            }

            return result;
        }
    }
}
