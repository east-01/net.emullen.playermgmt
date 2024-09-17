# Player Management

## Table of Contents
- [Introduction](#introduction)
- [Getting Started](#getting-started)
- [Usage](#usage)

---

## Introduction
Keep track of your local players when using the unity new input system.<br>
Another powerful feature is the PlayerDataRegistry which acts as an infinite size container for
any and all data you will need to store. This data will synchronize over the network if you're
using FishNetworking.

## Getting Started
Place the provided PlayerManager (found in samples folder) in a scene that will always be activated
(the PlayerManager will move to DoNotDestroyOnLoad once initialized). This ensures you will have
a new input system PlayerInputManager and a PlayerManager/PlayerDataRegistry combo in the game.

### PlayerManager
Connect an input prompt canvas, input prompt panel, and device missing panel to the component so
the PlayerManager can prompt the users for the necessary inputs.<br>
You can blacklist which scenes the primary player's control scheme will switch- this is mainly used
for scenes where other players need to join and we don't the primary player to take over.<br>
You can blacklist which scenes the player input prompt will appear- this is mainly used for the
title screen as it is assumed the player will join with input there.<br>
In code, you can track each local player through the PlayerManager#Instance#LocalPlayer array.<br>
A LocalPlayer object will give you the player's PlayerInput component and their uid.

### PlayerDataRegistry
The PlayerDataRegistry is a class designed to manage and store all PlayerData instances for the 
  local game instance. When using a networked setup with FishNet, it ensures that the player data 
  is synchronized with the server.<br>
<br>
Features<br>
<br>
* Singleton pattern to ensure only one instance of PlayerDataRegistry exists.
* Manages PlayerData objects using a dictionary keyed by unique identifiers.
* Integrates with FishNet for networked synchronization.
* Handles different phases of joining and managing a networked registry.
<br>
Get each player's data using PlayerDataRegistry#Instance#GetPlayerData, then you can retrieve
  any subclass data from there and make changes.

## Usage
  * Add a PlayerManager prefab sample to a bootstrapper scene.