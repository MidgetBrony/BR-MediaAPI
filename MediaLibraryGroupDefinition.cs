using ModsPanel;
using SteamShelf.Media;
using System;

namespace BR_MediaAPI
{
    /// <summary>A consumer-defined category rendered as a nested tab on a media library page.</summary>
    public sealed class MediaLibraryGroupDefinition
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public Func<IMediaItem, bool> Includes { get; set; }
        public ModSection SettingsSection { get; set; }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key)) throw new ArgumentException("A stable group key is required.", nameof(Key));
            if (string.IsNullOrWhiteSpace(DisplayName)) throw new ArgumentException("A group display name is required.", nameof(DisplayName));
            if (Includes == null) throw new ArgumentNullException(nameof(Includes));
        }
    }
}
