using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RadiantHomes.Services.VideoCalls
{
    /// <summary>
    /// JaaS (8x8 "Jitsi as a Service") account used for the video calls with agents. Configure it in user-secrets
    /// or environment variables (never in appsettings.json): Jaas__AppId, Jaas__ApiKeyId and Jaas__PrivateKey
    /// (or Jaas__PrivateKeyPath to the .pk file downloaded from the JaaS console, https://jaas.8x8.vc).
    /// </summary>
    public sealed class JaasSettings
    {
        public const string Section = "Jaas";

        /// <summary>The AppID, e.g. "vpaas-magic-cookie-1fc5…".</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>The API key id from the console ("4f4910") or the full kid ("vpaas-magic-cookie-…/4f4910").</summary>
        public string ApiKeyId { get; set; } = string.Empty;

        /// <summary>The RSA private key of that API key (PEM text).</summary>
        public string PrivateKey { get; set; } = string.Empty;

        /// <summary>Alternatively, the path of the private key file.</summary>
        public string PrivateKeyPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Signs the JaaS JWT every participant needs (RS256; format from developer.8x8.com/jaas/docs/api-keys-jwt) and
    /// gives the embed details: domain, script URL and the "AppID/room" name.
    /// </summary>
    public sealed class JaasTokens : IDisposable
    {
        public const string Domain = "8x8.vc";

        private readonly JaasSettings _settings;
        private readonly RSA? _key;

        public JaasTokens(IOptions<JaasSettings> options, IWebHostEnvironment env, ILogger<JaasTokens> logger)
        {
            _settings = options.Value;
            if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.ApiKeyId))
                return;

            var pem = _settings.PrivateKey;
            if (string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(_settings.PrivateKeyPath))
            {
                var path = Path.IsPathRooted(_settings.PrivateKeyPath) ? _settings.PrivateKeyPath : Path.Combine(env.ContentRootPath, _settings.PrivateKeyPath);
                pem = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                if (pem.Length == 0) logger.LogWarning("JaaS private key file {Path} was not found; video calls are off.", path);
            }
            if (string.IsNullOrWhiteSpace(pem)) return;

            try
            {
                var key = RSA.Create();
                key.ImportFromPem(pem.Replace("\\n", "\n")); // a key pasted into an environment variable keeps literal \n
                _key = key;
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                logger.LogWarning("The JaaS private key could not be read ({Error}); video calls are off.", ex.Message);
            }
        }

        /// <summary>Video calls work only with a complete JaaS configuration.</summary>
        public bool IsConfigured => _key is not null;

        public string ScriptUrl => $"https://{Domain}/{_settings.AppId}/external_api.js";

        /// <summary>The room name the iframe API expects: "AppID/room".</summary>
        public string RoomPath(string room) => $"{_settings.AppId}/{room}";

        /// <summary>A token for one participant of one room (moderators are the agents).</summary>
        public string CreateToken(string room, string userId, string displayName, bool moderator, TimeSpan lifetime)
        {
            if (_key is null) throw new InvalidOperationException("JaaS is not configured.");

            var kid = _settings.ApiKeyId.Contains('/') ? _settings.ApiKeyId : $"{_settings.AppId}/{_settings.ApiKeyId}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = new Dictionary<string, object> { ["alg"] = "RS256", ["kid"] = kid, ["typ"] = "JWT" };
            var payload = new Dictionary<string, object>
            {
                ["aud"] = "jitsi",
                ["iss"] = "chat",
                ["sub"] = _settings.AppId,
                ["room"] = room, // this room only (context.room.regex = false)
                ["nbf"] = now - 30,
                ["exp"] = now + (long)lifetime.TotalSeconds,
                ["context"] = new Dictionary<string, object>
                {
                    ["user"] = new Dictionary<string, object>
                    {
                        ["id"] = userId,
                        ["name"] = displayName,
                        ["avatar"] = string.Empty,
                        ["email"] = string.Empty, // not shared with the video service or other participants
                        ["moderator"] = moderator ? "true" : "false",
                    },
                    ["features"] = new Dictionary<string, object>
                    {
                        ["livestreaming"] = false,
                        ["recording"] = false,
                        ["transcription"] = false,
                        ["outbound-call"] = false,
                        ["sip-outbound-call"] = false,
                    },
                    ["room"] = new Dictionary<string, object> { ["regex"] = false },
                },
            };

            var signingInput = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}";
            var signature = _key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{signingInput}.{Base64Url(signature)}";
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public void Dispose() => _key?.Dispose();
    }
}
