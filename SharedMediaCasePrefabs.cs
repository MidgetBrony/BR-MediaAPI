using MelonLoader;
using MelonLoader.Utils;
using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Boxroom-Studios' standard media case for shelf, loose, and held contexts.</summary>
    public static class SharedMediaCasePrefabs
    {
        private const string BundleName = "brmediaapi_assets";
        private static readonly Dictionary<int, PlaceableData> Placeables = new Dictionary<int, PlaceableData>();
        private static AssetBundle bundle;
        private static bool ownsBundle;

        public static bool IsLoaded => bundle != null;
        public static AssetBundle SharedBundle => bundle;

        public static bool Load()
        {
            if (bundle != null) return true;
            bundle = TryGetBookSystemBundle();
            ownsBundle = false;
            if (bundle != null) return true;
            string path = Path.Combine(MelonEnvironment.ModsDirectory, BundleName);
            if (!File.Exists(path)) { MelonLogger.Error($"BR-MediaAPI asset bundle not found: {path}"); return false; }
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) { MelonLogger.Error($"Could not load BR-MediaAPI asset bundle: {path}"); return false; }
            ownsBundle = true;
            return true;
        }

        private static AssetBundle TryGetBookSystemBundle()
        {
            try
            {
                Type bookAssets = Type.GetType("Boxroom_Books.BookAssetBundle, BR_BookSystem", throwOnError: false);
                return bookAssets?.GetProperty("SharedBundle")?.GetValue(null) as AssetBundle;
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"Could not query BR-BookSystem's shared bundle: {exception.Message}");
                return null;
            }
        }

        /// <summary>Configures all three standard case contexts. Call before MediaApi.Register.</summary>
        public static void Configure(MediaTypeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!Load()) throw new InvalidOperationException("The BR-MediaAPI shared prefab bundle is unavailable.");

            definition.PlaceableId = string.IsNullOrWhiteSpace(definition.PlaceableId)
                ? $"BR_MediaAPI_Case_{definition.Id}"
                : definition.PlaceableId;
            definition.PlaceableDataFactory = () => GetPlaceableData(definition);
            definition.LoosePrefabFactory = () => CreateCase("MediaBox", definition);
            definition.HeldPrefabFactory = () => CreateCase("BookBox", definition) ?? CreateCase("MediaBox", definition);
            GameObject shelfPrefab = FindPrefab("MediaBoxShelf");
            definition.ShelfFactory = PrefabShelfFactory.Create(definition, shelfPrefab, "Cover");
        }

        internal static void Unload()
        {
            if (bundle != null && ownsBundle) bundle.Unload(false);
            bundle = null;
            ownsBundle = false;
            Placeables.Clear();
        }

        private static PlaceableData GetPlaceableData(MediaTypeDefinition definition)
        {
            if (Placeables.TryGetValue(definition.Id, out PlaceableData data)) return data;
            data = ScriptableObject.CreateInstance<PlaceableData>();
            data.ID = definition.PlaceableId;
            data.DisplayName = definition.DisplayName;
            data.PlacementType = PlacementType.Prop;
            data.SetToolType(EToolType.Placeable);
            data.IsLoadedFromMod = false;
            Placeables.Add(definition.Id, data);
            return data;
        }

        private static GameObject CreateCase(string prefabName, MediaTypeDefinition definition)
        {
            GameObject prefab = FindPrefab(prefabName);
            if (prefab == null) return null;
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = $"{definition.DisplayName} Case";
            if (instance.GetComponent<GenericMediaCaseProp>() == null) instance.AddComponent<GenericMediaCaseProp>();
            instance.SetActive(true);
            return instance;
        }

        private static GameObject FindPrefab(string name)
        {
            if (bundle == null) return null;
            GameObject prefab = bundle.LoadAsset<GameObject>(name);
            if (prefab != null) return prefab;
            foreach (string assetName in bundle.GetAllAssetNames())
                if (assetName.EndsWith("/" + name + ".prefab", StringComparison.OrdinalIgnoreCase))
                    return bundle.LoadAsset<GameObject>(assetName);
            MelonLogger.Error($"BR-MediaAPI prefab '{name}' was not found in {BundleName}.");
            return null;
        }
    }

    /// <summary>Generic saveable loose case used by the shared Boxroom-Studios prefab set.</summary>
    public sealed class GenericMediaCaseProp : MonoBehaviour, ICustomMediaProp
    {
        [SerializeField] private int mediaType;
        [SerializeField] private string mediaId = string.Empty;
        private IMediaItem item;
        private PlaceableSaveState saveState;
        private Texture2D coverTexture;
        private MediaRef reservedRef;
        private bool restoringPlacedItem;
        public IMediaItem MediaItem { get { Restore(); return item; } }
        public PlaceableSaveState SaveState => saveState;

        private void Awake() => PlayerInteractionTool.SpawnedMediaDemanded += OnSpawnedMediaDemanded;
        private void OnDestroy()
        {
            PlayerInteractionTool.SpawnedMediaDemanded -= OnSpawnedMediaDemanded;
            ClearReservation();
            if (coverTexture != null) Destroy(coverTexture);
        }

        public void ApplyMedia(IMediaItem value)
        {
            item = value;
            if (item == null) return;
            mediaType = (int)item.Ref.Type;
            mediaId = item.Ref.Id;
            item.IsSpawned = true;
            item.IsInHand = true;
            saveState ??= new PlaceableSaveState(string.Empty, transform.position, transform.rotation);
            saveState.CustomData = JsonUtility.ToJson(new CaseSaveData { MediaType = mediaType, MediaId = mediaId });
            ApplyCover();
        }

        public void PopulateFromLoad(PlaceableSaveState state)
        {
            saveState = state;
            CaseSaveData data = JsonUtility.FromJson<CaseSaveData>(state?.CustomData ?? string.Empty);
            if (data == null) return;
            mediaType = data.MediaType;
            mediaId = data.MediaId;
            restoringPlacedItem = true;
            reservedRef = new MediaRef((eMediaType)mediaType, mediaId);
            if (reservedRef.IsValid()) PlaceableMediaContainer.ReserveMedia(reservedRef);
            Restore();
            ApplyCover();
        }

        public void LinkSaveState(PlaceableSaveState state) => saveState = state;
        public void RefreshSaveStateReference(PlaceableSaveState state) => saveState = state;
        public void OnPlaced() { Restore(); if (item != null) { item.IsSpawned = true; item.IsInHand = false; ApplyCover(); } }
        public void OnPickedUp() { Restore(); if (item != null) item.IsInHand = true; }
        public void OnDeleted()
        {
            Restore();
            if (item != null) { item.IsSpawned = false; item.IsInHand = false; }
            MediaApi.NotifyMediaAvailabilityChanged();
        }

        private void Restore()
        {
            if (item == null && !string.IsNullOrWhiteSpace(mediaId) &&
                MediaApi.TryGet((eMediaType)mediaType, out MediaTypeDefinition definition))
                item = definition.Library.GetItemSync(new MediaRef((eMediaType)mediaType, mediaId));

            if (item != null && restoringPlacedItem)
            {
                item.IsSpawned = true;
                item.IsInHand = false;
                restoringPlacedItem = false;
                ClearReservation();
            }
        }

        private void ClearReservation()
        {
            if (!reservedRef.IsValid()) return;
            PlaceableMediaContainer.UnreserveMedia(reservedRef);
            reservedRef = default;
        }

        private void ApplyCover()
        {
            Restore();
            if (item == null) return;
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                if (text != null) text.text = item.DisplayName;
            StandardMediaCaseVisual.ApplySpine(gameObject, item.DisplayName);
            if (item.CoverArtBytes == null || item.CoverArtBytes.Length == 0) return;
            Renderer renderer = FindCover(transform)?.GetComponent<Renderer>();
            renderer ??= GetComponentInChildren<Renderer>(true);
            if (renderer == null) return;
            if (coverTexture != null) Destroy(coverTexture);
            coverTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (coverTexture.LoadImage(item.CoverArtBytes)) StandardMediaCaseVisual.ApplyCover(renderer, coverTexture);
        }

        private void OnSpawnedMediaDemanded(MediaRef value)
        {
            Restore();
            if (item != null && value.Type == item.Ref.Type && value.Id == item.Ref.Id) Destroy(gameObject);
        }

        private static Transform FindCover(Transform root)
        {
            if (root == null) return null;
            if (string.Equals(root.name, "Cover", StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { Transform found = FindCover(root.GetChild(i)); if (found != null) return found; }
            return null;
        }

        [Serializable]
        private sealed class CaseSaveData { public int MediaType; public string MediaId; }
    }
}
