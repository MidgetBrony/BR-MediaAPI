using HarmonyLib;
using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BR_MediaAPI
{
    internal static class NativeMediaVisualOverrides
    {
        internal static void Apply(Component target, IMediaItem item, MediaVisualUsage usage)
        {
            if (target == null) return;
            NativeMediaVisualHost host = target.GetComponent<NativeMediaVisualHost>();
            if (!MediaApi.TryGetVisualOverride(item, out MediaVisualOverrideDefinition definition))
            {
                host?.Restore();
                return;
            }
            if (host == null) host = target.gameObject.AddComponent<NativeMediaVisualHost>();
            host.Apply(item, usage, definition);
        }

        internal static void RefreshAll()
        {
            foreach (ShelfBox shelf in Object.FindObjectsByType<ShelfBox>(FindObjectsSortMode.None))
                if (shelf != null) Apply(shelf, shelf.gameInfo, MediaVisualUsage.Shelf);
            foreach (PlacedBoxProp loose in Object.FindObjectsByType<PlacedBoxProp>(FindObjectsSortMode.None))
                if (loose != null) Apply(loose, loose.GameData, MediaVisualUsage.Loose);

            PlayerInteractionTool tool = Object.FindFirstObjectByType<PlayerInteractionTool>();
            if (tool?.CurrentHeldSteamGameData != null)
            {
                Box handBox = AccessTools.Field(typeof(PlayerInteractionTool), "inHandGameBox")?.GetValue(tool) as Box;
                Apply(handBox, tool.CurrentHeldSteamGameData, MediaVisualUsage.Held);
            }
            BoxInspector inspector = Object.FindFirstObjectByType<BoxInspector>();
            if (inspector?.Box != null) Apply(inspector.Box, inspector.heldMediaInfo, MediaVisualUsage.Inspect);
        }

        internal static void Remove(string key)
        {
            foreach (NativeMediaVisualHost host in Object.FindObjectsByType<NativeMediaVisualHost>(FindObjectsSortMode.None))
                if (host != null && string.Equals(host.OverrideKey, key, System.StringComparison.OrdinalIgnoreCase)) host.Restore();
        }

        internal static void RestoreAll()
        {
            foreach (NativeMediaVisualHost host in Object.FindObjectsByType<NativeMediaVisualHost>(FindObjectsSortMode.None))
                host?.Restore();
        }
    }

    internal sealed class NativeMediaVisualHost : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> originals = new Dictionary<Renderer, bool>();
        private GameObject visual;
        private string mediaId;
        private MediaVisualUsage usage;
        internal string OverrideKey { get; private set; }

        internal void Apply(IMediaItem item, MediaVisualUsage newUsage, MediaVisualOverrideDefinition definition)
        {
            string newMediaId = item == null ? string.Empty : $"{(int)item.Ref.Type}:{item.Ref.Id}";
            if (visual != null && OverrideKey == definition.Key && mediaId == newMediaId && usage == newUsage)
            {
                HideOriginals();
                return;
            }

            Restore();
            CaptureOriginals();
            OverrideKey = definition.Key;
            mediaId = newMediaId;
            usage = newUsage;
            Vector3 anchor = FindAnchorCenter();
            Bounds anchorWorldBounds = FindAnchorWorldBounds();
            Bounds anchorBounds = anchorWorldBounds.size.sqrMagnitude > 0.0000001f
                ? BoundsInLocal(transform, anchorWorldBounds)
                : FindAnchorBounds(anchor);
            try
            {
                visual = definition.CreateVisual(new MediaVisualOverrideContext(item, usage, this, anchor, anchorBounds, anchorWorldBounds));
                if (visual == null) { Restore(); return; }
                if (visual.transform.parent != transform) visual.transform.SetParent(transform, false);
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                HideOriginals();
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Media visual override '{definition.Key}' failed: {ex}");
                Restore();
            }
        }

        internal void Restore()
        {
            foreach (KeyValuePair<Renderer, bool> entry in originals)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            originals.Clear();
            if (visual != null) Object.Destroy(visual);
            visual = null;
            OverrideKey = null;
            mediaId = null;
        }

        private void LateUpdate()
        {
            if (visual != null) HideOriginals();
        }

        private void CaptureOriginals()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer != null) originals[renderer] = renderer.enabled;
        }

        private void HideOriginals()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || visual != null && renderer.transform.IsChildOf(visual.transform)) continue;
                if (!originals.ContainsKey(renderer)) originals[renderer] = renderer.enabled;
                renderer.enabled = false;
            }
        }

        private Vector3 FindAnchorCenter()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) return box.center;
            Renderer largest = originals.Keys.Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude).FirstOrDefault();
            return largest == null ? Vector3.zero : BoundsInLocal(transform, largest.bounds).center;
        }

        private Bounds FindAnchorBounds(Vector3 fallbackCenter)
        {
            Renderer largest = originals.Keys
                .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude)
                .FirstOrDefault();
            if (largest != null) return BoundsInLocal(transform, largest.bounds);

            BoxCollider box = GetComponent<BoxCollider>();
            return box != null ? new Bounds(box.center, box.size) : new Bounds(fallbackCenter, Vector3.zero);
        }

        private Bounds FindAnchorWorldBounds()
        {
            ShelfBox shelf = GetComponent<ShelfBox>();
            if (shelf?.rend != null) return shelf.rend.bounds;

            Renderer largest = originals.Keys
                .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude)
                .FirstOrDefault();
            return largest == null ? default : largest.bounds;
        }

        private static Bounds BoundsInLocal(Transform parent, Bounds world)
        {
            bool found = false;
            Bounds result = default;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = parent.InverseTransformPoint(world.center + Vector3.Scale(world.extents, new Vector3(x, y, z)));
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                else result.Encapsulate(point);
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(ShelfBox), "HandleMetadataReady")]
    internal static class NativeShelfVisualPatch
    {
        private static void Postfix(ShelfBox __instance, SteamGameData game) =>
            NativeMediaVisualOverrides.Apply(__instance, game, MediaVisualUsage.Shelf);
    }

    [HarmonyPatch(typeof(ShelfBox), nameof(ShelfBox.ClearGame))]
    internal static class ClearNativeShelfVisualPatch
    {
        private static void Prefix(ShelfBox __instance) => __instance?.GetComponent<NativeMediaVisualHost>()?.Restore();
    }

    [HarmonyPatch(typeof(PlacedBoxProp), "HandleMetadataReady")]
    internal static class NativeLooseVisualPatch
    {
        private static void Postfix(PlacedBoxProp __instance, SteamGameData game) =>
            NativeMediaVisualOverrides.Apply(__instance, game, MediaVisualUsage.Loose);
    }

    [HarmonyPatch(typeof(PlayerInteractionTool), "ShowInHandGameBox")]
    internal static class NativeHeldVisualPatch
    {
        private static void Postfix(PlayerInteractionTool __instance, SteamGameData game)
        {
            Box handBox = AccessTools.Field(typeof(PlayerInteractionTool), "inHandGameBox")?.GetValue(__instance) as Box;
            NativeMediaVisualOverrides.Apply(handBox, game, MediaVisualUsage.Held);
        }
    }

    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.SetHeldMedia))]
    [HarmonyPriority(Priority.Last)]
    internal static class NativeInspectVisualPatch
    {
        private static void Postfix(BoxInspector __instance, IMediaItem item) =>
            NativeMediaVisualOverrides.Apply(__instance?.Box, item, MediaVisualUsage.Inspect);
    }

    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnToolActivated))]
    [HarmonyPriority(Priority.Last)]
    internal static class ActivateNativeInspectVisualPatch
    {
        private static void Postfix(BoxInspector __instance)
        {
            if (__instance?.Box == null || __instance.heldMediaInfo == null) return;
            if (!MediaApi.TryGetVisualOverride(__instance.heldMediaInfo, out _)) return;
            __instance.Box.SetBoxShowing(true);
            NativeMediaVisualOverrides.Apply(__instance.Box, __instance.heldMediaInfo, MediaVisualUsage.Inspect);
            Camera camera = Camera.main;
            if (__instance.BoxHolder != null && camera != null)
            {
                __instance.BoxHolder.position = camera.transform.TransformPoint(new Vector3(0f, 0f, 0.50f));
                __instance.BoxHolder.rotation = camera.transform.rotation;
            }
        }
    }

    [HarmonyPatch(typeof(ObjectPlacer), nameof(ObjectPlacer.Begin))]
    internal static class NativePlacementPreviewVisualPatch
    {
        private static void Postfix(ObjectPlacer __instance, GameObject prefab)
        {
            PlacedBoxProp source = prefab?.GetComponent<PlacedBoxProp>();
            GameObject preview = AccessTools.Field(typeof(ObjectPlacer), "preview")?.GetValue(__instance) as GameObject;
            PlacedBoxProp target = preview?.GetComponent<PlacedBoxProp>();
            if (source?.GameData == null || target == null) return;
            target.GetComponent<NativeMediaVisualHost>()?.Restore();
            NativeMediaVisualOverrides.Apply(target, source.GameData, MediaVisualUsage.Loose);
        }
    }

    [HarmonyPatch(typeof(ObjectPlacer), nameof(ObjectPlacer.CommitPlacement))]
    internal static class NativeCommittedPlacementVisualPatch
    {
        private static void Postfix(ref GameObject __result)
        {
            PlacedBoxProp target = __result?.GetComponent<PlacedBoxProp>();
            if (target?.GameData == null) return;
            target.GetComponent<NativeMediaVisualHost>()?.Restore();
            NativeMediaVisualOverrides.Apply(target, target.GameData, MediaVisualUsage.Loose);
        }
    }
}
