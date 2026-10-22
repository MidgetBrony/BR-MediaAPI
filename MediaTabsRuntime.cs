using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using SFB;
using SteamShelf.Media;
using SteamShelf.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BR_MediaAPI
{
    // Additive adapter for BOXROOM's October 2026 Media container. Registration,
    // libraries, saved paths and the original native tabs remain authoritative.
    internal static class MediaTabsRuntime
    {
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Buttons = typeof(UI_TabbedContainer).GetField("tabButtons", Fields);
        private static readonly FieldInfo Screens = typeof(UI_TabbedContainer).GetField("tabScreens", Fields);
        private static readonly FieldInfo MediaTabs = typeof(MediaContainer).GetField("mediaTabs", Fields);
        private static readonly FieldInfo LeftExits = typeof(UI_NavigationGroup).GetField("exitsLeft", Fields);
        private static readonly FieldInfo RightExits = typeof(UI_NavigationGroup).GetField("exitsRight", Fields);
        private static readonly List<Page> Pages = new List<Page>();
        private static UI_TabbedContainer tabs;
        private static Button[] originalButtons;
        private static UI_Container[] originalScreens;
        private static readonly Dictionary<UI_NavigationGroup, UI_NavigationGroup[]> sidebarExits =
            new Dictionary<UI_NavigationGroup, UI_NavigationGroup[]>();
        private static float nextTick;

        internal static void Tick()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.75f;
            try
            {
                if (tabs == null)
                {
                    Pages.Clear(); sidebarExits.Clear();
                    MediaContainer container = Resources.FindObjectsOfTypeAll<MediaContainer>()
                        .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());
                    if (container == null) return;
                    tabs = MediaTabs.GetValue(container) as UI_TabbedContainer;
                    if (tabs == null) return;
                    originalButtons = (Button[])Buttons.GetValue(tabs);
                    originalScreens = (UI_Container[])Screens.GetValue(tabs);
                }
                foreach (MediaTypeDefinition definition in MediaApi.RegisteredTypes.OrderBy(value => value.Id))
                    if (!Pages.Any(page => page.Definition.Id == definition.Id)) AddPage(definition);
                foreach (Page page in Pages) page.Tick();
            }
            catch (Exception exception)
            {
                MelonLogger.Warning("BR-MediaAPI Media tabs: " + exception);
                nextTick = Time.unscaledTime + 5f;
            }
        }

        private static void AddPage(MediaTypeDefinition definition)
        {
            if (originalButtons.Length == 0 || originalScreens.Length == 0) return;
            Button button = CreateButton(originalButtons[0], originalButtons[0].transform.parent, definition.DisplayName);
            button.name = "BR Media Tab " + definition.Key;
            button.navigation = originalButtons[0].navigation;
            RectTransform root = Rect("BR Media Page " + definition.Key, originalScreens[0].transform.parent);
            RectTransform nativeScreen = (RectTransform)originalScreens[0].transform;
            root.anchorMin = nativeScreen.anchorMin; root.anchorMax = nativeScreen.anchorMax;
            root.pivot = nativeScreen.pivot; root.sizeDelta = nativeScreen.sizeDelta;
            root.anchoredPosition = nativeScreen.anchoredPosition;
            root.gameObject.SetActive(false);
            UI_Container screen = root.gameObject.AddComponent<UI_Container>();
            UI_NavigationGroup group = root.gameObject.AddComponent<UI_NavigationGroup>();
            Page page;
            if (definition.LibraryFolder?.SettingsSection != null)
                definition.LibraryFolder.SettingsSection.VisibleInModSettings = true;
            try { page = new Page(definition, root, screen, button, group, originalButtons[0]); }
            catch
            {
                Object.Destroy(root.gameObject); Object.Destroy(button.gameObject);
                throw;
            }
            UI_NavigationGroup nativeGroup = originalScreens[0].GetComponentInChildren<UI_NavigationGroup>(true);
            if (nativeGroup != null)
            {
                var exits = (UI_NavigationGroup[])LeftExits.GetValue(nativeGroup);
                LeftExits.SetValue(group, exits);
                foreach (UI_NavigationGroup sidebar in exits ?? Array.Empty<UI_NavigationGroup>())
                {
                    if (sidebar == null) continue;
                    var existing = (UI_NavigationGroup[])RightExits.GetValue(sidebar) ?? Array.Empty<UI_NavigationGroup>();
                    if (!sidebarExits.ContainsKey(sidebar)) sidebarExits.Add(sidebar, existing);
                    RightExits.SetValue(sidebar, existing.Concat(new[] { group }).ToArray());
                }
            }
            Pages.Add(page);
            int index = originalButtons.Length + Pages.Count - 1;
            button.onClick.AddListener(() =>
            {
                if (tabs.CurrentTab == index) screen.SelectDefaultOrFirstSelectable();
                else tabs.OpenTabAtIndex(index);
            });
            Button[] allButtons = originalButtons.Concat(Pages.Select(value => value.Button)).ToArray();
            Buttons.SetValue(tabs, allButtons);
            Screens.SetValue(tabs, originalScreens.Concat(Pages.Select(value => value.Screen)).ToArray());
            button.gameObject.SetActive(true);
            if (definition.LibraryFolder?.SettingsSection != null)
                definition.LibraryFolder.SettingsSection.VisibleInModSettings = false;
        }

        internal static void Dispose()
        {
            if (tabs != null)
            {
                if (tabs.CurrentTab >= originalScreens.Length) tabs.OpenTabAtIndex(0);
                Buttons.SetValue(tabs, originalButtons); Screens.SetValue(tabs, originalScreens);
            }
            foreach (var pair in sidebarExits) if (pair.Key != null) RightExits.SetValue(pair.Key, pair.Value);
            foreach (Page page in Pages) page.Destroy();
            Pages.Clear(); sidebarExits.Clear(); tabs = null;
        }

        private sealed class Page
        {
            internal readonly MediaTypeDefinition Definition;
            internal readonly UI_Container Screen;
            internal readonly Button Button;
            private readonly RectTransform root;
            private readonly TMP_Text path, status, count;
            private readonly TMP_Text bodyTemplate;
            private readonly Button actionTemplate;
            private string actionError;
            private string pendingPath;
            private readonly Button apply;

            internal Page(MediaTypeDefinition definition, RectTransform root, UI_Container screen,
                Button button, UI_NavigationGroup group, Button template)
            {
                Definition = definition; this.root = root; Screen = screen; Button = button;
                // Copy presentation values, never native behaviours or localization
                // bindings. Those bindings otherwise change custom captions to Games.
                Transform nativeCard = originalScreens[1].transform.Find("Content/Cds");
                if (nativeCard == null) throw new InvalidOperationException("Native CD library card not found.");
                // MusicEX can host these objects in a scroll view. Resolve the
                // native serialized bindings rather than their original paths.
                MediaContainer mediaContainer = originalScreens[1].GetComponentInParent<MediaContainer>();
                bodyTemplate = (TMP_Text)typeof(MediaContainer).GetField("cdCountLabel", Fields).GetValue(mediaContainer);
                UI_AlbumLibraryPathDisplay albumPath = originalScreens[1].GetComponentInChildren<UI_AlbumLibraryPathDisplay>(true);
                if (bodyTemplate == null || albumPath == null)
                    throw new InvalidOperationException("Native CD library styling references are unavailable.");
                actionTemplate = (Button)typeof(UI_AlbumLibraryPathDisplay).GetField("rescanButton", Fields).GetValue(albumPath);
                if (actionTemplate == null)
                    throw new InvalidOperationException("Native CD Refresh button binding is unavailable.");
                RectTransform card = Rect("Library Card", root); Fill(card);
                CopyImage(nativeCard.GetComponent<Image>(), card.gameObject.AddComponent<Image>());
                RectTransform heading = Rect("Title", card);
                heading.anchorMin = new Vector2(0, 1); heading.anchorMax = Vector2.one;
                heading.pivot = new Vector2(0.5f, 1); heading.sizeDelta = new Vector2(0, 71.5644f);
                Transform nativeHeading = nativeCard.Find("TitleContainer");
                CopyImage(nativeHeading.GetComponent<Image>(), heading.gameObject.AddComponent<Image>());
                TMP_Text title = Text(heading, definition.DisplayName + " Library", nativeHeading.GetComponentInChildren<TMP_Text>(true));
                Fill(title.rectTransform, 30, 30, 0, 0);
                title.alignment = TextAlignmentOptions.MidlineLeft;

                RectTransform scrollRoot = Rect("Library Settings Scroll", card); Fill(scrollRoot, 0, 0, 75, 24);
                ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
                scroll.horizontal = false; scroll.scrollSensitivity = 40;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                RectTransform viewport = Rect("Viewport", scrollRoot); Fill(viewport);
                viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
                viewport.gameObject.AddComponent<RectMask2D>();
                RectTransform content = Rect("Library Settings", viewport);
                content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = content; scroll.viewport = viewport;
                RectTransform barRoot = Rect("Scrollbar", scrollRoot);
                barRoot.anchorMin = new Vector2(1, 0); barRoot.anchorMax = Vector2.one;
                barRoot.pivot = new Vector2(1, 0.5f); barRoot.sizeDelta = new Vector2(20, 0);
                barRoot.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.45f, 0.49f, 0.12f);
                RectTransform handle = Rect("Handle", barRoot); Fill(handle);
                Image handleImage = handle.gameObject.AddComponent<Image>();
                handleImage.color = new Color32(56, 116, 125, 255);
                Scrollbar bar = barRoot.gameObject.AddComponent<Scrollbar>();
                bar.direction = Scrollbar.Direction.BottomToTop; bar.handleRect = handle; bar.targetGraphic = handleImage;
                scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalScrollbarSpacing = 4;
                Fill(viewport, 0, 24, 0, 0);
                scrollRoot.gameObject.AddComponent<UI_ScrollRectFollowSelection>();
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(30, 30, 0, 16);
                layout.spacing = 8; layout.childControlHeight = true; layout.childControlWidth = true;
                layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
                count = Label(content, "", 44);
                Separator(content);
                if (definition.LibraryFolder != null)
                {
                    Label(content, "Choose the folder containing your " + definition.DisplayName.ToLowerInvariant() + ".", 90);
                    Separator(content);
                    Label(content, "Set the parent folder path below", 44);
                    RectTransform location = Row(content, "Location", 44);
                    location.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                    TMP_Text locationLabel = Label(location, definition.DisplayName + " Location:", 44);
                    LayoutElement locationSize = locationLabel.GetComponent<LayoutElement>();
                    locationSize.minWidth = locationSize.preferredWidth = 330; locationSize.flexibleWidth = 0;
                    locationLabel.fontSize = 40; locationLabel.color = new Color32(38, 61, 66, 255);
                    status = Label(location, "", 44);
                    status.color = new Color32(67, 104, 115, 255);
                    status.fontSize = status.fontSizeMax = 40; status.fontSizeMin = 28;
                    status.enableAutoSizing = true; status.textWrappingMode = TextWrappingModes.NoWrap;
                    status.overflowMode = TextOverflowModes.Ellipsis;
                    RectTransform folder = Rect("Folder", content);
                    LayoutElement folderSize = folder.gameObject.AddComponent<LayoutElement>();
                    folderSize.minHeight = folderSize.preferredHeight = 70; folderSize.flexibleHeight = 0;
                    RectTransform field = Rect("Folder Path", folder);
                    var fieldSizing = field.gameObject.AddComponent<LayoutElement>();
                    fieldSizing.minWidth = fieldSizing.preferredWidth = 0; fieldSizing.flexibleWidth = 3;
                    fieldSizing.minHeight = fieldSizing.preferredHeight = 66;
                    Image fieldImage = field.gameObject.AddComponent<Image>();
                    UIFolderBrowser nativeBrowser = nativeCard.GetComponentInChildren<UIFolderBrowser>(true);
                    TMP_Text nativePath = (TMP_Text)typeof(UIFolderBrowser).GetField("folderPathDisplay", Fields).GetValue(nativeBrowser);
                    Image nativeField = nativePath.GetComponentInParent<Image>();
                    fieldImage.sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sprite => sprite.name == "SquareRounded_Border")
                        ?? nativeField.overrideSprite ?? nativeField.sprite;
                    fieldImage.color = new Color32(56, 116, 125, 255);
                    fieldImage.type = Image.Type.Sliced; fieldImage.pixelsPerUnitMultiplier = 3;
                    fieldImage.fillCenter = true;
                    // Match the fixed native field and Browse geometry; filling
                    // the whole row changes both proportions and border thickness.
                    field.anchorMin = field.anchorMax = new Vector2(0, 0.5f);
                    field.pivot = new Vector2(0, 0.5f);
                    field.anchoredPosition = Vector2.zero;
                    field.sizeDelta = new Vector2(785.9363f, 67.263f);
                    path = Text(field, "", nativePath);
                    path.rectTransform.anchorMin = Vector2.zero;
                    path.rectTransform.anchorMax = Vector2.one;
                    path.rectTransform.anchoredPosition = new Vector2(-1.2734985f, 0);
                    path.rectTransform.sizeDelta = new Vector2(-29.7119f, 0);
                    path.alignment = TextAlignmentOptions.MidlineLeft;
                    path.fontSize = 38.72f; path.fontSizeMin = 18; path.fontSizeMax = 38.72f;
                    path.textWrappingMode = TextWrappingModes.NoWrap;
                    path.enableAutoSizing = true; path.overflowMode = TextOverflowModes.Ellipsis;
                    Button browse = ActionButton(folder, "Browse", Browse);
                    RectTransform browseRect = (RectTransform)browse.transform;
                    browseRect.anchorMin = browseRect.anchorMax = new Vector2(0, 0.5f);
                    browseRect.pivot = new Vector2(0, 0.5f);
                    browseRect.anchoredPosition = new Vector2(823, 0);
                    browseRect.sizeDelta = new Vector2(260, 70);
                    Label(content, "If the path looks correct, hit apply", 26).fontSize = 24;
                    Separator(content);
                    apply = ActionButton(content, "Apply", () => Run(() =>
                    {
                        if (string.IsNullOrWhiteSpace(pendingPath)) return;
                        MediaLibraryFolderPanel.SetPathAndReload(Definition, pendingPath);
                        pendingPath = null;
                    }));
                    apply.interactable = false;
                    Separator(content);
                    Label(content, "If you add more " + definition.DisplayName.ToLowerInvariant() + ", hit this:", 44);
                    ActionButton(content, "Refresh", () => Run(definition.LibraryFolder.Reload));
                    Screen.SetDefaultSelectable(folder.GetComponentInChildren<Button>());
                    if (definition.LibraryFolder.ShowOpenFolderButton)
                    {
                        Separator(content);
                        Button open = ActionButton(content, "Open Folder", () => Run(() => MediaLibraryFolderPanel.OpenFolder(Definition)));
                        LayoutElement openSize = open.GetComponent<LayoutElement>();
                        openSize.minHeight = openSize.preferredHeight = 58;
                    }
                }
                else
                {
                    status = Label(content, "Library folders are managed by " + definition.DisplayName + ".", 90);
                }
                if (definition.LibraryFolder?.SettingsSection != null)
                {
                    Separator(content);
                    ModsPanel.ModsPanelApi.RenderEmbeddedSection(definition.LibraryFolder.SettingsSection, content, bodyTemplate);
                }
            }

            internal void Tick()
            {
                if (!root.gameObject.activeInHierarchy) return;
                try
                {
                    int total = Definition.Library.GetKnownItems()?.Count ?? 0;
                    count.text = $"You currently have {total} {Definition.DisplayName.ToLowerInvariant()} ready to place.";
                    if (path != null)
                    {
                        string saved = MediaLibraryFolderPanel.GetPath(Definition);
                        path.text = pendingPath ?? saved;
                        apply.interactable = !string.IsNullOrWhiteSpace(pendingPath) && pendingPath != saved;
                    }
                    if (actionError != null) status.text = actionError;
                    else if (Definition.LibraryFolder != null) status.text = MediaLibraryFolderPanel.GetStatus(Definition);
                }
                catch (Exception exception) { status.text = "Could not refresh library status: " + exception.Message; }
            }

            private void Browse() => Run(() =>
            {
                string[] selected = StandaloneFileBrowser.OpenFolderPanel(Definition.LibraryFolder.BrowseTitle,
                    MediaLibraryFolderPanel.GetPath(Definition), false);
                if (selected != null && selected.Length > 0 && !string.IsNullOrWhiteSpace(selected[0]))
                    pendingPath = selected[0];
            });

            private void Run(Action action)
            {
                try { actionError = null; action(); }
                catch (Exception exception)
                {
                    actionError = "Library action failed: " + exception.Message;
                    MelonLogger.Warning(exception.ToString());
                }
                Tick();
            }

            private Button ActionButton(Transform parent, string caption, Action action)
            {
                Button button = CreateButton(actionTemplate, parent, caption);
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                var sizing = button.gameObject.AddComponent<LayoutElement>();
                sizing.minWidth = sizing.preferredWidth = 0; sizing.flexibleWidth = 1;
                sizing.minHeight = sizing.preferredHeight = 70f; sizing.flexibleHeight = 0f;
                button.onClick.AddListener(new UnityAction(action));
                return button;
            }

            private TMP_Text Label(Transform parent, string value, float height)
            {
                TMP_Text label = Text(parent, value, bodyTemplate);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                var sizing = label.gameObject.AddComponent<LayoutElement>();
                sizing.minHeight = sizing.preferredHeight = height;
                sizing.minWidth = sizing.preferredWidth = 0; sizing.flexibleWidth = 1;
                return label;
            }

            private static void Separator(Transform parent)
            {
                RectTransform line = Rect("Separator", parent);
                line.gameObject.AddComponent<Image>().color = new Color(0.8301887f, 0.8014714f, 0.8014714f, 1);
                line.gameObject.AddComponent<LayoutElement>().preferredHeight = 5.2607f;
            }

            internal void Destroy()
            {
                if (Definition.LibraryFolder?.SettingsSection != null)
                    Definition.LibraryFolder.SettingsSection.VisibleInModSettings = true;
                if (Button != null) Object.Destroy(Button.gameObject); if (root != null) Object.Destroy(root.gameObject);
            }
        }

        private static Button CreateButton(Button template, Transform parent, string caption)
        {
            RectTransform root = Rect(caption, parent);
            root.sizeDelta = ((RectTransform)template.transform).sizeDelta;
            Image background = root.gameObject.AddComponent<Image>();
            CopyImage(template.targetGraphic as Image, background);
            background.color = new Color32(59, 120, 129, 255);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background; button.colors = template.colors;
            button.transition = template.transition; button.spriteState = template.spriteState;
            TMP_Text label = Text(root, caption, template.GetComponentInChildren<TMP_Text>(true));
            Fill(label.rectTransform, 10, 10, 0, 0);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 45f; label.fontSizeMin = 18f; label.fontSizeMax = 45f;
            label.color = Color.white;
            label.enableAutoSizing = true;
            return button;
        }

        private static TMP_Text Text(Transform parent, string value, TMP_Text template)
        {
            RectTransform rect = Rect("Text", parent);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = template.font; label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = template.fontSize; label.color = template.color; label.fontStyle = template.fontStyle;
            label.textWrappingMode = TextWrappingModes.Normal; label.richText = false;
            label.text = value; label.raycastTarget = false;
            return label;
        }

        private static void CopyImage(Image source, Image target)
        {
            target.sprite = source.overrideSprite ?? source.sprite; target.color = source.color; target.type = source.type;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.preserveAspect = source.preserveAspect;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)obj.transform; rect.SetParent(parent, false); return rect;
        }
        private static void Fill(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static RectTransform Row(Transform parent, string name, float height)
        {
            RectTransform rect = Rect(name, parent);
            LayoutElement sizing = rect.gameObject.AddComponent<LayoutElement>();
            sizing.minHeight = sizing.preferredHeight = height; sizing.flexibleHeight = 0;
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.MiddleLeft;
            return rect;
        }
    }
}
