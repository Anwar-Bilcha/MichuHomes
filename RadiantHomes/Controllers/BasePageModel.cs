using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace RadiantHomes.Controllers
{
    // BasePageModel.c
    public abstract class BasePageModel : PageModel
    {
        protected readonly ILogger Logger;

        protected BasePageModel(ILogger logger)
        {
            Logger = logger;
        }

        public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
        {
            Logger.LogInformation("Executing handler: {Handler} on page: {Page}", context.HandlerMethod?.Name, context.ActionDescriptor?.DisplayName);
            base.OnPageHandlerExecuting(context);
        }

        public override void OnPageHandlerExecuted(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutedContext context)
        {
            Logger.LogInformation("Executed handler: {Handler} on page: {Page}", context.HandlerMethod?.Name, context.ActionDescriptor?.DisplayName);
            base.OnPageHandlerExecuted(context);
        }
    }

}
