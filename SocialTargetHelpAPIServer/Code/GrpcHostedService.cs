using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocialTargetHelpAPIServer
{
    internal class GrpcHostedService
    {
        private Server _server = null;

        public GrpcHostedService(Server server)
        {
            _server = server;
        }

        public void Start()
        {
            _server.Start();
        }

        public void Stop()
        {
            _server.ShutdownAsync().Wait();
        }
    }
}
