# BR-MediaAPI 1.1.23

## Fixed

- Resets transient `IsSpawned` and `IsInHand` flags for every registered custom
  media library before BOXROOM reconstructs a newly loaded room.
- Prevents source boxes from remaining empty after leaving and re-entering a room
  because items from the previous room were still marked as spawned.

Room restoration remains authoritative: shelves and loose props mark only the
items that are actually restored into the new room. Consumer-owned resets, such
as BR-Libretro's existing ROM reset, remain safe as backward-compatible redundancy.
