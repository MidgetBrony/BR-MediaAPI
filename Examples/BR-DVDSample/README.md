# BR-DVDSample

A minimal working custom media mod for BR-MediaAPI. It registers media type `1100`, scans a real `DVDs_Cache`, seeds one `Sample DVD`, and uses BR-MediaAPI's bundled Boxroom-Studios standard media case.

The sample demonstrates:

- a strongly typed `DvdItem` model;
- an `IMediaLibrary` containing one DVD;
- a recursive LocalLow cache loader with PascalCase `meta.json`;
- the shared Boxroom-Studios case for shelf, loose, and held contexts;
- a generated loose DVD case implementing `ICustomMediaProp`;
- RoomState media ID serialization;
- an Open callback.

Build from the BR-MediaAPI repository root:

```text
dotnet build Examples/BR-DVDSample/BR-DVDSample.csproj -c Release -p:DeployToGame=false
```

Install both `BR_MediaAPI.dll` and `BR_DVDSample.dll` in BOXROOM's `Mods` folder.

In BOXROOM, obtain the media the same way Books works:

1. Open BOXROOM's furniture/placeables build catalogue.
2. Place the **DVD Box**.
3. Finish placing it so its contents populate.
4. Take **Sample DVD** from inside the box.

The DVD itself is not furniture and does not appear directly in the catalogue. The box is the source of currently unplaced DVDs.

The library is loaded from `%USERPROFILE%\AppData\LocalLow\NestedLoop\BOXROOM\Boxroom-Plus\DVDs_Cache`. See `DVD_FORMAT.md` for the folder and metadata contract. A `Sample_DVD` entry is created automatically on first launch.

Case-shaped media can call `SharedMediaCasePrefabs.Configure(definition)` and provide only cover art plus the display title. Media with a different physical shape should supply dedicated shelf, loose, and held AssetBundle prefabs.
