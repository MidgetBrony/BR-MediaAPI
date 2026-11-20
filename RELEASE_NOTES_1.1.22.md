# BR-MediaAPI 1.1.22

## Fixed

- Preserves a custom loose media case's serialized media identity when BOXROOM
  replaces its provisional save state during first placement.
- Documents the `ICustomMediaProp` save-state handoff required for consumer-owned
  loose media props.

This prevents generic custom media cases from returning as empty physical shells
after saving and reloading a room.
