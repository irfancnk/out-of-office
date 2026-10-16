#!/bin/bash
# Builds the game and installs it on a connected Android phone (USB debugging on). Windows (Git Bash) or macOS.
# Usage: ./deploy_android.sh            development build (logs and analytics events in logcat)
#        OOO_DEV=0 ./deploy_android.sh  release-like build
# Follow the game's log afterwards: adb logcat -s Unity
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")" && pwd)"
VERSION="6000.6.5f1"
BUNDLE_ID="${BUNDLE_ID:-com.irfancankaleli.outofoffice}"
if [ -x "/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe" ]; then
  UNITY="/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"
  ANDROID="/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Data/PlaybackEngines/AndroidPlayer"
else
  UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
  ANDROID="/Applications/Unity/Hub/Editor/$VERSION/PlaybackEngines/AndroidPlayer"
fi
ADB="$ANDROID/SDK/platform-tools/adb"
[ -x "$ADB" ] || ADB="$ADB.exe"
OUT="$PROJECT/Builds/Android/OutOfOffice.apk"

fail() { echo "ERROR: $*" >&2; exit 1; }

DEVICE="$("$ADB" devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
[ -n "$DEVICE" ] || fail "No Android phone found. Turn on USB debugging, connect by USB, and tap Allow on the phone."
echo "Phone $DEVICE ($("$ADB" -s "$DEVICE" shell getprop ro.product.model | tr -d '\r'))"

echo "[1/3] Unity build (the first run is slow: it imports for Android and compiles IL2CPP)"
PROJECT_ARG="$PROJECT"
command -v cygpath >/dev/null && PROJECT_ARG="$(cygpath -w "$PROJECT")"
OOO_ANDROID_OUT="$OUT" OOO_BUNDLE_ID="$BUNDLE_ID" OOO_DEV="${OOO_DEV:-1}" \
  "$UNITY" -batchmode -quit -projectPath "$PROJECT_ARG" -buildTarget Android \
  -executeMethod OutOfOffice.EditorTools.BuildAndroid.Run -logFile "$PROJECT/Logs/android-build.log" \
  || { grep -E "error|OOO" "$PROJECT/Logs/android-build.log" | tail -20; fail "Unity build failed. Log: Logs/android-build.log"; }
[ -f "$OUT" ] || fail "APK not found at $OUT"

echo "[2/3] Installing"
"$ADB" -s "$DEVICE" install -r "$OUT"

echo "[3/3] Launching"
"$ADB" -s "$DEVICE" shell monkey -p "$BUNDLE_ID" -c android.intent.category.LAUNCHER 1 >/dev/null
echo "Done."
