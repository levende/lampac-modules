using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.Tasks;
using Shared.Models;
using Lampac;
using System;
using System.Linq;

namespace DlnaAuth
{
    public class Middlewares
    {
        public static async Task<bool> InvokeAsync(HttpContext context, IMemoryCache memoryCache)
        {
            var requestInfo = context.Features.Get<RequestModel>();

            if (AppInit.conf.accsdb.enable)
            {
                await WriteDenyResponseAsync(context);
                return false;
            }

            if (ModInit.Config.groupsAccess?.Any() != true)
            {
                return true;
            }

            var path = context.Request.Path.Value;

            if (string.IsNullOrEmpty(path)
                || !path.StartsWith("/dlna", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/dlna.js", StringComparison.OrdinalIgnoreCase)
                || ModInit.Config.groupsAccess.Contains(requestInfo.user.group))
            {
                return true;
            }

            await WriteDenyResponseAsync(context);
            return false;
        }

        private static async Task WriteDenyResponseAsync(HttpContext context)
        {
            var responsePayload = new
            {
                accsdb = true,
                msg = AppInit.conf.accsdb.denyGroupMesage
            };

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(responsePayload, context.RequestAborted);
        }
    }
}
