using System;
using System.Collections.Generic;

namespace gRPCGuideServer.Models
{
    public partial class CdData
    {
        public int Id { get; set; }
        public string CLastName { get; set; }
        public string CFirstName { get; set; }
        public string CMiddleName { get; set; }
        public DateTime? DBirthDate { get; set; }
    }
}
