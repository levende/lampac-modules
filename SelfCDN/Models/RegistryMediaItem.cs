using System;
using System.Collections.Generic;
using System.Text;

namespace SelfCdn.Registry.Models
{
    public class RegistryMediaItem
    {
        public string FilePath { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
    }
}