namespace RadiantHomes.Pages.LiveVideoMeet
{
    /// <summary>What Pages/LiveVideoMeet/_CallFrame.cshtml needs to start the embedded JaaS call (wwwroot/js/video-call.js).</summary>
    public sealed record CallView(
        bool IsAgent,
        string Domain,
        string ScriptUrl,
        string RoomPath,
        string Jwt,
        string Heading,
        string? Subheading,
        string DoneUrl,
        string DoneLabel);
}
