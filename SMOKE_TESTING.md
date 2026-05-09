# Smoke Testing

These notes focus on first-load behavior and settings apply/cancel behavior.

## First Install With Existing Avatar Data

Use this test when changing clone creation or avatar data loading.

1. Close Beat Saber.
2. Install the new `BetterBSAvatar.dll` into the Beat Saber `Plugins` folder.
3. Remove the mod config if it exists:
   - `<Beat Saber install>\UserData\BetterBSAvatar.json`
4. Keep Beat Saber's saved avatar data in place:
   - `%USERPROFILE%\AppData\LocalLow\Hyperbolic Magnetism\Beat Saber\AvatarData.dat`
5. Launch Beat Saber and do not open the avatar editor.
6. Open the main menu and wait a few seconds.

Expected result:

- The broken default white-head avatar should never appear.
- If saved avatar data is still loading, no avatar should be shown until it is ready.
- Once the clone appears, it should match the saved built-in multiplayer avatar.
- The log may show that clone creation was delayed until saved avatar data was ready.

## No Saved Avatar Data

Use this test to make sure the mod does not show the broken default avatar for new users.

1. Close Beat Saber.
2. Back up `AvatarData.dat` if it exists.
3. Temporarily remove:
   - `%USERPROFILE%\AppData\LocalLow\Hyperbolic Magnetism\Beat Saber\AvatarData.dat`
4. Launch Beat Saber with BetterBSAvatar installed.

Expected result:

- The broken default avatar should not appear.
- The mod should wait for an avatar to be saved.
- After creating or editing an avatar and pressing Apply/OK in Beat Saber's avatar editor, the clone should appear from the saved avatar data.

Restore the backed-up `AvatarData.dat` after the test if needed.

## Settings Apply And Cancel

Use this test when changing `Enable Avatar` or `Show in First Person` behavior.

1. Open `Mod Settings > BetterBSAvatar`.
2. Toggle `Enable Avatar` or `Show in First Person`.
3. Press `Cancel`.

Expected result:

- The visible avatar state should not change.
- The previous saved settings should remain active.

Then repeat and press `OK`.

Expected result:

- Settings changes should apply only after `OK`.
- Enabling the avatar should create or reload the clone after saved avatar data is ready.
- Disabling the avatar should remove the clone.
- Changing `Show in First Person` should update avatar visibility after `OK`.
