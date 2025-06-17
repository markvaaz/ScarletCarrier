# Changelog

## [0.6.2] - 2025-06-16

### Fixed
- Fixed null reference exception for new players without saved inventory data
- Fixed "entity does not exist" errors in sequence execution
- Fixed network synchronization issues with carrier entities *(typeIndexInArchetype was -1 for NetworkComponentIndex: 82. networkSnapshotType: 355)*

## [0.6.0] - 2025-06-16

- Equipment detection and blocking system to prevent data loss
- Warning messages when attempting to store invalid items
- Automatic item deletion if you try to force store instanced items
