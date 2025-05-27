using System.Collections.Generic;

namespace SelfCDN.Models
{
    public class Serial
    {
        public string id { get; set; }
        public string season { get; set; }

        public List<(string link, string quality)> links { get; set; }
    }
}
