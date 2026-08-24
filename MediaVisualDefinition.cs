using SteamShelf.Media;
using UnityEngine;

namespace BR_MediaAPI
{
    public sealed class MediaVisualDefinition
    {
        public System.Action<MediaVisualContext> OnHeld { get; set; }
        public System.Action<MediaVisualContext> OnInspect { get; set; }
    }

    public sealed class MediaVisualContext
    {
        internal MediaVisualContext(IMediaItem item, GameObject visual)
        {
            Item = item;
            Visual = visual;
        }

        public IMediaItem Item { get; }
        public GameObject Visual { get; }
    }
}
