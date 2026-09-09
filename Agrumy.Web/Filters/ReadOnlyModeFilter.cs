using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Agrumy.Web.Filters
{
    /// Computes ViewBag.IsReadOnly/CanEdit once per request from the caller's role cookie, so every view/partial can hide its Save/Delete/Add buttons the same way instead of each controller wiring this up itself - a Global reader is the only role this currently applies to.
    public sealed class ReadOnlyModeFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            bool isReadOnly = context.HttpContext.User.IsInRole(RoleNames.GlobalReader);
            if (context.Controller is Microsoft.AspNetCore.Mvc.Controller controller)
            {
                controller.ViewBag.IsReadOnly = isReadOnly;
                controller.ViewBag.CanEdit = !isReadOnly;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
