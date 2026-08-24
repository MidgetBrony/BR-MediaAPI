using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Applies the standard BOXROOM case cover and runtime spine title.</summary>
    internal static class StandardMediaCaseVisual
    {
        private const string LabelName = "BRMediaSpineTitle";
        private static TMP_FontAsset cachedFont;
        private static Material cachedMaterial;

        internal static void ApplyCover(Renderer renderer, Texture2D texture)
        {
            if (renderer == null || texture == null) return;
            Material material = renderer.material;
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);

            // The stock cover quad faces outward with a 180-degree Y rotation.
            // Mirror U so cover lettering reads in the correct direction.
            material.mainTextureScale = new Vector2(-1f, 1f);
            material.mainTextureOffset = new Vector2(1f, 0f);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                material.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            }
            renderer.material = material;
        }

        internal static void ApplySpine(GameObject mediaCase, string title)
        {
            if (mediaCase == null) return;
            Transform spine = Find(mediaCase.transform, "Spine");
            if (spine == null || spine.parent == null || !TryGetBoxroomFont()) return;

            Transform existing = spine.parent.Find(LabelName);
            TextMeshPro label;
            if (existing == null)
            {
                GameObject labelObject = new GameObject(LabelName, typeof(RectTransform));
                labelObject.layer = spine.gameObject.layer;
                labelObject.transform.SetParent(spine.parent, false);
                label = labelObject.AddComponent<TextMeshPro>();
            }
            else
            {
                label = existing.GetComponent<TextMeshPro>() ?? existing.gameObject.AddComponent<TextMeshPro>();
            }

            RectTransform rect = label.rectTransform;
            rect.localPosition = new Vector3(-0.0638f, 0f, 0f);
            rect.localRotation = Quaternion.Euler(0f, 90f, -90f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(0.158f, 0.025584f);

            label.font = cachedFont;
            label.fontSharedMaterial = cachedMaterial;
            label.text = string.IsNullOrWhiteSpace(title) ? "Untitled" : title;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = 0.46f;
            label.color = Color.white;
            label.raycastTarget = false;
            label.ForceMeshUpdate(true, true);
        }

        private static bool TryGetBoxroomFont()
        {
            if (cachedFont != null && cachedMaterial != null) return true;
            TMP_Text template = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null);
            template ??= Resources.FindObjectsOfTypeAll<TMP_Text>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null &&
                                        !text.name.Equals(LabelName, StringComparison.Ordinal));
            if (template == null) return false;
            cachedFont = template.font;
            cachedMaterial = template.fontSharedMaterial;
            return cachedFont != null && cachedMaterial != null;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
