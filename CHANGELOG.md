# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-07-30

### Added

- Searchable parent picker with fuzzy name matching, ancestor paths and keyboard navigation.
- Three entry points: `Ctrl+Shift+H` shortcut (rebindable), Hierarchy context menu, GameObject inspector header.
- `Unparent (a raíz)` command.
- **Crear "X" y agrupar** — creates an empty parent as a sibling of the first selected object, centred on the selection.
- `Mantener posición world` toggle, persisted between sessions.
- Prefab Mode support: only objects inside the open prefab are offered.
- Cross-scene guard, cycle guard, and single-step Undo for every operation.
