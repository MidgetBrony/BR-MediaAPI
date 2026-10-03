using SteamShelf.Media;
using System;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>The BOXROOM presentation currently displaying a media item.</summary>
    public enum MediaVisualUsage
    {
        Shelf,
        Loose,
        Held,
        Inspect
    }

    /// <summary>
    /// Describes a visual-only replacement for an existing media item. The media
    /// keeps its native BOXROOM identity, persistence, interaction and launch path.
    /// </summary>
    public sealed class MediaVisualOverrideDefinition
    {
        public string Key { get; set; }
        public int Priority { get; set; }
        public Func<IMediaItem, bool> Matches { get; set; }
        public Func<MediaVisualOverrideContext, GameObject> CreateVisual { get; set; }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key)) throw new ArgumentException("A visual override key is required.", nameof(Key));
            if (Matches == null) throw new ArgumentException("A visual override matcher is required.", nameof(Matches));
            if (CreateVisual == null) throw new ArgumentException("A visual override factory is required.", nameof(CreateVisual));
        }
    }

    public sealed class MediaVisualOverrideContext
    {
        internal MediaVisualOverrideContext(IMediaItem item, MediaVisualUsage usage, Component target, Vector3 anchorCenter, Bounds anchorBounds, Bounds anchorWorldBounds)
        {
            Item = item;
            Usage = usage;
            Target = target;
            AnchorCenter = anchorCenter;
            AnchorBounds = anchorBounds;
            AnchorWorldBounds = anchorWorldBounds;
        }

        public IMediaItem Item { get; }
        public MediaVisualUsage Usage { get; }
        public Component Target { get; }
        public Vector3 AnchorCenter { get; }
        /// <summary>
        /// Bounds of the native presentation being replaced, expressed in the
        /// target's local space. This lets replacements retain the native base
        /// alignment instead of only sharing its centre.
        /// </summary>
        public Bounds AnchorBounds { get; }
        /// <summary>World-space bounds of the native presentation being replaced.</summary>
        public Bounds AnchorWorldBounds { get; }
    }
}
