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
        //private STH dbContext = null;

        public ApiServiceImpl(String providerName, String connectionString)
        {
            _connectionString = connectionString;
            //dbContext = new STH(providerName, connectionString);
        }

        #region Формировнаие ответа для УФСИН
        //// Простой RPC, который получает запрос от клиента и возвращает ответ 
        //public override Task<GetPersonLifeStatusListResponse> GetPersonLifeStatus(GetPersonLifeStatusListRequest req, ServerCallContext context)
        //{
        //    GetPersonLifeStatusListResponse result = new GetPersonLifeStatusListResponse();
        //    String personDocData = null;
        //    GetPersonLifeStatusRequest[] persons;
        //    IQueryable<fatalzp_sv_cd_umer> men = null;

        //    try
        //    {
        //        foreach (var dbPerson in req.Obj)
        //        {
        //            men = dbContext.fatalzp_sv_cd_umer.Where(p =>
        //                //p.Id.ToString() == dbPerson.Guid &&
        //                p.Фамилия == dbPerson.LastName &&
        //                p.Имя == dbPerson.FirstName &&
        //                p.Отчество == dbPerson.MiddleName &&
        //                p.BirthDate == Convert.ToDateTime(dbPerson.BirthDate));

        //            if (men.Count() == 1)
        //            {
        //                var tmp = men.SingleOrDefault();
        //                result.DeadPerson.Add(
        //                    new GetPersonLifeStatusListResponse.Types.GetPersonLifeStatusResponse
        //                    {
        //                        LastName = tmp.Фамилия,
        //                        FirstName = tmp.Имя,
        //                        MiddleName = tmp.Отчество,
        //                        BirthDate = tmp.BirthDate.ToString(),
        //                        Status = GetPersonLifeStatusListResponse.Types.Statuses.Dead.ToString()
        //                    });
        //            }
        //            else if (men.Count() > 1)
        //            {
        //                foreach (var i in men)
        //                {
        //                    result.DeadPerson.Add(
        //                       new GetPersonLifeStatusListResponse.Types.GetPersonLifeStatusResponse
        //                       {
        //                           LastName = i.Фамилия,
        //                           FirstName = i.Имя,
        //                           MiddleName = i.Отчество,
        //                           BirthDate = i.BirthDate.ToString(),
        //                           Status = GetPersonLifeStatusListResponse.Types.Statuses.NotSure.ToString()
        //                       });
        //                }
        //            }
        //        }
        //    }
        //    catch
        //    {
        //    }
        //    return Task.FromResult(result);
        //}
        #endregion

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
                return Task.FromResult(result);
            }
            catch (Exception exception)
            {
                var result = new GetPersonPaymentsResponse()
                {
                    Errors = { new Error() { Code = "unknown_error", Description = "Непредвиденная ошибка" } }
                };

                return Task.FromResult(result);
            }
        }

        private DateTime ReadDate(NpgsqlDataReader reader, String fieldName)
        {
            NpgsqlDate? d = reader.GetDate(reader.GetOrdinal(fieldName));
            var d2 = new DateTime(d.Value.Year, d.Value.Month, d.Value.Day);
            return d2;
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
        //    };

        //    #region Формирование Json в ручную
        //    //if (where != null)
        //    //{
        //    //    where = where.Substring(0, where.Length - 3) +
        //    //        ",\n'Person': '" + fullName + "', " +
        //    //        "'Passport': " +
        //    //            "{ 'Serial': '" + serial + "'," +
        //    //            "'Number': '" + number + "' }" +
        //    //        "}";
        //    //    result = JsonConvert.DeserializeObject(where);
        //    //}
        //    //else
        //    //{
        //    //    where = "{ " + "'Person': '" + fullName + "', " +
        //    //            "'Passport': " +
        //    //                "{ 'Serial': '" + serial + "'," +
        //    //                " 'Number': '" + number + "' }," +
        //    //            "}";
        //    //    result = JsonConvert.DeserializeObject(where);
        //    //}
        //    #endregion

        //    return result;
        //}

        //public override Task<SocialCapResponse> SocialCapMessage(SocialCapRequest req, ServerCallContext context)
        //{
        //    SocialCapResponse result = null;

        //    result = SocCapCheck(req.LastName, req.FirstName, req.MiddleName);

        //    return Task.FromResult(result);
        //}

        //public SocialCapResponse SocCapCheck(string lastName, string firstName, string middleName)
        //{
        //    SocialCapResponse result = null;
        //    var men = dbContext.fatalzp_sv_cd_umer.Where(p => p.Фамилия == lastName && p.Имя == firstName && p.Отчество == middleName).FirstOrDefault();
        //    if (men != null)
        //    {
        //        result = new SocialCapResponse()
        //        {
        //            Status = SocialCapResponse.Types.Statuses.Available.ToString()
        //        };
        //    }
        //    else
        //    {
        //        result = new SocialCapResponse()
        //        {
        //            Status = SocialCapResponse.Types.Statuses.Absent.ToString()
        //        };
        //    }

        //    return result;
        //}
    }
}
