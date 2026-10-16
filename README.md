# Out of Office

A one-thumb physics puzzle for iPhone and Android, built in Unity.

## Layout

- `Assets/OutOfOffice/` - game code (`Scripts/`), editor tools and checks (`Editor/`), shaders, materials and fonts (`Resources/`), app icon (`Icon/`)
- `Assets/Plugins/iOS/` - native iOS haptics
- `deploy_ios.sh` - builds the game and installs it on a connected iPhone
- `deploy_android.sh` - builds a development APK and installs it on a connected Android phone (USB debugging on); then `adb logcat -s Unity` shows the game log
- `site/` - the game's website (Cloudflare Worker)

## Getting started

1. Install Unity `6000.6.5f1` (see `ProjectSettings/ProjectVersion.txt`) with iOS and Android build support.
2. Clone this repository and open the folder in Unity Hub. The first open rebuilds the `Library/` cache.
3. Open `Assets/Scenes/SampleScene.unity` and press Play. The game boots itself from code.

Run every automated check from the **Out of Office > Run All Checks** menu, or in batch mode:

```
Unity -batchmode -projectPath . -executeMethod OutOfOffice.EditorTools.AllChecks.Run
```
