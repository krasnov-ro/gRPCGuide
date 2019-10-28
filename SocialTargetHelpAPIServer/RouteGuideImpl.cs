using System;
using Grpc.Core;
using System.Collections.Generic;
using System.Text;
using SocialTargetHelpAPIContract;
using System.Threading.Tasks;
using static SocialTargetHelpAPIContract.Person.Types;
using SocialTargetHelpAPIServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using SocialTargetHelpAPIServer;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SocialTargetHelpAPIServer
{
    class RouteGuideImpl : RouteGuide.RouteGuideBase
    {
        private social_target_helpContext _context;

        public RouteGuideImpl()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true);

            var configuration = builder.Build();

            _context = new MyDbContext(configuration.GetConnectionString("MyDb"));
        }

        // Простой RPC, который получает запрос от клиента и возвращает ответ 
        public override Task<GetPersonLifeStatusResponse> GetPersonLifeStatus(GetPersonLifeStatusRequest req, ServerCallContext context)
        {
            GetPersonLifeStatusResponse result = null;
            result = FsinDeathCheck(req.LastName, req.FirstName, req.MiddleName, Convert.ToDateTime(req.BirthDate));
            return Task.FromResult(result);
        }

        public GetPersonLifeStatusResponse FsinDeathCheck(string lastName, string firstName, string middleName, DateTime birthDate)
        {
            String personDocData = null;
            GetPersonLifeStatusResponse result = null;
            try
            {
                var men = _context.CdData.Where(p => p.CLastName == lastName && p.CFirstName == firstName && p.CMiddleName == middleName && p.DBirthDate == birthDate).SingleOrDefault();
                if (men != null)
                {
                    result = new GetPersonLifeStatusResponse()
                    {
                        LastName = men.CLastName,
                        FirstName = men.CFirstName,
                        MiddleName = men.CMiddleName,
                        BirthDate = men.DBirthDate.ToString(),
                        Status = GetPersonLifeStatusResponse.Types.Statuses.Dead.ToString()
                    };
                }
                else
                {
                    result = new GetPersonLifeStatusResponse()
                    {
                        LastName = lastName,
                        FirstName = firstName,
                        MiddleName = middleName,
                        BirthDate = birthDate.ToString("dd.MM.yyyy"),
                        Status = GetPersonLifeStatusResponse.Types.Statuses.Alive.ToString()
                    };
                }
            }
            catch
            {
                
                String JsonResult = null;
                var Persons = _context.CdData.Where(p => p.CLastName == lastName && p.CFirstName == firstName && p.CMiddleName == middleName && p.DBirthDate == birthDate);
                foreach (var person in Persons)
                {
                    var fullName = person.CLastName + " " + person.CFirstName + " " + person.CMiddleName;

                     JsonGenerate(JsonResult, fullName, person.CDocumentSerial, person.CDocumentNumber);
                }
            }
            return result;
        }

        public object JsonGenerate(string where, string fullName, string serial, string number)
        {
            object result = new {
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
            var men = _context.CdData.Where(p => p.CLastName == lastName && p.CFirstName == firstName && p.CMiddleName == middleName).FirstOrDefault();
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
