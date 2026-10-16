using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterBSAvatar
{
    internal sealed class AvatarSettingsViewController : ViewController { }

    internal sealed class AvatarEditorMenu : MonoBehaviour, INotifyPropertyChanged
    {
        private BeatAvatarEditorFlowCoordinator _flow;
        private BeatAvatarEditorViewController _editor;
        private AvatarDataModel _model;
        private AvatarPartsModel _parts;
        private AvatarPresetStore _store;
        private SettingsMenuHost _settings;
        private GameObject _toolbar;
        private GameObject _dialogs;
        private AvatarData _originalDraft;
        private List<AvatarPreset> _presets = new List<AvatarPreset>();
        private int _index;
        private int _session;
        private string _errorMessage;
        private string _confirmationText;
        private AvatarAppearance _pendingAppearance;
        private Guid? _renameId;
        private Action _confirmationAction;
        private bool _disposed;

        [UIComponent("name-keyboard")] private ModalKeyboard _keyboard = null;
        [UIComponent("confirmation")] private ModalView _confirmation = null;
        [UIComponent("preset-name")] private TMP_Text _nameText = null;
        [UIComponent("previous-button")] private Button _previousButton = null;
        [UIComponent("next-button")] private Button _nextButton = null;
        [UIComponent("error-modal")] private ModalView _errorModal = null;
        [UIComponent("menu-body")] private RectTransform _menuBody = null;

        public event PropertyChangedEventHandler PropertyChanged;

        internal ViewController SettingsView { get; private set; }
        internal bool IsEditing { get; private set; }
        private AvatarPreset Selected => _index > 0 && _index <= _presets.Count ? _presets[_index - 1] : null;

        [UIValue("preset-name")]
        public string PresetName
        {
            get
            {
                AvatarPreset preset = Selected;
                if (preset == null) return "Current avatar";
                return preset.Name + (MatchesSelectedAppearance ? string.Empty : " *");
            }
        }

        private bool MatchesSelectedAppearance
        {
            get
            {
                if (Selected == null || _model?.avatarData == null) return false;
                try { return _model.avatarData.Equals(AvatarAppearanceMapper.Restore(Selected.Appearance, _parts)); }
                catch (Exception) { return false; /* Incompatible presets remain deletable/replaceable. */ }
            }
        }

        [UIValue("can-save")] public bool CanSave => IsEditing && _model?.avatarData != null && !MatchesSelectedAppearance;
        [UIValue("can-browse")] public bool CanBrowse => IsEditing && _model?.avatarData != null && _presets.Count > 0;
        [UIValue("can-delete")] public bool CanDelete => IsEditing && Selected != null;
        [UIValue("can-rename")] public bool CanRename => CanDelete;
        [UIValue("name-input")] public string NameInput { get; set; }
        [UIValue("confirmation-text")] public string ConfirmationText => _confirmationText;
        [UIValue("error-message")] public string ErrorMessage => _errorMessage;

        internal void Initialize(BeatAvatarEditorFlowCoordinator flow, BeatAvatarEditorViewController editor,
            AvatarDataModel model, AvatarPartsModel parts)
        {
            _flow = flow;
            _editor = editor;
            _model = model ?? throw new InvalidOperationException("The native avatar model is unavailable.");
            _parts = parts;
            _store = new AvatarPresetStore(Path.Combine(UnityGame.UserDataPath, "BetterBSAvatar", "Avatars"));
            _settings = new SettingsMenuHost();
            _settings.ResetDraft();

            SettingsView = BeatSaberUI.CreateViewController<AvatarSettingsViewController>();
            SettingsView.name = "BetterBSAvatar_Settings";
            BSMLParser.Instance.Parse(Resource("avatar-menu.bsml"), SettingsView.gameObject, this);
            if (_menuBody == null) throw new InvalidOperationException("The avatar menu scroll view could not be initialized.");
            BSMLParser.Instance.Parse(Resource("settings.bsml"), _menuBody.gameObject, _settings);

            _toolbar = new GameObject("BetterBSAvatar_Presets", typeof(RectTransform));
            RectTransform toolbarRect = (RectTransform)_toolbar.transform;
            toolbarRect.SetParent(_menuBody, false);
            toolbarRect.anchorMin = toolbarRect.anchorMax = new Vector2(0.5f, 0.5f);
            toolbarRect.sizeDelta = new Vector2(71, 76);
            toolbarRect.anchoredPosition = Vector2.zero;
            BSMLParser.Instance.Parse(Resource("avatar-presets.bsml"), _toolbar, this);

            // Center wide keyboards and confirmation dialogs on the main avatar screen.
            _dialogs = new GameObject("BetterBSAvatar_PresetDialogs", typeof(RectTransform));
            RectTransform dialogsRect = (RectTransform)_dialogs.transform;
            dialogsRect.SetParent(editor.transform, false);
            dialogsRect.anchorMin = dialogsRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogsRect.sizeDelta = new Vector2(160, 80);
            dialogsRect.anchoredPosition = Vector2.zero;
            BSMLParser.Instance.Parse(Resource("avatar-preset-dialogs.bsml"), _dialogs, this);
            if (_keyboard == null || _confirmation == null || _nameText == null || _previousButton == null || _nextButton == null || _errorModal == null)
                throw new InvalidOperationException("The avatar preset controls could not be initialized.");
            AddFullButtonHitArea(_previousButton);
            AddFullButtonHitArea(_nextButton);
            _nameText.richText = false;
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMin = 2.5f;
            _nameText.fontSizeMax = 4;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;
            Log.Debug("Saved avatars and display settings attached to the avatar editor right screen.");
        }

        internal void Begin()
        {
            if (_disposed) return;
            _session++;
            IsEditing = true;
            _originalDraft = _model.avatarData?.Clone();
            AvatarEditorHooks.BeginEditing(_model);
            _settings.Begin();
            RunOperation(() => ReloadLibrary(null, true));
        }

        internal void End()
        {
            IsEditing = false;
            _session++;
            _confirmationAction = null;
            _pendingAppearance = null;
            _renameId = null;
            _settings?.End();
            if (_keyboard != null && _keyboard.ModalView != null) _keyboard.ModalView.Hide(false);
            if (_confirmation != null) _confirmation.Hide(false);
            if (_errorModal != null) _errorModal.Hide(false);
            RefreshLabels();
        }

        internal void CommitSettings() => _settings?.Commit();

        private static void AddFullButtonHitArea(Button button)
        {
            // Keep full-button pointer coverage independent of the prefab's content graphics.
            GameObject hitArea = new GameObject("FullButtonHitArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            RectTransform rect = (RectTransform)hitArea.transform;
            rect.SetParent(button.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // Include the practice button's slanted background corners as well.
            rect.offsetMin = new Vector2(-1, 0);
            rect.offsetMax = new Vector2(1, 0);
            hitArea.GetComponent<LayoutElement>().ignoreLayout = true;
            Image image = hitArea.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
        }

        [UIAction("previous-avatar")] private void Previous() => Browse(-1);
        [UIAction("next-avatar")] private void Next() => Browse(1);

        private void Browse(int direction)
        {
            if (!CanBrowse) return;
            if (_index == 0) _originalDraft = _model.avatarData.Clone();
            _index = (_index + direction + _presets.Count + 1) % (_presets.Count + 1);
            RunOperation(() =>
            {
                AvatarData appearance = Selected == null ? _originalDraft?.Clone() : AvatarAppearanceMapper.Restore(Selected.Appearance, _parts);
                if (appearance == null) throw new InvalidOperationException("The avatar is still loading.");
                AvatarData previous = _model.avatarData.Clone();
                try
                {
                    _model.avatarData = appearance;
                    // Records the load as one whole-avatar edit, preserving native Undo/Redo.
                    AvatarEditorHooks.ReportAllChanged.Invoke(_editor, null);
                }
                catch
                {
                    _model.avatarData = previous;
                    _editor.RefreshUi();
                    throw;
                }
            });
            RefreshLabels();
        }

        [UIAction("save-avatar")]
        private void Save()
        {
            if (!CanSave) return;
            _renameId = null;
            _pendingAppearance = AvatarAppearanceMapper.Capture(_model.avatarData);
            NameInput = Selected?.Name ?? SuggestName();
            _keyboard.ModalView.Show(true);
        }

        [UIAction("rename-avatar")]
        private void Rename()
        {
            if (!CanRename) return;
            _pendingAppearance = null;
            _renameId = Selected.Id;
            NameInput = Selected.Name;
            _keyboard.ModalView.Show(true);
        }

        [UIAction("name-entered")]
        private void NameEntered(string value)
        {
            if (!IsEditing) return;
            RunOperation(() =>
            {
                string name = AvatarPresetStore.NormalizeName(value);
                if (_renameId.HasValue)
                {
                    AvatarPreset renamed = _store.Rename(_renameId.Value, name);
                    ReloadLibrary(renamed.Id);
                    _renameId = null;
                    return;
                }
                AvatarAppearance appearance = _pendingAppearance;
                if (appearance == null) return;
                _pendingAppearance = null;
                AvatarPreset existing = _store.ReadAll().FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    Confirm("Overwrite \"" + existing.Name + "\"?", () => SaveAppearance(name, appearance, existing.Id), true);
                }
                else SaveAppearance(name, appearance, null);
            });
        }

        private void SaveAppearance(string name, AvatarAppearance appearance, Guid? overwriteId)
        {
            AvatarPreset saved = _store.Save(name, appearance, overwriteId);
            ReloadLibrary(saved.Id);
            RefreshLabels();
        }

        [UIAction("delete-avatar")]
        private void Delete()
        {
            if (!CanDelete) return;
            Guid id = Selected.Id;
            Confirm("Delete \"" + Selected.Name + "\"?\nYour current appearance will stay unchanged.", () =>
            {
                _store.Delete(id);
                _originalDraft = _model.avatarData?.Clone();
                ReloadLibrary(null);
            });
        }

        private void Confirm(string message, Action action, bool afterKeyboard = false)
        {
            _confirmationText = message;
            _confirmationAction = action;
            RefreshLabels();
            if (afterKeyboard) StartCoroutine(ShowConfirmationNextFrame(_session));
            else _confirmation.Show(true);
        }

        private IEnumerator ShowConfirmationNextFrame(int session)
        {
            yield return null;
            if (IsEditing && _session == session) _confirmation.Show(true);
        }

        [UIAction("cancel-confirmation")]
        private void CancelConfirmation()
        {
            _confirmationAction = null;
            _confirmation.Hide(true);
        }

        [UIAction("confirm-operation")]
        private void ConfirmOperation()
        {
            Action action = _confirmationAction;
            _confirmationAction = null;
            _confirmation.Hide(true);
            if (IsEditing && action != null) RunOperation(action);
        }

        private void ReloadLibrary(Guid? selectedId, bool matchAppearance = false)
        {
            _presets = _store.ReadAll(warning => Log.Warn(warning));
            _index = 0;
            if (selectedId.HasValue) _index = _presets.FindIndex(preset => preset.Id == selectedId.Value) + 1;
            else if (matchAppearance && _model.avatarData != null)
            {
                for (int i = 0; i < _presets.Count; i++)
                {
                    try
                    {
                        if (_model.avatarData.Equals(AvatarAppearanceMapper.Restore(_presets[i].Appearance, _parts)))
                        { _index = i + 1; break; }
                    }
                    catch (Exception) { /* Keep incompatible entries available for browsing/deletion. */ }
                }
            }
        }

        private string SuggestName()
        {
            for (int number = 1; ; number++)
            {
                string name = "Avatar " + number;
                if (!_presets.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))) return name;
            }
        }

        private void RunOperation(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                _errorMessage = cause.Message;
                Log.Warn("Avatar preset operation failed: " + cause.Message);
                Log.Error(cause);
                StartCoroutine(ShowErrorNextFrame(_session));
            }
            RefreshLabels();
        }

        private IEnumerator ShowErrorNextFrame(int session)
        {
            yield return null;
            if (IsEditing && _session == session) _errorModal.Show(true);
        }

        [UIAction("dismiss-error")] private void DismissError() => _errorModal.Hide(true);

        internal void RefreshLabels()
        {
            if (_disposed) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PresetName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanBrowse)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanDelete)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRename)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConfirmationText)));
        }

        internal void DisposeMenu()
        {
            if (_disposed) return;
            _disposed = true;
            End();
            if (_flow != null && _flow.rightScreenViewController == SettingsView)
            {
                AvatarEditorHooks.SetRightScreen.Invoke(_flow, new object[] { null, ViewController.AnimationType.None });
            }
            if (_toolbar != null) Destroy(_toolbar);
            if (_dialogs != null) Destroy(_dialogs);
            if (SettingsView != null) Destroy(SettingsView.gameObject);
            AvatarEditorHooks.Forget(this);
        }

        private void OnDestroy() => DisposeMenu();

        private static string Resource(string filename) => Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BetterBSAvatar.Views." + filename);
    }
}
