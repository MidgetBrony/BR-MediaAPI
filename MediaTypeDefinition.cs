using SteamShelf.Media;
using SteamShelf.Placeables;
using System;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Everything BOXROOM needs to treat a mod's model as a first-class media type.</summary>
    public sealed class MediaTypeDefinition
    {
        public int Id { get; set; }
        /// <summary>Allows an established pre-API media ID such as Books ID 2. New mods should never enable this.</summary>
        public bool AllowLegacyId { get; set; }
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public IMediaLibrary Library { get; set; }
        public Func<Transform, IShelfItem> ShelfFactory { get; set; }
        public bool AllowOnShelves { get; set; } = true;

        public string PlaceableId { get; set; }
        public Func<PlaceableData> PlaceableDataFactory { get; set; }
        public Func<GameObject> LoosePrefabFactory { get; set; }
        public Func<GameObject> HeldPrefabFactory { get; set; }

        public Type ModelType { get; set; }
        public Action<IMediaItem> OnOpen { get; set; }
        /// <summary>Optional custom inspector presentation and interaction lifecycle.</summary>
        public MediaInspectDefinition Inspect { get; set; }
        /// <summary>Disable when a media mod supplies its own held/loose/inspect lifecycle.</summary>
        public bool UseGenericInteractionLifecycle { get; set; } = true;
        /// <summary>Optional media-specific adjustments applied after the API creates held or inspect visuals.</summary>
        public MediaVisualDefinition Visuals { get; set; }

        /// <summary>Optional native ModsPanel folder picker, persistence, refresh, status, and Open Folder controls.</summary>
        public MediaLibraryFolderOptions LibraryFolder { get; set; }

        /// <summary>Adds a native BOXROOM-style box containing all currently unplaced items.</summary>
        public bool CreateUnplacedMediaBox { get; set; } = true;
        public string UnplacedMediaBoxId { get; set; }
        public string UnplacedMediaBoxName { get; set; }
        public string UnplacedMediaBoxDescription { get; set; }

        public eMediaType MediaType => (eMediaType)Id;

        internal void Validate()
        {
            if (Id < MediaApi.MinimumCustomId && !AllowLegacyId)
                throw new ArgumentOutOfRangeException(nameof(Id), $"Custom media IDs must be {MediaApi.MinimumCustomId} or greater.");
            if (string.IsNullOrWhiteSpace(Key)) throw new ArgumentException("A globally unique key is required.", nameof(Key));
            if (string.IsNullOrWhiteSpace(DisplayName)) throw new ArgumentException("A display name is required.", nameof(DisplayName));
            if (Library == null) throw new ArgumentNullException(nameof(Library));
            if ((int)Library.HandledType != Id) throw new ArgumentException("Library.HandledType must match Id.", nameof(Library));
            if (ModelType == null || !typeof(IMediaItem).IsAssignableFrom(ModelType))
                throw new ArgumentException("ModelType must implement IMediaItem.", nameof(ModelType));
            LibraryFolder?.Validate(this);
            if (AllowOnShelves && ShelfFactory == null) throw new ArgumentNullException(nameof(ShelfFactory));

            bool hasAnyPlaceable = !string.IsNullOrWhiteSpace(PlaceableId) || PlaceableDataFactory != null || LoosePrefabFactory != null;
            bool hasAllPlaceable = !string.IsNullOrWhiteSpace(PlaceableId) && PlaceableDataFactory != null && LoosePrefabFactory != null;
            if (hasAnyPlaceable && !hasAllPlaceable)
                throw new ArgumentException("PlaceableId, PlaceableDataFactory, and LoosePrefabFactory must be supplied together.");
            if (hasAllPlaceable && HeldPrefabFactory == null)
                throw new ArgumentNullException(nameof(HeldPrefabFactory), "A dedicated held/display prefab is required for placeable custom media.");
        }

        internal IShelfItem CreateShelfItem(Transform parent) => ShelfFactory?.Invoke(parent);
        internal PlaceableData GetPlaceableData() => PlaceableDataFactory?.Invoke();
        internal GameObject CreateLoosePrefab()
        {
            GameObject instance = LoosePrefabFactory?.Invoke();
            LooseMediaPrefabSetup.Configure(instance, this);
            return instance;
        }
        internal GameObject CreateHeldPrefab() => HeldPrefabFactory?.Invoke();

        internal string SourceBoxId => string.IsNullOrWhiteSpace(UnplacedMediaBoxId) ? $"BR_MediaAPI_UnplacedBox_{Id}" : UnplacedMediaBoxId;
        internal string SourceBoxName => string.IsNullOrWhiteSpace(UnplacedMediaBoxName) ? $"{DisplayName} Box" : UnplacedMediaBoxName;
        internal string SourceBoxDescription => string.IsNullOrWhiteSpace(UnplacedMediaBoxDescription)
            ? $"A box containing all of your unplaced {DisplayName.ToLowerInvariant()}"
            : UnplacedMediaBoxDescription;
    }
}
