namespace Cevir.Core.Social
{
    /// <summary>Sharing and invites. Followers and friends are not part of the app yet, so nothing is shown for them.</summary>
    public interface ISocialService
    {
        string InviteLink { get; }
    }

    public sealed class InviteOnlySocialService : ISocialService
    {
        public string InviteLink => "https://github.com/aytac-pixel/chevir-io/releases/latest/download/ChevirIO.apk"; // newest APK
    }
}
