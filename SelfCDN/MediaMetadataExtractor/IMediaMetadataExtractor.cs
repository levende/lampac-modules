using SelfCdn.Registry.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SelfCDN.MediaMetadataExtractor
{
    internal interface IMediaMetadataExtractor
    {
        Task<IReadOnlyCollection<MediaMetadata>> ExtractAsync(IEnumerable<string> filePaths);
    }
}
