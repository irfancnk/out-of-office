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

## Audio

Every sound is synthesized in code, so the game ships with no audio files. `Sfx.cs` builds the effects at startup. The soundtrack is written as data in `Songs.cs` (a menu theme and one song per chapter) and rendered by `Composer.cs` on a worker thread into three looping stems (bed, groove, lead) plus win and lose stingers. `MusicPlayer.cs` crossfades between songs and mixes the stems by the mood of play.

- **Out of Office > Export Soundtrack** writes every song, stem, stinger and fanfare to `Builds/Soundtrack/` as WAV files for listening.
- The audio checks run with Run All Checks. A Play-mode smoke test of the live music (song switch, stems in lockstep, no frame hitch) runs with:

```
Unity -batchmode -projectPath . -executeMethod OutOfOffice.EditorTools.AudioSmoke.RunBatch
```
