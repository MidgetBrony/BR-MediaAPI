# DVDs_Cache format

Default location:

```text
%USERPROFILE%\AppData\LocalLow\NestedLoop\BOXROOM\Boxroom-Plus\DVDs_Cache
```

Each DVD has its own folder. Folders may be nested for organization:

```text
DVDs_Cache/
  Movies/
    My_Movie/
      meta.json
      cover.jpg
      My Movie.mkv
```

`meta.json` uses this PascalCase contract:

```json
{
  "Version": 1,
  "DvdID": "my-movie-2026",
  "Title": "My Movie",
  "Studio": "Example Studio",
  "Year": 2026,
  "Genre": "Adventure",
  "Rating": "PG",
  "Type": "DVD",
  "FileName": "My Movie.mkv"
}
```

`DvdID` is the permanent shelf/save identity and must be unique. `FileName` is optional; without it the loader selects the first supported video file (`.mp4`, `.mkv`, `.avi`, `.mov`, `.m4v`, or `.iso`). Covers may be `cover.jpg`, `cover.jpeg`, `cover.png`, `folder.jpg`, `poster.jpg`, or `poster.png`.

On first launch, the sample mod creates `Sample_DVD/meta.json` and `cover.png` automatically. Add a video file to that folder if you want the Open action to launch something.
