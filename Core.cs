using HarmonyLib;
using MelonLoader;
using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.Save;
using SteamShelf.UI;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(typeof(BR_MediaAPI.Core), "BR-MediaAPI", "1.0.0", "Rusty", null)]
[assembly: MelonGame("NestedLoop", "BOXROOM")]
[assembly: MelonAdditionalDependencies("ModsPanel")]

namespace BR_MediaAPI
{
    public sealed class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MediaApi.SetLogger(LoggerInstance);
            SharedMediaCasePrefabs.Load();
            PlaceableManager.PlaceableDataLoaded += MediaApi.RegisterSourceBoxes;
            LoggerInstance.Msg("Ready. Custom media types may now register through MediaApi.Register().");
        }

        public override void OnDeinitializeMelon()
        {
            PlaceableManager.PlaceableDataLoaded -= MediaApi.RegisterSourceBoxes;
            SharedMediaCasePrefabs.Unload();
        }
    }

    [HarmonyPatch(typeof(MediaBootstrap), "Initialize")]
    internal static class RegisterLibrariesPatch
    {
        private static void Postfix() => MediaApi.AttachRegisteredLibraries();
    }

    [HarmonyPatch(typeof(SteamLibrarySystem), "Configure")]
    internal static class LoadRegisteredMediaCachesPatch
    {
        private static void Postfix() => MediaApi.LoadRegisteredLibraries();
    }

    [HarmonyPatch(typeof(ShelfItemFactory), nameof(ShelfItemFactory.Create))]
    internal static class CustomShelfFactoryPatch
    {
        private static bool Prefix(eMediaType type, Transform parent, ref IShelfItem __result)
        {
            if (!MediaApi.TryGet(type, out MediaTypeDefinition definition)) return true;
            __result = definition.CreateShelfItem(parent);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaceableMediaContainer), nameof(PlaceableMediaContainer.CanAccept), new[] { typeof(eMediaType) })]
    internal static class CustomMediaAcceptancePatch
    {
        private static bool Prefix(eMediaType type, ref bool __result)
        {
            if (!MediaApi.TryGet(type, out MediaTypeDefinition definition)) return true;
            __result = definition.AllowOnShelves;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.GetDataByID))]
    internal static class ResolveCustomPlaceablePatch
    {
        private static void Postfix(string id, ref PlaceableData __result)
        {
            if (__result == null && MediaApi.TryGetByPlaceableId(id, out MediaTypeDefinition definition))
                __result = definition.GetPlaceableData();
        }
    }

    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.InstantiatePlaceableAsync))]
    internal static class InstantiateCustomPlaceablePatch
    {
        private static bool Prefix(PlaceableData data, ref Task<GameObject> __result)
        {
            if (data == null || !MediaApi.TryGetByPlaceableId(data.ID, out MediaTypeDefinition definition)) return true;
            __result = Task.FromResult(definition.CreateLoosePrefab());
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.InstantiatePlaceableAsync))]
    internal static class InstantiateUnplacedMediaBoxPatch
    {
        private static bool Prefix(PlaceableData data, ref Task<GameObject> __result)
        {
            if (!UnplacedMediaBoxSystem.TryGetByBoxId(data?.ID, out MediaTypeDefinition definition)) return true;
            __result = UnplacedMediaBoxSystem.Instantiate(definition);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.GetSpriteAsync))]
    internal static class UnplacedMediaBoxSpritePatch
    {
        private static bool Prefix(PlaceableData data, ref Task<Sprite> __result)
        {
            if (!UnplacedMediaBoxSystem.TryGetByBoxId(data?.ID, out _)) return true;
            __result = UnplacedMediaBoxSystem.GetSprite();
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerInteractionTool), "OnMediaInHandChanged")]
    internal static class CustomShelfPickupPatch
    {
        private static bool Prefix(PlayerInteractionTool __instance, IMediaItem item)
        {
            if (item == null || !MediaApi.TryGet(item.Ref.Type, out MediaTypeDefinition definition) || !definition.UseGenericInteractionLifecycle || definition.LoosePrefabFactory == null) return true;
            GameObject instance = definition.CreateLoosePrefab();
            if (instance == null) return false;
            PlacementTag tag = instance.GetComponent<PlacementTag>();
            ICustomMediaProp prop = instance.GetComponents<MonoBehaviour>().OfType<ICustomMediaProp>().FirstOrDefault();
            if (tag == null || prop == null) { UnityEngine.Object.Destroy(instance); return false; }
            prop.ApplyMedia(item);
            AccessTools.Method(typeof(PlayerInteractionTool), "PickupMediaProp")?.Invoke(__instance, new object[] { tag, instance });
            GenericHeldMediaVisual.Show(__instance, item, definition);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerInteractionTool), "PickupItem", new[] { typeof(PlacementTag) })]
    internal static class CustomLoosePickupPatch
    {
        private static bool Prefix(PlayerInteractionTool __instance, PlacementTag placeable,
            ref GameObject ___heldPrefab, ref PlaceableSaveState ___heldSaveState,
            ref PlacementTag ___currentLookingAtPlaceable, ref IMediaItem ___currentHeldMediaItem)
        {
            ICustomMediaProp prop = placeable?.GetComponents<MonoBehaviour>().OfType<ICustomMediaProp>().FirstOrDefault();
            if (prop == null || prop.MediaItem == null || !MediaApi.TryGet(prop.MediaItem.Ref.Type, out MediaTypeDefinition definition) || !definition.UseGenericInteractionLifecycle) return true;

            ___heldSaveState = prop.SaveState;
            placeable.TryClearMeFromItemBelow();
            prop.OnPickedUp();
            if (placeable.TryGetComponent(out StackableSurface stackable)) stackable.TriggerStackCollapse();
            prop.MediaItem.IsInHand = true;
            ___currentHeldMediaItem = prop.MediaItem;
            ___heldPrefab = placeable.gameObject;
            ___heldPrefab.SetActive(false);

            object controller = AccessTools.Field(typeof(PlayerTool), "controller")?.GetValue(__instance);
            object registry = GetMember(controller, "Registry");
            object placer = GetMember(controller, "ObjectPlacer");
            object settings = AccessTools.Method(registry?.GetType(), "Get", new[] { typeof(PlacementType) })?.Invoke(registry, new object[] { placeable.PlacementType });
            Material valid = AccessTools.Field(typeof(PlayerInteractionTool), "validPreviewMaterial")?.GetValue(__instance) as Material;
            Material invalid = AccessTools.Field(typeof(PlayerInteractionTool), "invalidPreviewMaterial")?.GetValue(__instance) as Material;
            MethodInfo begin = AccessTools.Method(placer?.GetType(), "Begin");
            if (controller == null || registry == null || placer == null || settings == null || begin == null)
            {
                ___heldPrefab.SetActive(true); ___heldPrefab = null; ___heldSaveState = null; ___currentHeldMediaItem = null; prop.MediaItem.IsInHand = false;
                return false;
            }
            begin.Invoke(placer, new object[] { placeable.gameObject, settings, valid, invalid });
            ___currentLookingAtPlaceable = null;
            GenericHeldMediaVisual.Show(__instance, prop.MediaItem, definition);
            return false;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null) return null;
            return AccessTools.Property(instance.GetType(), name)?.GetValue(instance) ?? AccessTools.Field(instance.GetType(), name)?.GetValue(instance);
        }
    }

    [HarmonyPatch(typeof(Menu_Inspect), nameof(Menu_Inspect.OnClick_LaunchGame))]
    internal static class CustomOpenPatch
    {
        private static bool Prefix()
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            return tool?.CurrentHeldMediaItem == null || !MediaApi.TryOpen(tool.CurrentHeldMediaItem);
        }
    }

    [HarmonyPatch(typeof(Menu_Inspect), nameof(Menu_Inspect.OnClick_OpenButton))]
    internal static class CustomOpenButtonPatch
    {
        private static bool Prefix()
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            return tool?.CurrentHeldMediaItem == null || !MediaApi.TryOpen(tool.CurrentHeldMediaItem);
        }
    }
}
