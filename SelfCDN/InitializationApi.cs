using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models.Module;
using Shared.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace SelfCDN
{
    public class InitializationApi
    {
        public static Task<ActionResult> InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache, RequestModel requestInfo, string host, InitializationModel args)
        {
            return Task.FromResult(null as ActionResult);
        }
    }
}