using MelonLoader;
using SteamShelf.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BR_MediaAPI
{
    /// <summary>Public registration and lookup surface used by media mods.</summary>
    public static class MediaApi
    {
        // 0 and 1 are BOXROOM's built-ins; 2 was historically used by Books.
        // New API consumers use a higher namespace to avoid legacy collisions.
        public const int MinimumCustomId = 1000;

        private static readonly object Sync = new object();
        private static readonly Dictionary<int, MediaTypeDefinition> ById = new Dictionary<int, MediaTypeDefinition>();
        private static readonly Dictionary<string, MediaTypeDefinition> ByKey = new Dictionary<string, MediaTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, MediaTypeDefinition> ByPlaceableId = new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal);
        private static MelonLogger.Instance logger;
        private static bool routerReady;
        private static bool librariesConfigured;

        public static event Action<MediaTypeDefinition> Registered;
        internal static event Action MediaAvailabilityChanged;

        public static IReadOnlyCollection<MediaTypeDefinition> RegisteredTypes
        {
            get { lock (Sync) return ById.Values.ToArray(); }
        }

        public static void Register(MediaTypeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();

            bool loadAfterRegistration = false;
            lock (Sync)
            {
                if (ById.TryGetValue(definition.Id, out MediaTypeDefinition idOwner))
                    throw new InvalidOperationException($"Media ID {definition.Id} is already registered by '{idOwner.Key}'.");
                if (ByKey.ContainsKey(definition.Key))
                    throw new InvalidOperationException($"Media key '{definition.Key}' is already registered.");
                if (!string.IsNullOrWhiteSpace(definition.PlaceableId) && ByPlaceableId.ContainsKey(definition.PlaceableId))
                    throw new InvalidOperationException($"Placeable ID '{definition.PlaceableId}' is already registered.");

                ById.Add(definition.Id, definition);
                ByKey.Add(definition.Key, definition);
                if (!string.IsNullOrWhiteSpace(definition.PlaceableId)) ByPlaceableId.Add(definition.PlaceableId, definition);
                if (routerReady) RegisterLibrary(definition);
                loadAfterRegistration = librariesConfigured && definition.LibraryFolder?.LoadOnMediaBootstrap == true;
            }

            UnplacedMediaBoxSystem.TryRegister(definition);
            MediaLibraryFolderPanel.Register(definition);
            if (loadAfterRegistration) definition.LibraryFolder.Reload();

            logger?.Msg($"Registered {definition.DisplayName} ({definition.Key}, ID {definition.Id}).");
            Registered?.Invoke(definition);
        }

        public static bool TryGet(eMediaType type, out MediaTypeDefinition definition)
        {
            lock (Sync) return ById.TryGetValue((int)type, out definition);
        }

        public static bool TryGet(string key, out MediaTypeDefinition definition)
        {
            lock (Sync) return ByKey.TryGetValue(key ?? string.Empty, out definition);
        }

        public static bool TryGetByPlaceableId(string id, out MediaTypeDefinition definition)
        {
            lock (Sync) return ByPlaceableId.TryGetValue(id ?? string.Empty, out definition);
        }

        public static bool TryOpen(IMediaItem item)
        {
            if (item == null || !TryGet(item.Ref.Type, out MediaTypeDefinition definition)) return false;
            if (GenericMediaInspectorVisual.TryExecutePrimary(item)) return true;
            if (definition.OnOpen == null) return false;
            definition.OnOpen(item);
            return true;
        }

        /// <summary>Returns the persisted library folder, or the definition's default folder.</summary>
        public static string GetLibraryFolder(eMediaType type)
        {
            return TryGet(type, out MediaTypeDefinition definition)
                ? MediaLibraryFolderPanel.GetPath(definition)
                : string.Empty;
        }

        /// <summary>Returns the persisted library folder for a registered media key.</summary>
        public static string GetLibraryFolder(string key)
        {
            return TryGet(key, out MediaTypeDefinition definition)
                ? MediaLibraryFolderPanel.GetPath(definition)
                : string.Empty;
        }

        internal static void SetLogger(MelonLogger.Instance instance) => logger = instance;
        internal static void NotifyMediaAvailabilityChanged() => MediaAvailabilityChanged?.Invoke();

        internal static void AttachRegisteredLibraries()
        {
            lock (Sync)
            {
                routerReady = true;
                foreach (MediaTypeDefinition definition in ById.Values) RegisterLibrary(definition);
            }
        }

        internal static void RegisterSourceBoxes()
        {
            MediaTypeDefinition[] definitions;
            lock (Sync) definitions = ById.Values.ToArray();
            foreach (MediaTypeDefinition definition in definitions) UnplacedMediaBoxSystem.TryRegister(definition);
        }

        internal static void LoadRegisteredLibraries()
        {
            MediaTypeDefinition[] definitions;
            lock (Sync)
            {
                librariesConfigured = true;
                definitions = ById.Values.ToArray();
            }

            foreach (MediaTypeDefinition definition in definitions)
                if (definition.LibraryFolder?.LoadOnMediaBootstrap == true)
                    definition.LibraryFolder.Reload();
        }

        private static void RegisterLibrary(MediaTypeDefinition definition)
        {
            MediaLibraryRouter.UnRegister(definition.MediaType);
            MediaLibraryRouter.Register(definition.Library);
        }
    }
}
