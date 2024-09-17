# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
