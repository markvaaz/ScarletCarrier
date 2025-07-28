# Changelog

## [0.8.3] - 2025-07-28

### Changed
- All calls to BuffService.TryApplyBuff now explicitly set the buff duration
- This change does not affect gameplay, but improves code clarity and future compatibility

## [0.8.1] - 2025-07-08

### Added
- **Emote Controls Toggle**: Added option to disable emote-based carrier controls
- Players can now choose to use only commands instead of emotes for carrier management

### Fixed
- **Equipment Transfer Prevention**: Added protection against transferring equipped items directly from player to carrier inventory
- Prevents players from bypassing equipment restrictions by unequipping items directly into carrier storage
- Previously this protection existed but would purposely delete the transferred item - now it simply blocks the transfer

## [0.8.0] - 2025-07-03

### Added
- **Carrier Following System**: Carriers can now follow the player when summoned
  - Use `.carrier follow` to enable following behavior
  - Use `.carrier stop` to disable following and make carrier stay in place
- **Emote Controls**: Added intuitive emote-based carrier management
  - 👍 - Summon/call your carrier
  - 👎 - Dismiss your carrier
  - 👉 - Toggle follow/stay behavior

### Changed
- **Removed automatic despawn**: Carriers no longer automatically disappear after 60 seconds
- Carriers now remain active until manually dismissed by the player (or server restart)

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
