using SteamShelf;
using SteamShelf.Media;
using SteamShelf.Placeables;
using SteamShelf.Tweening;
using System;
using TMPro;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Ready-made shelf adapter for a prefab. It applies title and cover automatically.</summary>
    public sealed class PrefabShelfItem : MonoBehaviour, IShelfItem
    {
        private MediaTypeDefinition definition;
        private IMediaItem item;
        private Renderer coverRenderer;
        private TMP_Text[] labels = Array.Empty<TMP_Text>();
        private Renderer[] renderers = Array.Empty<Renderer>();
        private Texture2D coverTexture;
        private Tweener tweener;
        private bool fullDisplay = true;

        public bool HasItemAndIsActive => item != null && fullDisplay && gameObject.activeInHierarchy;
        public IMediaItem ItemInfo => item;

        public void Initialize(MediaTypeDefinition owner, Renderer cover)
        {
            definition = owner;
            coverRenderer = cover;
            labels = GetComponentsInChildren<TMP_Text>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            tweener = GetComponent<Tweener>();
            Clear();
        }

        public void SetItem(IMediaItem value, bool playTween = false)
        {
            if (value == null || value.Ref.Type != definition.MediaType || !definition.ModelType.IsInstanceOfType(value)) { Clear(); return; }
            item = value;
            foreach (TMP_Text label in labels) if (label != null) label.text = value.DisplayName;
            ApplyCover(value.CoverArtBytes);
            StandardMediaCaseVisual.ApplySpine(gameObject, value.DisplayName);
            if (playTween && tweener != null) tweener.Play();
        }

        public void SetItem(MediaRef mediaRef)
        {
            SetItem(mediaRef.Type == definition.MediaType ? definition.Library.GetItemSync(mediaRef) : null);
        }

        public void Clear()
        {
            item = null;
            ReleaseCover();
            foreach (TMP_Text label in labels) if (label != null) label.text = string.Empty;
        }

        public void HideCoverArt() { if (coverRenderer != null) coverRenderer.enabled = false; }
        public void ShowCoverArt() { if (coverRenderer != null) coverRenderer.enabled = true; }
        public void SetFullDisplayActive(bool active)
        {
            fullDisplay = active;
            foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = active;
            foreach (TMP_Text label in labels) if (label != null) label.enabled = active;
        }
        public void SetHoveredDisplayActive(bool active) { }

        private void ApplyCover(byte[] bytes)
        {
            ReleaseCover();
            if (coverRenderer == null || bytes == null || bytes.Length == 0) return;
            coverTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!coverTexture.LoadImage(bytes)) { ReleaseCover(); return; }
            StandardMediaCaseVisual.ApplyCover(coverRenderer, coverTexture);
        }

        private void ReleaseCover()
        {
            if (coverTexture == null) return;
            Destroy(coverTexture);
            coverTexture = null;
        }

        private void OnDestroy() => ReleaseCover();
    }

    public static class PrefabShelfFactory
    {
        public static Func<Transform, IShelfItem> Create(MediaTypeDefinition definition, GameObject prefab, string coverChildName = "Cover")
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            return parent =>
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
                instance.SetActive(true);
                Renderer cover = Find(instance.transform, coverChildName)?.GetComponent<Renderer>();
                PrefabShelfItem adapter = instance.GetComponent<PrefabShelfItem>() ?? instance.AddComponent<PrefabShelfItem>();
                adapter.Initialize(definition, cover);
                return adapter;
            };
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { Transform found = Find(root.GetChild(i), name); if (found != null) return found; }
            return null;
        }
    }
}
