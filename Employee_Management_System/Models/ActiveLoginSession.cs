using System;

namespace Employee_Management_System.Models
{
    public class ActiveLoginSession
    {
        public string LoginId { get; set; } = "";
        public string SessionToken { get; set; } = "";
        public string BrowserName { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public DateTime LoginTimeUtc { get; set; }
    }
}
