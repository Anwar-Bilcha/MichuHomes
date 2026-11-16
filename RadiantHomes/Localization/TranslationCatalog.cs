using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RadiantHomes.Localization
{
    /// <summary>
    /// UI translations, one JSON file per language in Resources/Translations/{culture}.json:
    ///   { "English text": "Translated text", "{0} houses pending approval": "{0} ቤቶች ለማጽደቅ በመጠባበቅ ላይ" }
    /// Keys are the English text exactly as shown on screen (whitespace is normalised).
    /// Keys containing {0}, {1}… are patterns; their arguments are translated too when they are known texts.
    /// Keys starting with "_" are comments. English needs no file — it is the source language.
    /// </summary>
    public sealed partial class TranslationCatalog : IDisposable
    {
        public const string SourceCulture = "en";

        /// <param name="Slots">For each capture group (in order of appearance), the {n} index it fills.</param>
        private sealed record Pattern(Regex Regex, string Template, int LiteralLength, int[] Slots);
        private sealed class Language
        {
            public Dictionary<string, string> Exact { get; } = new(StringComparer.Ordinal);
            public List<Pattern> Patterns { get; } = new();
        }

        private readonly string _directory;
        private readonly ILogger<TranslationCatalog> _logger;
        private readonly FileSystemWatcher? _watcher;
        private volatile IReadOnlyDictionary<string, Language> _languages = new Dictionary<string, Language>();
        private readonly ConcurrentDictionary<(string, string), string?> _cache = new();

        public TranslationCatalog(IWebHostEnvironment env, ILogger<TranslationCatalog> logger)
        {
            _logger = logger;
            _directory = Path.Combine(env.ContentRootPath, "Resources", "Translations");
            Load();

            // Pick up edits to the JSON files without restarting (development convenience).
            if (env.IsDevelopment() && Directory.Exists(_directory))
            {
                _watcher = new FileSystemWatcher(_directory, "*.json") { EnableRaisingEvents = true };
                _watcher.Changed += (_, _) => Reload();
                _watcher.Created += (_, _) => Reload();
                _watcher.Renamed += (_, _) => Reload();
            }
        }

        /// <summary>Languages that have a translation file (plus English).</summary>
        public IReadOnlyCollection<string> Cultures => _languages.Keys.Append(SourceCulture).ToList();

        public bool Has(string culture) => _languages.ContainsKey(culture);

        /// <summary>The translation, or null when the text is unknown for that language.</summary>
        public string? Translate(string? text, string culture) => Translate(text, culture, depth: 0);

        /// <summary>The translation, or the original text when there is none.</summary>
        public string T(string text, string culture) => Translate(text, culture) ?? text;

        private string? Translate(string? text, string culture, int depth)
        {
            if (string.IsNullOrWhiteSpace(text) || culture == SourceCulture) return null;
            if (!_languages.TryGetValue(culture, out var language)) return null;

            var key = Normalize(text);
            if (key.Length == 0) return null;
            return _cache.GetOrAdd((culture, key), k => Lookup(language, k.Item2, culture, depth));
        }

        private string? Lookup(Language language, string key, string culture, int depth)
        {
            if (language.Exact.TryGetValue(key, out var exact)) return exact;
            if (depth > 2) return null;

            foreach (var pattern in language.Patterns)
            {
                var match = pattern.Regex.Match(key);
                if (!match.Success) continue;
                // Placeholders may appear in any order in the key ("at least {2} … at most {1}"):
                // each capture goes to the {n} slot it stands for, not to its position.
                var args = new object[pattern.Slots.Length == 0 ? 0 : pattern.Slots.Max() + 1];
                for (var i = 0; i < args.Length; i++) args[i] = string.Empty;
                for (var g = 1; g < match.Groups.Count && g - 1 < pattern.Slots.Length; g++)
                {
                    var arg = match.Groups[g].Value;
                    args[pattern.Slots[g - 1]] = Translate(arg, culture, depth + 1) ?? arg;
                }
                try { return string.Format(pattern.Template, args); }
                catch (FormatException) { _logger.LogWarning("Bad translation template for culture {Culture}: {Template}", culture, pattern.Template); }
            }
            return null;
        }

        public static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

        private void Reload()
        {
            // File writes arrive as several events; a short delay lets the editor finish.
            Thread.Sleep(200);
            try { Load(); } catch (Exception ex) { _logger.LogError(ex, "Reloading translations failed"); }
        }

        private void Load()
        {
            var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(_directory))
            {
                foreach (var file in Directory.GetFiles(_directory, "*.json"))
                {
                    var culture = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (culture == SourceCulture) continue;
                    try
                    {
                        languages[culture] = Parse(File.ReadAllText(file, Encoding.UTF8));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not read translation file {File}", file);
                    }
                }
            }
            _languages = languages;
            _cache.Clear();
            _logger.LogInformation("Translations loaded: {Summary}",
                string.Join(", ", languages.Select(l => $"{l.Key} ({l.Value.Exact.Count} texts, {l.Value.Patterns.Count} patterns)")));
        }

        private static Language Parse(string json)
        {
            var language = new Language();
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) ?? new();

            foreach (var (rawKey, value) in entries)
            {
                if (rawKey.StartsWith('_') || string.IsNullOrWhiteSpace(value)) continue;
                var key = Normalize(rawKey);
                if (Placeholder().IsMatch(key))
                {
                    var regex = "^" + Placeholder().Replace(Regex.Escape(key).Replace(@"\{", "{"), "(.+?)") + "$";
                    var literal = Placeholder().Replace(key, string.Empty).Length;
                    var slots = Placeholder().Matches(key).Select(m => int.Parse(m.Value.Trim('{', '}'))).ToArray();
                    language.Patterns.Add(new Pattern(new Regex(regex, RegexOptions.CultureInvariant | RegexOptions.Singleline), value, literal, slots));
                }
                else
                {
                    language.Exact[key] = value;
                }
            }

            // Most specific (longest literal text) first, so "Delete this {0}?" wins over "{0}?".
            language.Patterns.Sort((a, b) => b.LiteralLength.CompareTo(a.LiteralLength));
            return language;
        }

        public void Dispose() => _watcher?.Dispose();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();

        [GeneratedRegex(@"\{\d+\}")]
        private static partial Regex Placeholder();
    }
}
