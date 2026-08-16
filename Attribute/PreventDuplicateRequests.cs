using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace lotus_blue.Attributes
{
    public class PreventDuplicationRequestAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;
        private readonly double _milliseconds;

        public PreventDuplicationRequestAttribute(double milliseconds = 1000)
        {
            _milliseconds = milliseconds;
        }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var cache = serviceProvider.GetRequiredService<IMemoryCache>();
            return new PreventDuplicationRequestFilter(cache, _milliseconds);
        }

        private class PreventDuplicationRequestFilter : IActionFilter
        {
            private readonly IMemoryCache _cache;
            private readonly TimeSpan _timeout;

            public PreventDuplicationRequestFilter(IMemoryCache cache, double milliseconds)
            {
                _cache = cache;
                _timeout = TimeSpan.FromMilliseconds(milliseconds);
            }

            public void OnActionExecuting(ActionExecutingContext context)
            {
                var userId = context.HttpContext.User.Identity.IsAuthenticated
                             ? context.HttpContext.User.Identity.Name
                             : context.HttpContext.Connection.RemoteIpAddress.ToString();
                var cacheKey = GenerateCacheKey(context, userId);

                if (_cache.TryGetValue(cacheKey, out _))
                {
                    context.Result = new StatusCodeResult(429); // Too Many Requests
                    return;
                }

                _cache.Set(cacheKey, true, _timeout);
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                // You may consider removing the cache key here if the request completes successfully
                // _cache.Remove(cacheKey);
            }

            private string GenerateCacheKey(ActionExecutingContext context, string userId)
            {
                var request = context.HttpContext.Request;
                var key = $"{userId}:{request.Path}:{request.QueryString}:{request.Method}";
                return key;
            }
        }
    }
}
