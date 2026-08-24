using SteamShelf.Save;
using System;
using System.IO;

namespace BR_MediaAPI
{
    /// <summary>Configures the standard ModsPanel library-folder controls for a media type.</summary>
    public sealed class MediaLibraryFolderOptions
    {
        /// <summary>Default location used until the player chooses another folder.</summary>
        public string DefaultPath { get; set; }

        /// <summary>Stable SaveManager key. Defaults to BRMediaAPI.LibraryFolder.{media key}.</summary>
        public string SettingId { get; set; }

        public string Label { get; set; }
        public string BrowseTitle { get; set; }
        public string PanelTitle { get; set; }
        public int PanelOrder { get; set; } = 120;
        public string EmptyStatus { get; set; }

        /// <summary>Called after Browse changes the folder and when Refresh is pressed.</summary>
        public Action Reload { get; set; }

        /// <summary>Loads the library automatically when BOXROOM's media router becomes ready.</summary>
        public bool LoadOnMediaBootstrap { get; set; } = true;

        /// <summary>Optional live status such as "24 records found".</summary>
        public Func<string> GetStatus { get; set; }

        public bool ShowOpenFolderButton { get; set; } = true;

        internal void Validate(MediaTypeDefinition definition)
        {
            if (Reload == null) throw new ArgumentNullException(nameof(Reload), "LibraryFolder.Reload is required.");
            SettingId = string.IsNullOrWhiteSpace(SettingId) ? $"BRMediaAPI.LibraryFolder.{definition.Key}" : SettingId;
            Label = string.IsNullOrWhiteSpace(Label) ? $"{definition.DisplayName} Folder Location" : Label;
            BrowseTitle = string.IsNullOrWhiteSpace(BrowseTitle) ? $"Select {definition.DisplayName.ToLowerInvariant()} library folder" : BrowseTitle;
            PanelTitle = string.IsNullOrWhiteSpace(PanelTitle) ? definition.DisplayName : PanelTitle;
            EmptyStatus = string.IsNullOrWhiteSpace(EmptyStatus) ? $"No {definition.DisplayName.ToLowerInvariant()} folder configured" : EmptyStatus;
        }
    }

    internal static class MediaLibraryFolderPanel
    {
        internal static void Register(MediaTypeDefinition definition)
        {
            MediaLibraryFolderOptions options = definition.LibraryFolder;
            if (options == null) return;

            ModsPanel.ModSection section = ModsPanel.ModsPanelApi
                .RegisterSection(definition.Key, options.PanelTitle, options.PanelOrder)
                .Clear()
                .AddFolder(
                    "library-folder",
                    options.Label,
                    () => GetPath(definition),
                    path => SetPathAndReload(definition, path),
                    () => GetStatus(definition),
                    options.Reload,
                    options.BrowseTitle);

            if (options.ShowOpenFolderButton)
                section.AddButton("open-library-folder", "Open the configured library folder", "Open Folder", () => OpenFolder(definition));
        }

        internal static string GetPath(MediaTypeDefinition definition)
        {
            MediaLibraryFolderOptions options = definition?.LibraryFolder;
            if (options == null) return string.Empty;
            if (!Singleton<SaveManager>.HasInstance()) return options.DefaultPath ?? string.Empty;
            return Singleton<SaveManager>.Instance.Settings.GetValue(options.SettingId, options.DefaultPath ?? string.Empty);
        }

        private static void SetPathAndReload(MediaTypeDefinition definition, string path)
        {
            MediaLibraryFolderOptions options = definition.LibraryFolder;
            if (Singleton<SaveManager>.HasInstance())
            {
                Singleton<SaveManager>.Instance.Settings.SetValue(options.SettingId, path ?? string.Empty);
                Singleton<SaveManager>.Instance.Settings.Save();
            }
            options.Reload();
        }

        private static string GetStatus(MediaTypeDefinition definition)
        {
            MediaLibraryFolderOptions options = definition.LibraryFolder;
            string path = GetPath(definition);
            if (string.IsNullOrWhiteSpace(path)) return options.EmptyStatus;
            return options.GetStatus?.Invoke() ?? (Directory.Exists(path) ? "Folder ready" : "Folder not found");
        }

        private static void OpenFolder(MediaTypeDefinition definition)
        {
            string path = GetPath(definition);
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) FileExtensions.OpenDirectory(path);
        }
    }
}
