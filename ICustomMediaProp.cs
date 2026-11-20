using SteamShelf.Media;
using SteamShelf.Placeables;

namespace BR_MediaAPI
{
    /// <summary>
    /// Implement this on a loose prefab to opt into generic shelf-to-hand and pickup routing.
    /// ApplyMedia should prepare SaveState.CustomData with the consumer's opaque media
    /// identity. BR-MediaAPI carries that data into BOXROOM's authoritative first-placement
    /// state; implementations may also rewrite it in LinkSaveState as a defensive safeguard.
    /// </summary>
    public interface ICustomMediaProp : IPlaceable
    {
        IMediaItem MediaItem { get; }
        void ApplyMedia(IMediaItem item);
    }
}
