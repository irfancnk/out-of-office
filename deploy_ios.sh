#!/bin/bash
# Builds the game and installs it on a connected iPhone. Works while the Unity editor is open.
# Usage: ./deploy_ios.sh
# Optional env: BUNDLE_ID (default com.irfancankaleli.outofoffice), TEAM_ID (default: your Personal Team in Xcode)
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")" && pwd)"
WORK="$HOME/Library/Caches/OutOfOfficeBuild"
UNITY="/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity"
BUNDLE_ID="${BUNDLE_ID:-com.irfancankaleli.outofoffice}"

fail() { echo "ERROR: $*" >&2; exit 1; }

# Reuse the team of the last successful deploy (an Apple ID can belong to several teams).
if [ -z "${TEAM_ID:-}" ] && [ -f "$HOME/Library/Caches/OutOfOfficeBuild/team" ]; then
  TEAM_ID="$(cat "$HOME/Library/Caches/OutOfOfficeBuild/team")"
fi
if [ -z "${TEAM_ID:-}" ]; then
  # Prefer the free Personal Team from the Apple ID added in Xcode > Settings > Accounts.
  TEAM_ID="$(defaults export com.apple.dt.Xcode - 2>/dev/null | python3 -I -c '
import plistlib, sys
try:
    prefs = plistlib.loads(sys.stdin.buffer.read())
except Exception:
    sys.exit()
teams = [t for ts in prefs.get("IDEProvisioningTeamByIdentifier", {}).values() for t in ts]
teams.sort(key=lambda t: not t.get("isFreeProvisioningTeam"))
if teams:
    print(teams[0]["teamID"])
')"
fi
[ -n "$TEAM_ID" ] || fail "No Apple ID team found. In Xcode: Settings > Accounts > add your Apple ID."

DEVICE_JSON="$WORK/devices.json"
mkdir -p "$WORK"
xcrun devicectl list devices --json-output "$DEVICE_JSON" >/dev/null 2>&1 || true
read -r DEVICE UDID <<<"$(python3 -I -c '
import json, sys
try:
    devices = json.load(open(sys.argv[1]))["result"]["devices"]
except Exception:
    devices = []
# Prefer a phone with a live connection; otherwise take a paired one (its connection wakes on first use).
phones = [d for d in devices if d.get("hardwareProperties", {}).get("platform") == "iOS"
          and d.get("hardwareProperties", {}).get("reality") == "physical"
          and d.get("connectionProperties", {}).get("pairingState") == "paired"]
phones.sort(key=lambda d: d.get("connectionProperties", {}).get("tunnelState") == "disconnected")
if phones:
    print(phones[0]["identifier"], phones[0]["hardwareProperties"].get("udid", ""))
' "$DEVICE_JSON")" || true
[ -n "$DEVICE" ] || fail "No connected iPhone found. Connect it by USB, unlock it, and tap Trust."

echo "Team $TEAM_ID, bundle $BUNDLE_ID, device $UDID"
xcrun devicectl device info details --device "$DEVICE" >/dev/null 2>&1 \
  || fail "iPhone is paired but not reachable. Unlock it and keep it connected by USB."

echo "[1/4] Copying project"
if [ ! -d "$WORK/project/Library" ]; then
  mkdir -p "$WORK/project"
  cp -cR "$PROJECT/Library" "$WORK/project/Library" 2>/dev/null || true  # fast APFS clone of the import cache
fi
rsync -a --delete --exclude Library --exclude Temp --exclude Logs --exclude UserSettings --exclude .git --exclude Builds \
  "$PROJECT/" "$WORK/project/"

echo "[2/4] Unity build (first run is slow: it imports for iOS)"
OOO_IOS_OUT="$WORK/xcode" OOO_BUNDLE_ID="$BUNDLE_ID" OOO_TEAM_ID="$TEAM_ID" \
  "$UNITY" -batchmode -quit -projectPath "$WORK/project" -buildTarget iOS \
  -executeMethod OutOfOffice.EditorTools.BuildIOS.Run -logFile "$WORK/unity.log" \
  || { grep -E "error|OOO" "$WORK/unity.log" | tail -20; fail "Unity build failed. Log: $WORK/unity.log"; }

echo "[3/4] Xcode build and sign"
xcodebuild -project "$WORK/xcode/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -destination "platform=iOS,id=$UDID" -derivedDataPath "$WORK/derived" \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration \
  DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic -quiet build \
  > "$WORK/xcode.log" 2>&1 || { grep -E "error" "$WORK/xcode.log" | tail -20; fail "Xcode build failed. Log: $WORK/xcode.log"; }
APP="$(find "$WORK/derived/Build/Products/Release-iphoneos" -maxdepth 1 -name "*.app" | head -1)"
[ -n "$APP" ] || fail "Built app not found"

echo "[4/4] Installing on iPhone"
xcrun devicectl device install app --device "$DEVICE" "$APP"
xcrun devicectl device process launch --device "$DEVICE" "$BUNDLE_ID" \
  || echo "Installed. If it does not open: iPhone Settings > General > VPN & Device Management > trust your developer account."
echo "$TEAM_ID" > "$WORK/team"
echo "Done."
