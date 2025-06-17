namespace RadiantHomes.LogConfig
{
    using Serilog.Core;
    using Serilog.Events;
    using Microsoft.AspNetCore.Http;
    using System.Security.Claims;

    public class SerilogUserEnricher : ILogEventEnricher
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SerilogUserEnricher(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string userName = "Anonymous";

            try
            {
                if (httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    userName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value ??
                               httpContext.User.Identity.Name ??
                               "AuthenticatedUser";
                }
            }
            catch
            {
                // Log or silently fail — avoid breaking request logging
            }

            var userProperty = propertyFactory.CreateProperty("UserName", userName);
            logEvent.AddPropertyIfAbsent(userProperty);
        }
    }

}
