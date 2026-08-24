# API design notes

BOXROOM currently defines only Steam games and CD albums in `eMediaType`. Casting a stable integer to that enum works, but the game's built-in switches, shelf flags, and media router do not know what to do with it. BR-MediaAPI owns those shared bridges once.

The API deliberately does not force a universal metadata schema. A movie needs runtime, rating, and file path; a record needs artist and sides; a book needs author and pages. `IMediaItem` supplies the common save/reference/display surface while each mod retains a strongly typed model.

The API also does not own scanning. Each media mod knows its real-world folder and tag rules and exposes the result through `IMediaLibrary`. This keeps the central API small and prevents unrelated media formats from becoming coupled.
