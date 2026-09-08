using HarmonyLib;
using MelonLoader;
using SteamShelf;
using SteamShelf.Items;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace BR_MediaAPI
{
    internal static class UnplacedMediaBoxSystem
    {
        private const string AlbumBoxId = "Placeable_UnplacedAlbumBox";
        private static readonly Dictionary<string, MediaTypeDefinition> Definitions =
            new Dictionary<string, MediaTypeDefinition>(StringComparer.Ordinal);
        private static readonly Dictionary<int, PlaceableData> BoxData = new Dictionary<int, PlaceableData>();
        private static PlaceableData albumTemplate;

        internal static void TryRegister(MediaTypeDefinition definition)
        {
            if (definition == null || !definition.CreateUnplacedMediaBox || !Singleton<PlaceableManager>.HasInstance()) return;
            PlaceableManager manager = Singleton<PlaceableManager>.Instance;
            if (BoxData.ContainsKey(definition.Id)) return;

            albumTemplate ??= manager.GetDataByID(AlbumBoxId);
            if (albumTemplate == null) return;

            string boxId = definition.SourceBoxId;
            PlaceableData existing = manager.GetDataByID(boxId);
            if (existing != null)
            {
                Definitions[boxId] = definition;
                BoxData[definition.Id] = existing;
                return;
            }

            PlaceableData data = ScriptableObject.CreateInstance<PlaceableData>();
            data.ID = boxId;
            data.DisplayName = definition.SourceBoxName;
            data.DisplayDescription = definition.SourceBoxDescription;
            data.PlacementType = albumTemplate.PlacementType;
            data.SetToolType(EToolType.Placeable);
            data.IsLoadedFromMod = true;
            data.ModScale = 1f;

            var mods = AccessTools.Field(typeof(PlaceableManager), "modsPlaceables")?.GetValue(manager) as List<PlaceableData>;
            if (mods == null)
            {
                MelonLogger.Error($"Could not register {definition.SourceBoxName}: mod catalogue list is unavailable.");
                return;
            }

            Definitions.Add(boxId, definition);
            BoxData.Add(definition.Id, data);
            mods.Insert(0, data);
            manager.AllPlaceables.Insert(0, data);
        }

        internal static bool TryGetByBoxId(string id, out MediaTypeDefinition definition) =>
            Definitions.TryGetValue(id ?? string.Empty, out definition);

        internal static async Task<GameObject> Instantiate(MediaTypeDefinition definition)
        {
            if (albumTemplate == null || !BoxData.TryGetValue(definition.Id, out PlaceableData data)) return null;
            GameObject instance = await Singleton<PlaceableManager>.Instance.InstantiatePlaceableAsync(albumTemplate);
            if (instance == null) return null;
            foreach (UnplacedAlbumsBox albums in instance.GetComponentsInChildren<UnplacedAlbumsBox>(true))
                UnityEngine.Object.DestroyImmediate(albums);
            GenericUnplacedMediaBox box = instance.AddComponent<GenericUnplacedMediaBox>();
            box.SetDefinition(definition);
            if (instance.GetComponent<UnplacedMediaBoxPlacementRelay>() == null)
                instance.AddComponent<UnplacedMediaBoxPlacementRelay>();
            instance.name = definition.SourceBoxName;
            PlacementTag tag = instance.GetComponent<PlacementTag>();
            if (tag != null) tag.PlaceableData = data;
            return instance;
        }

        internal static Task<Sprite> GetSprite() => albumTemplate != null
            ? Singleton<PlaceableManager>.Instance.GetSpriteAsync(albumTemplate)
            : Task.FromResult<Sprite>(null);
    }

    /// <summary>Native source box that automatically displays every available item from one custom library.</summary>
    public sealed class GenericUnplacedMediaBox : MonoBehaviour, IPlaceable
    {
        private MediaTypeDefinition definition;
        private PlaceableSaveState saveState;
        private PlaceableMediaContainer[] containers;
        private bool initialized;
        private bool canFill;
        public PlaceableSaveState SaveState => saveState;
        internal MediaTypeDefinition Definition { get { ResolveDefinition(); return definition; } }

        internal void SetDefinition(MediaTypeDefinition owner)
        {
            definition = owner;
        }

        private void Awake() => containers = GetComponentsInChildren<PlaceableMediaContainer>(true);
        private void OnDestroy()
        {
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(Fill));
            MediaApi.MediaAvailabilityChanged -= Fill;
        }

        public void OnPlaced()
        {
            ResolveDefinition();
            InitializeContainers();
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(Fill));
            Shelf.OnShelfPutAway = (Action)Delegate.Combine(Shelf.OnShelfPutAway, new Action(Fill));
            MediaApi.MediaAvailabilityChanged -= Fill;
            MediaApi.MediaAvailabilityChanged += Fill;
        }

        public void OnPickedUp()
        {
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(Fill));
            MediaApi.MediaAvailabilityChanged -= Fill;
            Clear();
        }

        public void OnDeleted() => Clear();

        public void PopulateFromLoad(PlaceableSaveState state)
        {
            saveState = state;
            ResolveDefinition();
            InitializeContainers();
            Shelf.OnShelfPutAway = (Action)Delegate.Remove(Shelf.OnShelfPutAway, new Action(Fill));
            Shelf.OnShelfPutAway = (Action)Delegate.Combine(Shelf.OnShelfPutAway, new Action(Fill));
            MediaApi.MediaAvailabilityChanged -= Fill;
            MediaApi.MediaAvailabilityChanged += Fill;
        }

        public void LinkSaveState(PlaceableSaveState state) => saveState = state;
        public void RefreshSaveStateReference(PlaceableSaveState state) => saveState = state;

        private void ResolveDefinition()
        {
            if (definition != null) return;
            PlacementTag tag = GetComponent<PlacementTag>();
            UnplacedMediaBoxSystem.TryGetByBoxId(tag?.PlaceableData?.ID, out definition);
        }

        private void InitializeContainers()
        {
            if (initialized || definition == null) return;
            containers = GetComponentsInChildren<PlaceableMediaContainer>(true);
            if (containers.Length == 0)
            {
                MelonLogger.Error($"{definition.SourceBoxName} contains no PlaceableMediaContainer components.");
                return;
            }
            foreach (PlaceableMediaContainer container in containers)
            {
                container.Initialise();
                container.OnItemPickedFromSlot = (Action<PlaceableMediaContainer, int>)Delegate.Combine(
                    container.OnItemPickedFromSlot,
                    new Action<PlaceableMediaContainer, int>((_, _) => Fill()));
            }
            initialized = true;
            MelonLogger.Msg($"{definition.SourceBoxName} initialized with {containers.Length} media container(s).");
            StartCoroutine(WaitForRoomThenFill());
        }

        private IEnumerator WaitForRoomThenFill()
        {
            while (Singleton<RoomDataManager>.Instance.IsSpawningRoom) yield return null;
            canFill = true;
            Fill();
        }

        private void Fill()
        {
            if (!canFill || definition == null) return;
            int available = 0;
            int placed = 0;
            foreach (IMediaItem item in definition.Library.GetKnownItems().OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                if (item == null || item.IsSpawned || item.IsInHand || !item.IsFullyLoaded) continue;
                if (PlaceableMediaContainer.IsMediaReserved(item.Ref)) continue;
                available++;
                if (TryPlace(item)) { placed++; continue; }
                break;
            }
            MelonLogger.Msg($"{definition.SourceBoxName} fill: {available} available, {placed} placed.");
        }

        private bool TryPlace(IMediaItem item)
        {
            foreach (PlaceableMediaContainer container in containers)
                for (int slot = 0; slot < container.MaxGameCount; slot++)
                {
                    if (container.IsItemInSlot(slot)) continue;
                    container.PlaceItem(item, slot, playPlacedTween: false);
                    return true;
                }
            return false;
        }

        private void Clear()
        {
            if (containers == null) return;
            foreach (PlaceableMediaContainer container in containers) container.ClearAll();
        }
    }

    /// <summary>Reconnects the cloned native box's placement event to the API replacement component.</summary>
    internal sealed class UnplacedMediaBoxPlacementRelay : MonoBehaviour
    {
        private UnityEvent placedEvent;
        private UnityAction listener;

        private void Awake()
        {
            PlacementTag tag = GetComponent<PlacementTag>();
            GenericUnplacedMediaBox box = GetComponent<GenericUnplacedMediaBox>();
            placedEvent = AccessTools.Field(typeof(PlacementTag), "onPlaced")?.GetValue(tag) as UnityEvent;
            if (placedEvent == null || box == null) return;
            listener = box.OnPlaced;
            placedEvent.AddListener(listener);
        }

        private void OnDestroy()
        {
            if (placedEvent != null && listener != null) placedEvent.RemoveListener(listener);
        }
    }
}
