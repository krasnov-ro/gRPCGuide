using System;
using System.Collections.Generic;
using System.Text;

namespace SocialTargetHelpAPIClient.Models
{
    public partial class STH
    {
        public STH(String providerName, string connectionString)
            : base(providerName, connectionString) { }
    }
}
