using RadiantHomes.Services.Sales;

namespace RadiantHomes.Pages.Shared
{
    /// <summary>What Pages/Shared/_ChatPanel.cshtml shows: a conversation's messages and (when allowed) the reply form.</summary>
    /// <param name="PostUrl">The page posting a message without JavaScript (also the live connection's fallback).</param>
    /// <param name="PollUrl">JSON list of messages after an id (?handler=Messages&amp;id=…), when the live connection is down.</param>
    /// <param name="CannotSend">Why the form is hidden (closed, not verified, no permission); null when sending is allowed.</param>
    public sealed record ChatPanel(
        Guid ConversationId,
        bool MeIsCustomer,
        string Title,
        string? Subtitle,
        IReadOnlyList<MessageDto> Messages,
        string PostUrl,
        string PollUrl,
        string? CannotSend);
}
