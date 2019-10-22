using System;
using Grpc.Core;
using System.Collections.Generic;
using System.Text;
using gRPCGuideContract.Contract;
using System.Threading.Tasks;
using static gRPCGuideContract.Contract.Person.Types;
using gRPCGuideServer.Models;
using Microsoft.EntityFrameworkCore;

namespace gRPCGuideServer
{
    class RouteGuideImpl : RouteGuide.RouteGuideBase
    {
        //private social_target_helpContext _context;

        //public RouteGuideImpl(social_target_helpContext context)
        //{
        //    _context = context;
        //}

        // Простой RPC, который получает запрос от клиента и возвращает ответ 
        public override Task<SendMessageResponse> GetMessage(SendMessageRequest request, Grpc.Core.ServerCallContext context)
        {
            SendMessageResponse result = null;
            DeathCheck(request.LastName, request.FirstName, request.MiddleName, Convert.ToDateTime(request.BirthDate));

            return Task.FromResult(result);
        }

        public Task<CdData> DeathCheck(string lastName, string firstName, string middleName, DateTime birthDate)
        {
            Task<CdData> dPerson;
            using (social_target_helpContext _context = new social_target_helpContext())
            {
                dPerson = _context.CdData.SingleOrDefaultAsync(p => p.CLastName == lastName && p.CFirstName == firstName && p.CMiddleName == middleName && p.DBirthDate == birthDate);
            }
            return dPerson;
        }
    }
}
