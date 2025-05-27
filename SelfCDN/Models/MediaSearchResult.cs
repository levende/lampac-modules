using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System;

namespace SelfCDN.Models
{
    public class MediaSearchResult
    {
        public List<Movie> Movies { get; set; }
        public Dictionary<string, List<Voice>> Series { get; set; }

        public bool HasContent => Movies?.Count > 0 || Series?.Count > 0;

        public ActionResult Match(
            Func<List<Movie>, ActionResult> movieHandler,
            Func<Dictionary<string, List<Voice>>, ActionResult> seriesHandler)
        {
            if (Movies != null) return movieHandler(Movies);
            if (Series != null) return seriesHandler(Series);
            return new OkResult();
        }
    }
}
