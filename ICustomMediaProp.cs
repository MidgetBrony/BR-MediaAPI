using SteamShelf.Media;
using SteamShelf.Placeables;

namespace BR_MediaAPI
{
    /// <summary>Implement this on a loose prefab to opt into generic shelf-to-hand and pickup routing.</summary>
    public interface ICustomMediaProp : IPlaceable
    {
        IMediaItem MediaItem { get; }
        void ApplyMedia(IMediaItem item);
    }
}
