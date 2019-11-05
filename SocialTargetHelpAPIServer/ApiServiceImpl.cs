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
using NpgsqlTypes;

namespace SocialTargetHelpAPIServer
{
    class ApiServiceImpl : ApiService.ApiServiceBase
    {
        private String _connectionString = null;
        private STH dbContext = null;

        public ApiServiceImpl(String providerName, String connectionString)
        {
            _connectionString = connectionString;
            dbContext = new STH(providerName, connectionString);
        }

        #region Формировнаие ответа для УФСИН
        //// Простой RPC, который получает запрос от клиента и возвращает ответ 
        public override Task<GetPersonsLifeStatusResponse> GetPersonsLifeStatus(GetPersonsLifeStatusRequest req, ServerCallContext context)
        {
            GetPersonsLifeStatusResponse result = new GetPersonsLifeStatusResponse();
            String personDocData = null;
            PersonLifeStatusRequest[] persons;
            IQueryable<fatalzp_sv_cd_umer> men = null;

            try
            {
                foreach (var dbPerson in req.RequestData)
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
                        result.ResponseData.Add(
                            new PersonLifeStatusResponse
                            {
                                LastName = tmp.Фамилия,
                                FirstName = tmp.Имя,
                                MiddleName = tmp.Отчество,
                                BirthDate = tmp.BirthDate.ToString(),
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
                }
            }
            catch
            {
            }
            return Task.FromResult(result);
        }
        #endregion

        public override Task<GetPersonPaymentsResponse> GetPersonPayments(GetPersonPaymentsRequest req, ServerCallContext context)
        {
            var result = new GetPersonPaymentsResponse();
            //GetPersonPaymentsResponse[] Payments;
            IQueryable<fatalzp_sv_cd_umer> men = null;

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;

                    cmd.CommandText = "public.get_msp";
                    cmd.Parameters.Add(new NpgsqlParameter("_d_datebegin", Convert.ToDateTime(req.PeriodBegin)));
                    cmd.Parameters.Add(new NpgsqlParameter("_d_dateend", Convert.ToDateTime(req.PeriodEnd)));
                    cmd.Parameters.Add(new NpgsqlParameter("_c_snils", req.Snils));
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
                                new PersonPayment()
                                {
                                    DateCalculation = CalcDate,
                                    DateBegin = ReadDate(personPayments, "d_begin").ToString("yyyy-MM-dd"),
                                    DateEnd = ReadDate(personPayments, "d_end").ToString("yyyy-MM-dd"),
                                    Title = personPayments[3].ToString(),
                                    Name = personPayments[4].ToString(),
                                    PaymentSum = Convert.ToDouble(personPayments[5].ToString())
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
    }
}
