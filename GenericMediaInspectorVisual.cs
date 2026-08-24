using HarmonyLib;
using SteamShelf.Media;
using SteamShelf.PlayerTools;
using SteamShelf.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Provides BOXROOM's inspect-stage presentation for registered custom media.</summary>
    internal static class GenericMediaInspectorVisual
    {
        private static GameObject visual;
        private static MediaInspectContext context;
        private static MediaTypeDefinition currentDefinition;
        private static readonly Dictionary<TMP_Text, string> ChangedLabels = new Dictionary<TMP_Text, string>();

        internal static void Show(BoxInspector inspector, IMediaItem item)
        {
            if (inspector?.BoxHolder == null || item == null ||
                !MediaApi.TryGet(item.Ref.Type, out MediaTypeDefinition definition) ||
                !definition.UseGenericInteractionLifecycle ||
                (definition.Inspect?.PrefabFactory == null && definition.HeldPrefabFactory == null)) return;

            ExitCurrent();
            if (visual != null) UnityEngine.Object.Destroy(visual);
            RestoreLabels();
            HideStockVisuals(inspector);
            visual = definition.Inspect?.PrefabFactory?.Invoke(item) ?? definition.CreateHeldPrefab();
            if (visual == null) return;

            visual.name = $"Inspected {definition.DisplayName}";
            ConfigureVisual(inspector, item, visual);
            currentDefinition = definition;
            context = new MediaInspectContext(inspector, item, visual, replacement => ReplaceVisual(inspector, item, replacement));
            definition.Visuals?.OnInspect?.Invoke(new MediaVisualContext(item, visual));

            InspectUIAnchors anchors = visual.GetComponentInChildren<InspectUIAnchors>(true);
            if (anchors != null)
                AccessTools.Property(typeof(BoxInspector), nameof(BoxInspector.ActiveUIAnchors))?.SetValue(inspector, anchors);
            definition.Inspect?.OnEnter?.Invoke(context);
            ApplyActionLabel();
        }

        internal static void PlaceInFront(BoxInspector inspector)
        {
            Camera camera = Camera.main;
            if (inspector?.BoxHolder == null || camera == null) return;
            inspector.BoxHolder.position = camera.transform.TransformPoint(new Vector3(0f, 0f, 0.50f));
            inspector.BoxHolder.rotation = camera.transform.rotation;
        }

        internal static void Hide()
        {
            ExitCurrent();
            if (visual != null) UnityEngine.Object.Destroy(visual);
            visual = null;
            context = null;
            currentDefinition = null;
            RestoreLabels();
        }

        internal static bool TryExecutePrimary(IMediaItem item)
        {
            if (context == null || currentDefinition?.Inspect?.OnPrimaryAction == null || item == null ||
                context.Item.Ref.Type != item.Ref.Type || context.Item.Ref.Id != item.Ref.Id) return false;
            currentDefinition.Inspect.OnPrimaryAction(context);
            return true;
        }

        internal static void Update()
        {
            if (context != null) currentDefinition?.Inspect?.OnUpdate?.Invoke(context);
        }

        internal static void ApplyActionLabel()
        {
            string label = currentDefinition?.Inspect?.PrimaryActionLabel;
            if (string.IsNullOrWhiteSpace(label)) return;
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || (!string.Equals(text.text, "Open", System.StringComparison.OrdinalIgnoreCase) &&
                                     !string.Equals(text.text, "Play", System.StringComparison.OrdinalIgnoreCase))) continue;
                if (!ChangedLabels.ContainsKey(text)) ChangedLabels.Add(text, text.text);
                text.text = label;
            }
        }

        private static void ReplaceVisual(BoxInspector inspector, IMediaItem item, GameObject replacement)
        {
            if (replacement == null) return;
            if (visual != null) UnityEngine.Object.Destroy(visual);
            visual = replacement;
            ConfigureVisual(inspector, item, visual);
            currentDefinition?.Visuals?.OnInspect?.Invoke(new MediaVisualContext(item, visual));
            if (context != null) context.Visual = visual;
        }

        private static void ConfigureVisual(BoxInspector inspector, IMediaItem item, GameObject instance)
        {
            instance.transform.SetParent(inspector.BoxHolder, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * 1.25f;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            instance.GetComponents<MonoBehaviour>().OfType<ICustomMediaProp>().FirstOrDefault()?.ApplyMedia(item);
            instance.SetActive(true);
        }

        private static void ExitCurrent()
        {
            if (context != null) currentDefinition?.Inspect?.OnExit?.Invoke(context);
        }

        private static void RestoreLabels()
        {
            foreach (KeyValuePair<TMP_Text, string> pair in ChangedLabels)
                if (pair.Key != null) pair.Key.text = pair.Value;
            ChangedLabels.Clear();
        }

        private static void HideStockVisuals(BoxInspector inspector)
        {
            inspector.Box?.SetBoxShowing(false);
            Component album = AccessTools.Field(typeof(BoxInspector), "albumBox")?.GetValue(inspector) as Component;
            if (album != null) AccessTools.Method(album.GetType(), "SetBoxShowing")?.Invoke(album, new object[] { false });
        }
    }

    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.SetHeldMedia))]
    internal static class GenericInspectorMediaPatch
    {
        private static void Postfix(BoxInspector __instance, IMediaItem item)
        {
            if (item != null && MediaApi.TryGet(item.Ref.Type, out MediaTypeDefinition definition) && definition.UseGenericInteractionLifecycle) GenericMediaInspectorVisual.Show(__instance, item);
            else GenericMediaInspectorVisual.Hide();
        }
    }

    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnToolActivated))]
    internal static class PositionGenericInspectorPatch
    {
        private static void Postfix(BoxInspector __instance)
        {
            IMediaItem item = __instance.heldMediaInfo;
            if (item == null || !MediaApi.TryGet(item.Ref.Type, out MediaTypeDefinition definition) || !definition.UseGenericInteractionLifecycle) return;
            GenericHeldMediaVisual.Hide();
            GenericMediaInspectorVisual.Show(__instance, item);
            GenericMediaInspectorVisual.PlaceInFront(__instance);
        }
    }

    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnToolDeactivated))]
    internal static class HideGenericInspectorPatch
    {
        private static void Postfix() => GenericMediaInspectorVisual.Hide();
    }

    [HarmonyPatch(typeof(BoxInspector), nameof(BoxInspector.OnUpdate))]
    internal static class UpdateGenericInspectorPatch
    {
        private static void Postfix() => GenericMediaInspectorVisual.Update();
    }

    [HarmonyPatch(typeof(Menu_Inspect), "OnPreShow")]
    internal static class LabelGenericInspectorActionPatch
    {
        private static void Postfix() => GenericMediaInspectorVisual.ApplyActionLabel();
    }
}
