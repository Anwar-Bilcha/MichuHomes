namespace RadiantHomes.Services.Security
{
    public static class AccessExtensions
    {
        /// <summary>The signed-in user's effective access (cached per request).</summary>
        public static Task<UserAccess> AccessAsync(this HttpContext httpContext) =>
            httpContext.RequestServices.GetRequiredService<AccessControlService>().GetAsync(httpContext.User);
    }
}
