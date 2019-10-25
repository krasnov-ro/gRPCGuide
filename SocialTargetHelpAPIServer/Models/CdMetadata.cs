using System;
using System.Collections.Generic;

namespace SocialTargetHelpAPIServer.Models
{
    public partial class CdMetadata
    {
        public Guid Id { get; set; }
        public string CSenderCode { get; set; }
        public string CMethod { get; set; }
    }
}
