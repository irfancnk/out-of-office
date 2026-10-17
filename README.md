# Out of Office

A one-thumb physics puzzle for iPhone and Android, built in Unity.

## Layout

- `Assets/OutOfOffice/` - game code (`Scripts/`), editor tools and checks (`Editor/`), shaders, materials and fonts (`Resources/`), app icon (`Icon/`)
- `Assets/Plugins/iOS/` - native iOS haptics
- `deploy_ios.sh` - builds the game and installs it on a connected iPhone
- `deploy_android.sh` - builds a development APK and installs it on a connected Android phone (USB debugging on); then `adb logcat -s Unity` shows the game log
- `RELEASE.md` - the Google Play release checklist; `OOO_RELEASE=1 ./deploy_android.sh` builds the signed App Bundle
- `site/` - the game's website (Cloudflare Worker)

## Getting started

1. Install Unity `6000.6.5f1` (see `ProjectSettings/ProjectVersion.txt`) with iOS and Android build support.
2. Clone this repository and open the folder in Unity Hub. The first open rebuilds the `Library/` cache.
3. Open `Assets/Scenes/SampleScene.unity` and press Play. The game boots itself from code.

Run every automated check from the **Out of Office > Run All Checks** menu, or in batch mode:

```
Unity -batchmode -projectPath . -executeMethod OutOfOffice.EditorTools.AllChecks.Run
```

## Rooms and the level editor

Every room is a JSON file in `Assets/OutOfOffice/Resources/Levels/`, named after its id; `chapters.json` lists the chapters in order with their rooms (chapter *n* uses the *n*th theme in `Theme.cs`). The game loads them at start-up, so no room lives in C#.

- **Out of Office > Level Editor** (Ctrl+Shift+L): build a room on a snapping grid, see problems live, **Check this room** to run the solver and draw its solution, and **Play this room** to jump straight into it in Play mode.
- **Out of Office > Levels > Difficulty Report (CSV)**: LevelCheck on every room, one row each, for tuning the difficulty curve.
- Ids are save keys. `Assets/OutOfOffice/Editor/ShippedLevelIds.txt` lists every id that has shipped; the checks fail if one disappears. Append new ids there when a build with them ships.
