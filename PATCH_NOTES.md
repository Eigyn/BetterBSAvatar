# Patch Notes

## v0.2.0

- Added named appearance presets with previous/next arrows, Save, Delete, rename, and confirmed overwrites in the native Avatar editor.
- Added a transparent BetterBSAvatar window on the right of the native editor, with a matching blue title header and Saved Avatars above Display Settings.
- Moved the preset name/arrows and Save/Delete controls into Saved Avatars and restored the native appearance editor's original spacing. Naming and confirmation dialogs remain centered on the main screen.
- Added a standard BSML scrollbar for both right-window sections, with a fixed blue title header.
- Settings preview immediately; the native Apply/Create Avatar button saves them and Cancel restores the previous settings.
- Expanded the avatar arrow hitboxes and placed the selector inside the native view's interactive bounds.
- Save stays gray for unchanged saved avatars and updates as appearance edits are made or undone.
- Removed the preset status footer and increased spacing around the selector and Save/Delete controls. Operation errors use a dialog.
- Preset loading participates in native Undo/Redo and keeps the game's Apply/Cancel behavior.
- Presets use separate versioned JSON files with backups for updates and recovery from unreadable entries.
- The displayed clone uses committed appearance data while editing; failed or canceled avatar saves do not refresh it.

## v0.1.1

- Attempted to fix broken source avatar loading on first install.
- Settings updates are now only accepted after the OK button is pressed.
