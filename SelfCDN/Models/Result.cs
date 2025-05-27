using System.Collections.Generic;

namespace SelfCDN.Models
{
    public class Result
    {
        public List<Movie> movie { get; set; }
        public Dictionary<string, List<Voice>> serial { get; set; }
    }
}
