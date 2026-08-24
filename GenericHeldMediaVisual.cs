using HarmonyLib;
using SteamShelf.Media;
using SteamShelf.PlayerTools;
using System.Linq;
using UnityEngine;

namespace BR_MediaAPI
{
    internal static class GenericHeldMediaVisual
    {
        private static GameObject holder;
        private static IMediaItem currentItem;
        private static MediaTypeDefinition currentDefinition;

        internal static void Show(PlayerInteractionTool tool, IMediaItem item, MediaTypeDefinition definition)
        {
            Clear();
            if (tool == null || item == null || definition?.HeldPrefabFactory == null) return;
            currentItem = item;
            currentDefinition = definition;
            Transform parent = Camera.main != null
                ? Camera.main.transform
                : AccessTools.Field(typeof(PlayerInteractionTool), "mediaStageHolder")?.GetValue(tool) as Transform;
            if (parent == null) return;

            holder = new GameObject($"BR-MediaAPI Held {definition.DisplayName}");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(0.24f, -0.19f, 0.36f);
            holder.transform.localRotation = Quaternion.Euler(5f, -10f, -5f);

            GameObject visual = definition.CreateHeldPrefab();
            if (visual == null) { Clear(); return; }
            visual.transform.SetParent(holder.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.8f;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            visual.GetComponents<MonoBehaviour>().OfType<ICustomMediaProp>().FirstOrDefault()?.ApplyMedia(item);
            definition.Visuals?.OnHeld?.Invoke(new MediaVisualContext(item, visual));
            visual.SetActive(true);
        }

        internal static void Clear()
        {
            if (holder != null) UnityEngine.Object.Destroy(holder);
            holder = null;
            currentItem = null;
            currentDefinition = null;
        }

        internal static void Hide()
        {
            if (holder != null) holder.SetActive(false);
        }

        internal static void ShowCurrent(PlayerInteractionTool tool)
        {
            if (currentItem != null && currentDefinition != null) Show(tool, currentItem, currentDefinition);
        }
    }

    [HarmonyPatch(typeof(PlayerInteractionTool), "ClearMediaInHand")]
    internal static class ClearGenericHeldMediaVisualPatch
    {
        private static void Postfix() => GenericHeldMediaVisual.Clear();
    }

    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnToolDeactivated))]
    internal static class HideGenericHeldMediaVisualPatch
    {
        private static void Postfix() => GenericHeldMediaVisual.Hide();
    }

    [HarmonyPatch(typeof(PlayerInteractionTool), nameof(PlayerInteractionTool.OnToolActivated))]
    internal static class RestoreGenericHeldMediaVisualPatch
    {
        private static void Postfix(PlayerInteractionTool __instance) => GenericHeldMediaVisual.ShowCurrent(__instance);
    }

}
