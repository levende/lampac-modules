using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.Module;
using Shared.Models;
using System.Collections.Generic;

namespace SelfCDN
{
    public class OnlineApi
    {
        public static List<(string, string, string, int)> Invoke(HttpContext httpContext, IMemoryCache memoryCache, RequestModel requestInfo, string host, OnlineEventsModel args)
        {
            const string plugin = "SelfCDN";

            var url = ModInit.Settings.overridehost;

            if (string.IsNullOrEmpty(url))
            {
                url = $"{host}/{plugin}";
            }

            return new List<(string name, string url, string plugin, int index)>
            {
                (ModInit.Settings.displayname ?? ModInit.Settings.plugin, url, plugin, 0)
            };
        }
    }
}