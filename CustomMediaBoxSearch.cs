using HarmonyLib;
using SteamShelf;
using SteamShelf.ControlHints;
using SteamShelf.Input;
using SteamShelf.Media;
using SteamShelf.PlayerTools;
using SteamShelf.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BR_MediaAPI
{
    /// <summary>Adapts BOXROOM's native Game Box search menu to registered media source boxes.</summary>
    internal static class CustomMediaBoxSearch
    {
        private const int ResultCap = 50;
        private static readonly List<GameObject> CustomRows = new List<GameObject>();
        private static HintHandle hint;
        private static GenericUnplacedMediaBox lookedAtBox;
        internal static MediaTypeDefinition ActiveDefinition { get; private set; }

        internal static void Update()
        {
            PlayerInteractionTool tool = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTool>();
            GenericUnplacedMediaBox box = null;
            if (tool != null && !tool.IsHoldingProp && tool.LookingAtPlaceableTag != null)
                box = tool.LookingAtPlaceableTag.GetComponent<GenericUnplacedMediaBox>();

            if (box != lookedAtBox)
            {
                hint?.Dispose();
                hint = null;
                lookedAtBox = box;
                if (box != null)
                    hint = new HintHandle("BR-MediaAPI Search Box", new ControlHint
                    {
                        actionLabel = $"Search For {box.Definition?.DisplayName ?? "Media"}",
                        bindingKeys = new[] { "Secondary" },
                        priority = 3
                    });
            }

            PlayerInputContext input = Singleton<InputManager>.Instance?.CurrentPlayerInputContext;
            if (box != null && input != null && input.SecondaryPressedThisFrame)
            {
                ActiveDefinition = box.Definition;
                if (ActiveDefinition != null) Singleton<MenuManager>.Instance.OpenMenu("SearchGame");
            }
        }

        internal static bool IsActive => ActiveDefinition != null;

        internal static void Populate(Menu_SearchGame menu, string query)
        {
            if (menu == null || ActiveDefinition == null) return;
            ClearRows();
            RetrieveNativeRows(menu);

            Transform parent = AccessTools.Field(typeof(Menu_SearchGame), "searchResultParent")?.GetValue(menu) as Transform;
            GameSearchButton prefab = AccessTools.Field(typeof(Menu_SearchGame), "searchResultPrefab")?.GetValue(menu) as GameSearchButton;
            if (parent == null || prefab == null) return;

            IEnumerable<IMediaItem> items = ActiveDefinition.Library.GetKnownItems()
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.DisplayName));
            if (!string.IsNullOrWhiteSpace(query))
                items = items.Where(item => item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (IMediaItem item in items.OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase).Take(ResultCap))
                AddRow(menu, prefab, parent, item);
        }

        internal static void Close()
        {
            ClearRows();
            ActiveDefinition = null;
        }

        internal static void Dispose()
        {
            hint?.Dispose();
            hint = null;
            lookedAtBox = null;
            Close();
        }

        private static void AddRow(Menu_SearchGame menu, GameSearchButton prefab, Transform parent, IMediaItem item)
        {
            GameSearchButton row = UnityEngine.Object.Instantiate(prefab, parent);
            row.gameObject.name = "BR-MediaAPI Search Result";
            TMP_Text name = AccessTools.Field(typeof(GameSearchButton), "nameText")?.GetValue(row) as TMP_Text;
            TMP_Text info = AccessTools.Field(typeof(GameSearchButton), "infoText")?.GetValue(row) as TMP_Text;
            Button button = AccessTools.Field(typeof(GameSearchButton), "button")?.GetValue(row) as Button;
            if (name != null) name.text = item.DisplayName;
            if (info != null) info.text = item.IsSpawned ? "Spawned" : (!item.IsFullyLoaded ? "Loading" : string.Empty);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = !item.IsInHand && item.IsFullyLoaded;
                button.onClick.AddListener(() => Demand(menu, item));
            }
            row.gameObject.SetActive(true);
            CustomRows.Add(row.gameObject);
        }

        private static void Demand(Menu_SearchGame menu, IMediaItem item)
        {
            PlayerInteractionTool tool = Singleton<PlayerToolController>.HasInstance()
                ? Singleton<PlayerToolController>.Instance.GetToolClass<PlayerInteractionTool>()
                : null;
            if (tool == null || item == null) return;

            if (item.IsSpawned)
            {
                Action<MediaRef> demanded = AccessTools.Field(typeof(PlayerInteractionTool), "SpawnedMediaDemanded")?.GetValue(null) as Action<MediaRef>;
                demanded?.Invoke(item.Ref);
            }
            AccessTools.Method(typeof(PlayerInteractionTool), "OnMediaInHandChanged")?.Invoke(tool, new object[] { item });
            menu.Back();
        }

        private static void RetrieveNativeRows(Menu_SearchGame menu)
        {
            object cache = AccessTools.Field(typeof(Menu_SearchGame), "searchResultCache")?.GetValue(menu);
            AccessTools.Method(cache?.GetType(), "RetrieveAll")?.Invoke(cache, Array.Empty<object>());
        }

        private static void ClearRows()
        {
            foreach (GameObject row in CustomRows)
                if (row != null) UnityEngine.Object.Destroy(row);
            CustomRows.Clear();
        }
    }

    [HarmonyPatch(typeof(Menu_SearchGame), "OnSearchTextChanged")]
    internal static class CustomMediaSearchTextPatch
    {
        private static bool Prefix(Menu_SearchGame __instance, string newSearchValue)
        {
            if (!CustomMediaBoxSearch.IsActive) return true;
            CustomMediaBoxSearch.Populate(__instance, newSearchValue);
            return false;
        }
    }

    [HarmonyPatch(typeof(Menu_SearchGame), "OnPreShow")]
    internal static class CustomMediaSearchOpenPatch
    {
        private static void Postfix(Menu_SearchGame __instance)
        {
            if (!CustomMediaBoxSearch.IsActive) return;
            TMP_InputField input = AccessTools.Field(typeof(Menu_SearchGame), "searchInput")?.GetValue(__instance) as TMP_InputField;
            CustomMediaBoxSearch.Populate(__instance, input?.text ?? string.Empty);
        }
    }

    [HarmonyPatch(typeof(Menu), "OnPostHide")]
    internal static class CustomMediaSearchClosePatch
    {
        private static void Postfix(Menu __instance)
        {
            if (__instance is Menu_SearchGame && CustomMediaBoxSearch.IsActive) CustomMediaBoxSearch.Close();
        }
    }
}
