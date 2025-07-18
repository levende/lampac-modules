using SelfCdn.Registry.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SelfCDN.Tmdb
{
    public sealed record TmdbFindResult(long Id, int? Year);

    internal interface ITmdbFinder
    {
        Task<IReadOnlyList<TmdbFindResult>> SearchAsync(MediaMetadata mediaMetadata);
    }
}
