# Releasing Out of Office

The checklist for a Google Play release (OUT-54). Items marked **you** need the owner's accounts or keys; the rest is
already in the repository.

## Ready in the repository

- **Release build:** `OOO_RELEASE=1 ./deploy_android.sh` writes `Builds/Android/OutOfOffice.aab`. It is:
  - an Android App Bundle, IL2CPP, ARM64, release configuration, never a development build;
  - version `1.0.0` (`BuildAndroid.Version`, or `OOO_VERSION`), with a version code from the UTC date and hour (`yyMMddHH`, or `OOO_VERSION_CODE`), so each build counts up;
  - signed with your upload key, read from environment variables only (below).

  The build stops with a list of anything missing instead of shipping test ids.
- **Store-safe behaviour in release builds:**
  - The developer tools are hidden and can't be unlocked.
  - Test ads and test purchases are development-only.
  - The Unity splash is off, and the game opens on its own boot screen.
- **Legal pages:** `site/src/index.js` serves `/privacy`, `/terms` and `/support` on gotobed.win. Settings links to the first two.
- **Checks:** **Out of Office > Run All Checks** (or `AllChecks.Run` in batch mode) must pass with 0 failures before every release.

## What only you can provide

1. **Upload key (you, once).** Run this yourself; it asks for the passwords, which should never be typed into chat or committed:

   ```
   keytool -genkeypair -v -keystore ~/keys/outofoffice-upload.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000
   ```

   Keep the `.jks` file and both passwords in a password manager, outside the repository. Before a release build:

   ```
   export OOO_KEYSTORE=~/keys/outofoffice-upload.jks OOO_KEY_ALIAS=upload
   export OOO_KEYSTORE_PASS=... OOO_KEY_PASS=...
   ```

2. **Play Console app (you).**
   - Create the app with package `com.irfancankaleli.outofoffice`.
   - Turn on Play App Signing.
   - Upload the first `.aab` to **Internal testing**.
3. **AdMob (you).**
   - Create the Android app and two rewarded ad units (`continue`, `shop_gems`).
   - Put the ids in `Assets/OutOfOffice/Scripts/AdMobConfig.cs`.
   - Add `app-ads.txt` to the site with the line AdMob gives you.
4. **In-app purchases (you, then code).** Release builds show the shop as "coming soon" until a real store provider is wired in.
   - Create these products in Play Console: `ooo.gems.80`, `ooo.gems.500`, `ooo.gems.1200`, `ooo.starter`, `ooo.noads`, and the subscription `ooo.pass.monthly`.
   - Then add Unity IAP (or Play Billing) as an `IStoreProvider` (`Scripts/Store.cs`).
5. **Store listing (you).**
   - Title, short and full description, and the 512 px icon (`Assets/OutOfOffice/Icon`).
   - Phone screenshots (**Out of Office > Render Review Shots** writes them) and a feature graphic.
   - The privacy policy URL `https://gotobed.win/privacy`.
6. **Policy forms (you).**
   - **Content rating questionnaire.**
   - **Target audience:** 13+. The game is not made for children, which matches the privacy policy.
   - **Data safety:**
     - Ads collect the device or other IDs and approximate location through the IP address.
     - Purchases go through Google Play.
     - No data is collected by the developer.
     - Data is not shared beyond Google's ad services.
7. **Publish the site (you).** `cd site && npm install && npx wrangler login && npx wrangler deploy`.

## Every release

1. Run all checks: 0 failures.
2. Play the new build on a phone (`./deploy_android.sh`): the orientation room on a fresh install, a few rooms, the shop and an ad.
3. If the release is not 1.0.0, set `OOO_VERSION`.
4. Build: `OOO_RELEASE=1 ./deploy_android.sh`.
5. Upload the `.aab` to internal testing, then promote it to production once it looks right.
