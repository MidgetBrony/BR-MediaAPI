using SteamShelf.Media;
using SteamShelf.Placeables;

namespace BR_MediaAPI
{
    /// <summary>
    /// Implement this on a loose prefab to opt into generic shelf-to-hand and pickup routing.
    /// Implementations that prepare CustomData before first placement must carry it into
    /// the fresh PlaceableSaveState supplied through IPlaceable.LinkSaveState.
    /// </summary>
    public interface ICustomMediaProp : IPlaceable
    {
        IMediaItem MediaItem { get; }
        void ApplyMedia(IMediaItem item);
    }
}
