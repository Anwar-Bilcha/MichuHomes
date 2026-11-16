using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RadiantHomes.Localization
{
    /// <summary>
    /// Translates rendered HTML pages into the visitor's language (see TranslationCatalog).
    /// Pages are written in English as usual; for other languages every visible text node and the user-facing
    /// attributes (placeholder, title, aria-label, alt, confirm texts, validation messages, button values) are
    /// looked up in the catalog. Content inside script/style/svg/code/textarea, and anything marked
    /// translate="no", is left alone. Data from the database (names, titles…) simply has no translation and
    /// stays as entered.
    /// </summary>
    public sealed partial class HtmlTranslationMiddleware
    {
        private static readonly HashSet<string> SkipElements = new(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "svg", "code", "pre", "textarea", "noscript", "template", "kbd", "samp"
        };

        private static readonly HashSet<string> TextAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "placeholder", "title", "aria-label", "alt", "data-confirm", "data-confirm-title", "data-confirm-ok",
            "data-bs-title", "data-placeholder"
        };

        static HtmlTranslationMiddleware()
        {
            // HtmlAgilityPack treats <form> and <option> as "empty" by default, which detaches their content.
            HtmlNode.ElementsFlags.Remove("form");
            HtmlNode.ElementsFlags.Remove("option");
        }

        private readonly RequestDelegate _next;
        private readonly TranslationCatalog _catalog;
        private readonly MissingTranslations _missing;

        public HtmlTranslationMiddleware(RequestDelegate next, TranslationCatalog catalog, MissingTranslations missing)
        {
            _next = next;
            _catalog = catalog;
            _missing = missing;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (culture == TranslationCatalog.SourceCulture || !_catalog.Has(culture) || !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
            {
                await _next(context);
                return;
            }

            var originalBody = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await _next(context);
            }
            finally
            {
                context.Response.Body = originalBody;
            }

            buffer.Position = 0;
            var contentType = context.Response.ContentType ?? string.Empty;
            var isHtml = contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                         && !context.Response.Headers.ContainsKey("Content-Encoding");
            if (!isHtml || buffer.Length == 0)
            {
                await buffer.CopyToAsync(originalBody);
                return;
            }

            var html = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
            var translated = TranslateHtml(html, culture, context.Request.Path);
            var bytes = Encoding.UTF8.GetBytes(translated);
            context.Response.ContentLength = bytes.Length;
            await originalBody.WriteAsync(bytes);
        }

        public string TranslateHtml(string html, string culture, string path)
        {
            var doc = new HtmlDocument { OptionOutputOriginalCase = true, OptionDefaultStreamEncoding = Encoding.UTF8 };
            doc.LoadHtml(html);
            Walk(doc.DocumentNode, culture, path);
            using var writer = new StringWriter();
            doc.Save(writer);
            return writer.ToString();
        }

        private void Walk(HtmlNode node, string culture, string path)
        {
            foreach (var child in node.ChildNodes.ToList())
            {
                switch (child.NodeType)
                {
                    case HtmlNodeType.Text:
                        TranslateText((HtmlTextNode)child, culture, path);
                        break;

                    case HtmlNodeType.Element:
                        if (string.Equals(child.GetAttributeValue("translate", ""), "no", StringComparison.OrdinalIgnoreCase)) break;

                        if (child.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
                        {
                            // <title> content is raw text in HtmlAgilityPack.
                            var title = WebUtility.HtmlDecode(child.InnerHtml);
                            var t = Lookup(title, culture, path, "title");
                            if (t != null) child.InnerHtml = WebUtility.HtmlEncode(t);
                            break;
                        }

                        TranslateAttributes(child, culture, path);
                        if (!SkipElements.Contains(child.Name)) Walk(child, culture, path);
                        break;
                }
            }
        }

        private void TranslateText(HtmlTextNode node, string culture, string path)
        {
            var raw = node.Text;
            if (string.IsNullOrWhiteSpace(raw)) return;

            var decoded = WebUtility.HtmlDecode(raw);
            var translated = Lookup(decoded, culture, path, "text");
            if (translated == null) return;

            var leading = decoded.Length - decoded.TrimStart().Length;
            var trailing = decoded.Length - decoded.TrimEnd().Length;
            node.Text = decoded[..leading] + WebUtility.HtmlEncode(translated) + decoded[(decoded.Length - trailing)..];
        }

        private void TranslateAttributes(HtmlNode element, string culture, string path)
        {
            foreach (var attribute in element.Attributes)
            {
                var name = attribute.Name;
                var translatable =
                    TextAttributes.Contains(name)
                    || name.StartsWith("data-val-", StringComparison.OrdinalIgnoreCase) && !name.Contains("-additionalfields") && !name.EndsWith("-other")
                    || name.Equals("value", StringComparison.OrdinalIgnoreCase) && element.Name.Equals("input", StringComparison.OrdinalIgnoreCase)
                       && element.GetAttributeValue("type", "") is "submit" or "button" or "reset"
                    || name.Equals("content", StringComparison.OrdinalIgnoreCase) && element.Name.Equals("meta", StringComparison.OrdinalIgnoreCase)
                       && element.GetAttributeValue("name", "") == "description";
                if (!translatable || string.IsNullOrWhiteSpace(attribute.Value)) continue;

                var decoded = WebUtility.HtmlDecode(attribute.Value);
                var translated = Lookup(decoded, culture, path, "attr:" + name);
                if (translated != null) attribute.Value = WebUtility.HtmlEncode(translated);
            }
        }

        private string? Lookup(string text, string culture, string path, string kind)
        {
            var translated = _catalog.Translate(text, culture);
            if (translated == null && _missing.Enabled && LooksTranslatable(text))
            {
                _missing.Record(culture, TranslationCatalog.Normalize(text), path, kind);
            }
            return translated;
        }

        /// <summary>Has letters in a Latin script (numbers, dates, symbols and Ethiopic text need no translation).</summary>
        private static bool LooksTranslatable(string text) => LatinWord().IsMatch(text);

        [GeneratedRegex(@"[A-Za-z]{2,}")]
        private static partial Regex LatinWord();
    }

    /// <summary>Development aid: texts that were shown without a translation (see /_dev/translations/missing).</summary>
    public sealed class MissingTranslations
    {
        public bool Enabled { get; }
        private readonly ConcurrentDictionary<(string Culture, string Text), ConcurrentDictionary<string, byte>> _items = new();

        public MissingTranslations(IConfiguration configuration, IWebHostEnvironment env)
        {
            Enabled = env.IsDevelopment() && configuration.GetValue("Localization:CollectMissing", true);
        }

        public void Record(string culture, string text, string path, string kind)
        {
            var where = _items.GetOrAdd((culture, text), _ => new ConcurrentDictionary<string, byte>());
            if (where.Count < 5) where.TryAdd($"{kind} @ {path}", 0);
        }

        public object Report(string culture) => _items
            .Where(i => i.Key.Culture == culture)
            .OrderBy(i => i.Key.Text, StringComparer.OrdinalIgnoreCase)
            .Select(i => new { text = i.Key.Text, seen = i.Value.Keys.OrderBy(k => k).ToArray() })
            .ToList();

        public void Clear() => _items.Clear();
    }
}
