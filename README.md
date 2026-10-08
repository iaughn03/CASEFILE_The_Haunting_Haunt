# CASEFILE: The Haunting Haunt

An educational horror investigation game made in Unity. The player is a detective sent to the scene of a death. On each map they search the crime scene for evidence while hiding from a ghost, then close the case at the Final Inspection by connecting the clues, choosing the ruling (Parricide, Murder or Homicide) and naming the suspect. Each case ends with a lesson on what makes a killing Parricide, Murder or Homicide.

## Maps

1. Asylum Ward
2. Nexus Residential Block
3. Tudor Street

## Built With

* Unity 6 (6000.3.10f1), Universal Render Pipeline
* C#

## What This Repository Contains

This repository holds the source code of the game:

* `Assets/Scripts`: all gameplay C# scripts (player, ghost, evidence, Final Inspection, menus, settings)
* `Assets/Scripts/Evidence`: the evidence, case wall and Final Inspection system
* `Assets/Scripts/Settings`: graphics, display and keybind settings
* `Assets/Editor`: editor tools used during development
* `Packages`: the Unity packages the project uses
* `ProjectSettings`: the Unity project settings

The 3D models, textures, sounds and scenes are not included because the full project is about 8.6 GB, which is too large for a GitHub repository.

## Controls

* Move: W A S D
* Look around: Mouse
* Sprint: L SHIFT
* Crouch: L CTRL
* Jump or vault: SPACE
* Interact, open, pick up, hide: E
* Flashlight: F
* Evidence bag: TAB
* Drop held item: G
* Pause: ESC
