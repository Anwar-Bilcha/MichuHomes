using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RadiantHomes.Services.Security
{
    /// <summary>
    /// Renders an element only for users holding one of the listed permissions:
    /// <c>&lt;a mh-permission="projects.update" asp-page="./Edit"&gt;</c> or several, comma-separated.
    /// Buttons hidden this way are also refused on the server (permission policies and handler checks).
    /// </summary>
    [HtmlTargetElement(Attributes = AttributeName)]
    public sealed class PermissionTagHelper : TagHelper
    {
        public const string AttributeName = "mh-permission";

        [HtmlAttributeName(AttributeName)]
        public string Permissions { get; set; } = string.Empty;

        [ViewContext, HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = null!;

        // Run before other tag helpers (asp-page links etc.) so hidden elements cost nothing.
        public override int Order => -1000;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var keys = Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var access = await ViewContext.HttpContext.AccessAsync();
            if (!access.IsActive || !keys.Any(access.Has)) output.SuppressOutput();
        }
    }
}
