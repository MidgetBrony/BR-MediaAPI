# BR-MediaAPI 1.1.22

## Fixed

- Preserves a custom loose media case's serialized media identity when BOXROOM
  replaces its provisional save state during first placement.
- Promotes consumer-owned `ICustomMediaProp` data into a correctly identified save
  state using BOXROOM's final placement position, rotation, and scale.
- Documents the automatic `ICustomMediaProp` save-state handoff for consumer-owned
  loose media props, while retaining consumer safeguards as harmless redundancy.

This prevents generic custom media cases from returning as empty physical shells
after saving and reloading a room.
