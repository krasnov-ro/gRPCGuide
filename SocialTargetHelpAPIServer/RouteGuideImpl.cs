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
            GetPersonLifeStatusResponse result = null;
            var men = _context.CdData.Where(p => p.CLastName == lastName && p.CFirstName == firstName && p.CMiddleName == middleName && p.DBirthDate == birthDate).FirstOrDefault();
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
