# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 11-24-2024

### Added

- PlayerDataRegistry Networking permissions, configure which PlayerDataClass types can be seen/
edited by other players on the network.
- WebRequests to make POST requests to a backend.
- PlayerDataRegistry Web Authentication uses the new WebRequests class to register and authenticate
players. (requires Node.js and SQL backend)
- PlayerDataRegistry Web Database uses the new WebRequests class to store and retrieve 
PlayerDatabaseDataClass concrete implementations.
- Custom PlayerDataRegistry editor to visualize PlayerData and fine tune settings.

### Changed

- PlayerDataRegistry uses broadcasts for synchronization.

### Removed

- NetworkedPlayerDataRegistry object structure, switched to broadcasts for synchronization instead.

## [1.0.1] - 10-01-2024

### Added

- PlayerManager#LocalPlayerJoinedEvent
- PlayerManager#LocalPlayerLeftEvent

## [1.0.0] - 09-16-2024

### Added

- PlayerManager component to pair with new input system player manager
  - Keeps track of local player's input and uid for use with data registry
  - Prompt users for device input if necessary
- PlayerDataRegistry holds each player's unique PlayerData
  - Syncs with network if using FishNetworking
  - Hold as much information as necessary
- PlayerData components
  - Can hold any type of PlayerDataClass (essentially anything as long as it can be serialized)
  - Always holds an IdentifierData to keep a proper label
