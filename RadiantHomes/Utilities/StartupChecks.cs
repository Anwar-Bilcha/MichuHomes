namespace RadiantHomes.Utilities
{
    /// <summary>
    /// Start-up checks for servers (Program.cs, outside Development): which settings are missing or still point at a
    /// developer machine. Only setting names are logged, never their values. Settings come from environment variables
    /// (ConnectionStrings__DefaultConnection, Authentication__Google__ClientId, …) or appsettings.Production.json;
    /// see appsettings.Production.example.json.
    /// </summary>
    public static class StartupChecks
    {
        private static readonly (string Feature, string[] Keys)[] OptionalFeatures =
        {
            ("Google sign-in", new[] { "Authentication:Google:ClientId", "Authentication:Google:ClientSecret" }),
            ("Facebook sign-in", new[] { "Authentication:Facebook:AppId", "Authentication:Facebook:AppSecret" }),
            ("SMS (Twilio)", new[] { "Twilio:AccountSid", "Twilio:AuthToken", "Twilio:FromPhoneNumber" }),
            ("Payments (Chapa)", new[] { "Chapa:SecretKey" }),
            ("Video calls (JaaS)", new[] { "Jaas:AppId", "Jaas:ApiKeyId" }),
            ("Account emails (password reset, invitations)", new[] { "Email:Host", "Email:FromAddress" }),
        };

        public static void LogMissingSettings(IConfiguration configuration, ILogger logger)
        {
            var connection = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connection))
            {
                logger.LogCritical("ConnectionStrings:DefaultConnection is not set.");
            }
            else if (connection.Contains("RADIANTTECH", StringComparison.OrdinalIgnoreCase)
                     || connection.Contains("Trusted_Connection=true", StringComparison.OrdinalIgnoreCase)
                     || connection.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogCritical("ConnectionStrings:DefaultConnection still points at a developer database (Windows login). " +
                    "Set the server's connection string (environment variable ConnectionStrings__DefaultConnection or appsettings.Production.json).");
            }

            foreach (var (feature, keys) in OptionalFeatures)
            {
                var missing = keys.Where(k => string.IsNullOrWhiteSpace(configuration[k])).ToList();
                if (missing.Count > 0)
                    logger.LogWarning("{Feature} is off: {Missing} not set.", feature, string.Join(", ", missing));
            }
        }

        /// <summary>Whether the app may create and write files in this folder (creates it when needed).</summary>
        public static bool CanWrite(DirectoryInfo folder)
        {
            try
            {
                folder.Create();
                var probe = Path.Combine(folder.FullName, $".write-test-{Guid.NewGuid():N}");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }
}
