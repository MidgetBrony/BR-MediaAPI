using SteamShelf.Media;
using SteamShelf.PlayerTools;
using System;
using UnityEngine;

namespace BR_MediaAPI
{
    /// <summary>Customizes how a media item is presented and operated inside BOXROOM's inspector.</summary>
    public sealed class MediaInspectDefinition
    {
        /// <summary>Optional inspector-only prefab. The held prefab is used when omitted.</summary>
        public Func<IMediaItem, GameObject> PrefabFactory { get; set; }

        /// <summary>Text replacing BOXROOM's Open/Play action while this item is inspected.</summary>
        public string PrimaryActionLabel { get; set; } = "Open";

        /// <summary>The inspector's primary action; for example Pull Out Record, Read, or Insert Disc.</summary>
        public Action<MediaInspectContext> OnPrimaryAction { get; set; }

        public Action<MediaInspectContext> OnEnter { get; set; }
        public Action<MediaInspectContext> OnUpdate { get; set; }
        public Action<MediaInspectContext> OnExit { get; set; }
    }

    /// <summary>Runtime state supplied to a media mod during custom inspection.</summary>
    public sealed class MediaInspectContext
    {
        private readonly Action<GameObject> replaceVisual;

        internal MediaInspectContext(BoxInspector inspector, IMediaItem item, GameObject visual, Action<GameObject> replace)
        {
            Inspector = inspector;
            Item = item;
            Visual = visual;
            replaceVisual = replace;
        }

        public BoxInspector Inspector { get; }
        public IMediaItem Item { get; }
        public GameObject Visual { get; internal set; }
        public Transform Holder => Inspector?.BoxHolder;
        public Camera Camera => Camera.main;

        /// <summary>Swaps the displayed object while retaining the active inspect session.</summary>
        public void ReplaceVisual(GameObject replacement) => replaceVisual?.Invoke(replacement);
    }
}
