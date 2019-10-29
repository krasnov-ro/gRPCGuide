using SocialTargetHelpAPIServer.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace SocialTargetHelpAPIServer
{
    class MyDbContext : STH
    {
        private readonly string connectionString;
        public MyDbContext(string connectionString) : base()
        {
            this.connectionString = connectionString;
        }
    }
}
