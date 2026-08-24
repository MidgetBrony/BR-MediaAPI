using BR_MediaAPI;
using MelonLoader;
using Newtonsoft.Json;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(typeof(BR_DVDSample.DvdSampleMod), "BR-DVDSample", "1.0.0", "Rusty", null)]
[assembly: MelonGame("NestedLoop", "BOXROOM")]
[assembly: MelonAdditionalDependencies("BR_MediaAPI")]

namespace BR_DVDSample
{
    public sealed class DvdSampleMod : MelonMod
    {
        public const int DvdMediaTypeId = 1100;
        public const string DvdPlaceableId = "BR_DVDSample_DVDCase";

        public override void OnInitializeMelon()
        {
            var definition = new MediaTypeDefinition
            {
                Id = DvdMediaTypeId,
                Key = "com.rusty.boxroom.dvd-sample",
                DisplayName = "DVDs",
                ModelType = typeof(DvdItem),
                Library = DvdLibrary.Instance,
                AllowOnShelves = true,
                Inspect = new MediaInspectDefinition
                {
                    PrimaryActionLabel = "Play DVD",
                    OnPrimaryAction = context => DvdPlayer.Open((DvdItem)context.Item)
                },
                CreateUnplacedMediaBox = true,
                UnplacedMediaBoxName = "DVD Box",
                LibraryFolder = new MediaLibraryFolderOptions
                {
                    DefaultPath = DvdLibrarySettings.DefaultCacheRoot,
                    Label = "DVD Folder Location",
                    PanelTitle = "BR-DVDSample",
                    Reload = DvdLibrary.Instance.LoadCache,
                    GetStatus = () => $"{DvdLibrary.Instance.GetKnownItems().Count} DVDs found"
                }
            };

            SharedMediaCasePrefabs.Configure(definition);

            MediaApi.Register(definition);
            LoggerInstance.Msg($"DVD library configured at: {DvdLibrarySettings.CacheRoot}");
            LoggerInstance.Msg("Place a 'DVD Box', then take a cached DVD case from inside it.");
        }
    }

    public sealed class DvdItem : MediaItemBase
    {
        public DvdItem(string id) : base((eMediaType)DvdSampleMod.DvdMediaTypeId, id) { }

        public string FolderPath { get; internal set; } = string.Empty;
        public string ContentPath { get; internal set; } = string.Empty;
        public string Title { get; internal set; } = string.Empty;
        public string Studio { get; internal set; } = string.Empty;
        public string Genre { get; internal set; } = string.Empty;
        public string Rating { get; internal set; } = string.Empty;
        public string DvdType { get; internal set; } = string.Empty;
        public int Year { get; internal set; }
        public override string DisplayName => Title;
        internal void SetCover(byte[] bytes) => CoverArtBytes = bytes;
    }

    public sealed class DvdMetadata
    {
        public int Version { get; set; } = 1;
        public string DvdID { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Studio { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Genre { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
        public string Type { get; set; } = "DVD";
        public string FileName { get; set; } = string.Empty;
    }

    public static class DvdLibrarySettings
    {
        public static string DefaultCacheRoot => Path.Combine(
            Application.persistentDataPath,
            "Boxroom-Plus",
            "DVDs_Cache");

        public static string CacheRoot => MediaApi.GetLibraryFolder((eMediaType)DvdSampleMod.DvdMediaTypeId);
    }

    public sealed class DvdLibrary : IMediaLibrary
    {
        public static DvdLibrary Instance { get; } = new DvdLibrary();
        private static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".m4v", ".iso" };
        private static readonly string[] CoverNames = { "cover.jpg", "cover.jpeg", "cover.png", "folder.jpg", "poster.jpg", "poster.png" };
        private readonly Dictionary<string, DvdItem> items = new Dictionary<string, DvdItem>(StringComparer.OrdinalIgnoreCase);

        public eMediaType HandledType => (eMediaType)DvdSampleMod.DvdMediaTypeId;
        public event Action<IMediaItem> OnItemReady;
        public event Action<IReadOnlyList<IMediaItem>> OnLibraryReady;

        public IReadOnlyList<IMediaItem> GetKnownItems() => items.Values.Cast<IMediaItem>().ToList();
        public IMediaItem GetItemSync(MediaRef mediaRef)
        {
            if (mediaRef.Type != HandledType) return null;
            items.TryGetValue(mediaRef.Id, out DvdItem item);
            return item;
        }
        public Task<IMediaItem> GetItemAsync(MediaRef mediaRef) => Task.FromResult(GetItemSync(mediaRef));

        public void LoadCache()
        {
            items.Clear();
            EnsureSampleEntry();

            foreach (string folder in Directory.GetDirectories(DvdLibrarySettings.CacheRoot, "*", SearchOption.AllDirectories))
                LoadFolder(folder);

            OnLibraryReady?.Invoke(GetKnownItems());
        }

        private void LoadFolder(string folder)
        {
            try
            {
                string metadataPath = Path.Combine(folder, "meta.json");
                if (!File.Exists(metadataPath)) return;
                DvdMetadata metadata = JsonConvert.DeserializeObject<DvdMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.DvdID))
                {
                    MelonLogger.Warning($"Skipping DVD folder with no DvdID: {folder}");
                    return;
                }

                var item = new DvdItem(metadata.DvdID)
                {
                    FolderPath = folder,
                    Title = string.IsNullOrWhiteSpace(metadata.Title) ? metadata.DvdID : metadata.Title,
                    Studio = metadata.Studio,
                    Year = metadata.Year,
                    Genre = metadata.Genre,
                    Rating = metadata.Rating,
                    DvdType = metadata.Type
                };

                string namedContent = string.IsNullOrWhiteSpace(metadata.FileName) ? null : Path.Combine(folder, metadata.FileName);
                item.ContentPath = namedContent != null && File.Exists(namedContent)
                    ? namedContent
                    : Directory.GetFiles(folder).FirstOrDefault(path => VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) ?? string.Empty;

                string cover = CoverNames.Select(name => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
                if (cover != null) item.SetCover(File.ReadAllBytes(cover));

                items[item.Id] = item;
                OnItemReady?.Invoke(item);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed loading DVD folder '{folder}': {ex.Message}");
            }
        }

        private static void EnsureSampleEntry()
        {
            string folder = Path.Combine(DvdLibrarySettings.CacheRoot, "Sample_DVD");
            Directory.CreateDirectory(folder);
            string metadataPath = Path.Combine(folder, "meta.json");
            if (!File.Exists(metadataPath))
            {
                var metadata = new DvdMetadata
                {
                    Version = 1,
                    DvdID = "sample-dvd",
                    Title = "Sample DVD",
                    Studio = "BR-MediaAPI",
                    Year = 2026,
                    Genre = "Demo",
                    Rating = "Everyone",
                    Type = "DVD"
                };
                File.WriteAllText(metadataPath, JsonConvert.SerializeObject(metadata, Formatting.Indented));
            }

            string coverPath = Path.Combine(folder, "cover.png");
            if (!File.Exists(coverPath) || new FileInfo(coverPath).Length < 256)
                File.WriteAllBytes(coverPath, CreateSampleCover());
        }

        private static byte[] CreateSampleCover()
        {
            var texture = new Texture2D(128, 192, TextureFormat.RGBA32, false);
            var pixels = new Color[128 * 192];
            for (int row = 0; row < 192; row++)
                for (int column = 0; column < 128; column++)
                {
                    bool border = row < 7 || row >= 185 || column < 7 || column >= 121;
                    bool label = row > 72 && row < 120 && column > 18 && column < 110;
                    pixels[row * 128 + column] = border
                        ? Color.white
                        : label ? new Color(1f, 0.72f, 0.08f) : new Color(0.035f, 0.16f, 0.58f);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            byte[] bytes = texture.EncodeToPNG();
            UnityEngine.Object.Destroy(texture);
            return bytes;
        }
    }

    internal static class DvdPlayer
    {
        internal static void Open(DvdItem item)
        {
            if (item == null) return;
            if (string.IsNullOrWhiteSpace(item.ContentPath) || !File.Exists(item.ContentPath))
            {
                MelonLogger.Warning($"'{item.DisplayName}' has no video file. Add one to: {item.FolderPath}");
                return;
            }
            Application.OpenURL(new Uri(item.ContentPath).AbsoluteUri);
        }
    }

}
