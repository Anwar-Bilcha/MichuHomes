namespace RadiantHomes.Pages.LiveVideoMeet
{
    /// <summary>What Pages/LiveVideoMeet/_CallFrame.cshtml needs to start the embedded JaaS call (wwwroot/js/video-call.js).</summary>
    /// <param name="EndUrl">Posted when this side leaves the call (the call log is closed).</param>
    /// <param name="StatusUrl">Customer only: polled while waiting, so a declined or missed call ends the wait.</param>
    /// <param name="AudioOnly">Start without the camera (weak connections, no camera).</param>
    public sealed record CallView(
        bool IsAgent,
        string Domain,
        string ScriptUrl,
        string RoomPath,
        string Jwt,
        string Heading,
        string? Subheading,
        string DoneUrl,
        string DoneLabel,
        string? EndUrl = null,
        string? StatusUrl = null,
        bool AudioOnly = false);
}
