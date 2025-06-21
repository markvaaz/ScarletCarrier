# Changelog

## [0.7.0] - 2025-06-18

### Added
- Customizable carrier appearance system
- `.carrier list` command to view available appearances
- `.carrier <number>` command to change carrier appearance
- Personal appearance settings that persist between summons

### Fixed
- Fixed error `typeIndexInArchetype was -1 for NetworkComponentIndex: 82. networkSnapshotType: 355`

## [0.6.4] - 2025-06-18

### Changed
- Coffins are now destroyed instead of using the LifeTime component
- Added a cleanup routine to remove all carrier-related entities when the server starts, preventing leftovers from previous sessions

## [0.6.3] - 2025-06-17

### Fixed
- Fixed servant spawning at incorrect height - now spawns at same level as player

## [0.6.2] - 2025-06-16

### Fixed
- Fixed null reference exception for new players without saved inventory data
- Fixed "entity does not exist" errors in sequence execution

## [0.6.0] - 2025-06-16

- Equipment detection and blocking system to prevent data loss
- Warning messages when attempting to store invalid items
- Automatic item deletion if you try to force store instanced items
