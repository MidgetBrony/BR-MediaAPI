using HarmonyLib;
using MelonLoader;
using SteamShelf.Placeables;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace BR_MediaAPI
{
    internal static class LooseMediaPrefabSetup
    {
        internal static void Configure(GameObject instance, MediaTypeDefinition definition)
        {
            if (instance == null || definition == null) return;
            PlacementTag tag = instance.GetComponent<PlacementTag>() ?? instance.AddComponent<PlacementTag>();
            tag.PlaceableData = definition.GetPlaceableData();
            try { instance.tag = "Prop"; }
            catch (Exception ex) { MelonLogger.Warning($"Could not apply BOXROOM's Prop tag to {definition.DisplayName}: {ex.Message}"); }

            if (instance.GetComponentsInChildren<Collider>(true).Length == 0) instance.AddComponent<BoxCollider>();
            int propLayer = LayerMask.NameToLayer("Prop");
            if (propLayer >= 0)
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = propLayer;

            PlaceablePainter painter = instance.GetComponent<PlaceablePainter>() ?? instance.AddComponent<PlaceablePainter>();
            painter.InitializeMaterials();
            painter.SetupAsForcedPlaceable();
            if (instance.GetComponent<LooseMediaPlacementRelay>() == null) instance.AddComponent<LooseMediaPlacementRelay>();
        }
    }

    /// <summary>Recreates the OnPlaced UnityEvent wiring normally authored into BOXROOM prefabs.</summary>
    internal sealed class LooseMediaPlacementRelay : MonoBehaviour
    {
        private PlacementTag placementTag;
        private ICustomMediaProp prop;
        private UnityEvent placedEvent;
        private UnityAction listener;

        private void Awake()
        {
            placementTag = GetComponent<PlacementTag>();
            prop = GetComponents<MonoBehaviour>().OfType<ICustomMediaProp>().FirstOrDefault();
            placedEvent = AccessTools.Field(typeof(PlacementTag), "onPlaced")?.GetValue(placementTag) as UnityEvent;
            if (placedEvent == null || prop == null) return;
            listener = prop.OnPlaced;
            placedEvent.AddListener(listener);
        }

        private void OnDestroy()
        {
            if (placedEvent != null && listener != null) placedEvent.RemoveListener(listener);
        }
    }
}
