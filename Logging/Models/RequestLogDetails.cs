using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logging.Models
{
    public class RequestLogDetails
    {
        public string? RequestId { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
        public string? ClientIp { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime ResponseTime { get; set; }
        public string Request { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public int? HttpStatusCode { get; set; }
        public bool IsCacheEnabled { get; set; } = false;
        public string Headers { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
    }
}
