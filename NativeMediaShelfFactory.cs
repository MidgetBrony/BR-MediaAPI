using HarmonyLib;
using SteamShelf.Media;
using SteamShelf.Placeables;
using System;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Adapts BOXROOM's native album shelf shell to a custom media model.</summary>
    public static class NativeMediaShelfFactory
    {
        public static Func<Transform, IShelfItem> Create(MediaTypeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return parent =>
            {
                IShelfItem nativeItem = ShelfItemFactory.Create(eMediaType.CDAlbum, parent);
                Component nativeComponent = nativeItem as Component;
                if (nativeComponent == null) return null;
                Renderer renderer = AccessTools.Field(nativeComponent.GetType(), "rend")?.GetValue(nativeComponent) as Renderer;
                renderer ??= nativeComponent.GetComponentInChildren<Renderer>(true);
                GameObject instance = nativeComponent.gameObject;
                PrefabShelfItem adapter = instance.GetComponent<PrefabShelfItem>() ?? instance.AddComponent<PrefabShelfItem>();
                adapter.Initialize(definition, renderer);
                UnityEngine.Object.DestroyImmediate(nativeComponent);
                return adapter;
            };
        }
    }
}
