using System.Collections.Generic;

namespace SelfCdn.Registry.Models
{
    public class RegistryItem
    {
        public string MediaType { get; set; }
        public int TmdbId { get; set; }
        public List<RegistryMediaItem> Items { get; set; } = new List<RegistryMediaItem>();
    }
}
