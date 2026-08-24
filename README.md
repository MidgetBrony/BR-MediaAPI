# BR-MediaAPI

BR-MediaAPI lets BOXROOM mods add genuinely new media kinds—movies, records, books, VHS tapes, magazines, or anything else—without each mod maintaining its own patches for the game's built-in two-value media enum.

## What a media mod provides

1. A permanent numeric ID (`1000` or higher) and unique reverse-domain key.
2. A model class implementing `IMediaItem` (or inheriting `MediaItemBase`).
3. An `IMediaLibrary` that resolves saved IDs to those models.
4. `SharedMediaCasePrefabs.Configure(definition)` for the standard case, or custom prefab factories.
5. Optionally, an `OnOpen` action for Play, Read, Listen, etc.
6. Optionally, `LibraryFolder` settings for a native ModsPanel folder selector.

By default, the API also creates a native **Unplaced Media Box** in BOXROOM's furniture catalogue. It borrows the built-in CD Album Box presentation and automatically fills it with every available, unplaced item from the registered library. Media mods do not implement catalogue registration, container filling, reservations, refill, or box save/load.

The ID and key become save-file contracts. Never change or reuse them after release. Publish your chosen ID in your README so other modders can avoid it.

## Smallest registration

Register during your mod's `OnInitializeMelon`. The API safely attaches the library whether registration happens before or after BOXROOM's media bootstrap.

```csharp
private const int MovieTypeId = 1100;

public override void OnInitializeMelon()
{
    var definition = new MediaTypeDefinition
    {
        Id = MovieTypeId,
        Key = "com.yourname.boxroom.movies",
        DisplayName = "Movies",
        ModelType = typeof(MovieItem),
        Library = MovieLibrary.Instance,
        AllowOnShelves = true,
        OnOpen = item => MoviePlayer.Play((MovieItem)item)
    };

    definition.LibraryFolder = new MediaLibraryFolderOptions
    {
        DefaultPath = MovieLibrary.DefaultPath,
        Reload = MovieLibrary.Instance.LoadCache,
        GetStatus = () => $"{MovieLibrary.Instance.GetKnownItems().Count} movies found"
    };

    SharedMediaCasePrefabs.Configure(definition);
    MediaApi.Register(definition);
}
```

```csharp
public sealed class MovieItem : MediaItemBase
{
    public MovieItem(string id, string title, byte[] poster)
        : base((eMediaType)1100, id)
    {
        Title = title;
        CoverArtBytes = poster;
    }

    public string Title { get; }
    public override string DisplayName => Title;
}
```

`PrefabShelfFactory` searches the prefab for a renderer named `Cover`, fills all TextMeshPro labels with `DisplayName`, applies `CoverArtBytes`, and implements BOXROOM's `IShelfItem` lifecycle. Use your own `ShelfFactory` when a record, cartridge, or other shape needs custom visuals.

`LibraryFolder` creates a ModsPanel section with Browse, Refresh, Open Folder, persisted selection, and live status. Read the selected location with `MediaApi.GetLibraryFolder((eMediaType)MovieTypeId)`. The API invokes `Reload` when BOXROOM's media bootstrap is ready, after changing folders, and when the player presses Refresh. The media mod only parses its own files and supplies the reload callback and optional status text. Set `LoadOnMediaBootstrap = false` only when a mod deliberately controls its own initial scan.

## Custom inspection and actions

The standard case automatically appears in BOXROOM's inspector. Supply `Inspect` when the media needs its own action or presentation:

```csharp
definition.Inspect = new MediaInspectDefinition
{
    PrimaryActionLabel = "Pull Out Record",
    PrefabFactory = item => RecordAssets.CreateInspectSleeve((RecordItem)item),
    OnEnter = context => RecordAnimator.Prepare(context.Visual),
    OnUpdate = context => RecordAnimator.Tick(context.Visual),
    OnPrimaryAction = context =>
    {
        GameObject vinyl = RecordAssets.CreateVinyl((RecordItem)context.Item);
        context.ReplaceVisual(vinyl);
    },
    OnExit = context => RecordAnimator.Stop()
};
```

`MediaInspectContext` exposes the selected item, `BoxInspector`, holder, camera, and current visual. `ReplaceVisual` lets an action move from a sleeve or closed box to a record, disc, cartridge, open board game, reader, or any other custom state. If `PrefabFactory` is omitted, the API uses the registered held prefab as the inspect model.

Do not reuse one Unity transform for all three contexts. BOXROOM applies different parent rotations, scales, and camera poses to shelf, loose, and held objects. AssetBundle authors should export a dedicated prefab for each context, with its root transform already authored for that context. The API handles behavior; the media mod owns physical art and orientation.

The default source box will be named `Movies Box` in this example. Customize or disable it only when needed:

```csharp
definition.UnplacedMediaBoxName = "Movie Crate";
definition.UnplacedMediaBoxDescription = "All movies that are not currently placed";
// definition.CreateUnplacedMediaBox = false;
```

## Optional loose/placeable support

Set all three properties together:

```csharp
definition.PlaceableId = "YourMod_MovieCase";
definition.PlaceableDataFactory = MovieAssets.GetOrCreatePlaceableData;
definition.LoosePrefabFactory = MovieAssets.InstantiateLoosePrefab;
```

The API then lets RoomState resolve the private placeable ID and recreates the prefab during load. Put an `ICustomMediaProp` component on that prefab. It owns media-specific visuals and serialization while the API handles shelf-to-hand conversion, generic pickup, and BOXROOM's placement tool. Serialize the media type and ID in `PlaceableSaveState.CustomData` so the library can resolve it after loading.

## Build and install

Copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and set `GamePath`, or pass `-p:GamePath=...`. Build with:

```text
dotnet build -c Release -p:DeployToGame=false
```

Copy `BR_MediaAPI.dll`, `brmediaapi_assets`, and `ModsPanel.dll` to BOXROOM's `Mods` folder. A consuming mod references BR-MediaAPI and lists it as a required dependency.

## API behavior

- Rejects built-in/legacy-range IDs and duplicate IDs, keys, or placeable IDs with clear errors.
- Registers every custom `IMediaLibrary` at BOXROOM's correct bootstrap point.
- Routes shelf creation and custom media acceptance centrally.
- Restores optional custom placeables from RoomState.
- Leaves Steam games and CD albums completely on BOXROOM's original paths.

BR-BookSystem can migrate from its historical ID `2` to this API in a compatibility release, but existing saves need an explicit ID migration rather than silently changing the value.

## Working sample

`Examples/BR-DVDSample` contains a separate sample mod using the shared case, configurable ModsPanel DVD folder, recursive cache, shelf/held/inspect/loose behavior, save/load data, and Open callback.
