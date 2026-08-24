using SteamShelf.Media;

namespace BR_MediaAPI
{
    /// <summary>Optional convenience model. Mods may implement IMediaItem directly instead.</summary>
    public abstract class MediaItemBase : IMediaItem
    {
        protected MediaItemBase(eMediaType type, string id) { Type = type; Id = id; }
        public eMediaType Type { get; }
        public string Id { get; }
        public abstract string DisplayName { get; }
        public virtual bool CoverArtLoaded => CoverArtBytes != null && CoverArtBytes.Length > 0;
        public virtual byte[] CoverArtBytes { get; protected set; }
        public virtual bool IsFullyLoaded { get; protected set; } = true;
        public virtual bool IsSpawned { get; set; }
        public virtual bool IsInHand { get; set; }
        public MediaRef Ref => new MediaRef(Type, Id);
    }
}
