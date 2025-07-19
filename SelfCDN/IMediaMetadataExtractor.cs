using SelfCdn.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SelfCDN
{
    internal interface IMediaMetadataExtractor
    {
        Task<IReadOnlyCollection<MediaMetadata>> ExtractAsync(IEnumerable<string> filePaths);
    }
}