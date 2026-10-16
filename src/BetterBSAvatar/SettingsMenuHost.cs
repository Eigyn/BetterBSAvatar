using BeatSaberMarkupLanguage.Attributes;

using System.ComponentModel;

namespace BetterBSAvatar
{
    internal sealed class SettingsMenuHost : INotifyPropertyChanged
    {
        private bool _enableAvatar;
        private bool _showFirstPerson;
        internal static SettingsMenuHost Active { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [UIValue("enable-avatar")]
        public bool EnableAvatar
        {
            get => _enableAvatar;
            set
            {
                if (_enableAvatar == value) return;
                bool wasEnabled = Plugin.AvatarEnabled;
                bool wasHidden = Plugin.HideFromFirstPersonCamera;
                _enableAvatar = value;
                if (Active == this) RefreshPreview(wasEnabled, wasHidden);
                Changed();
            }
        }

        [UIValue("show-first-person")]
        public bool ShowFirstPerson
        {
            get => _showFirstPerson;
            set
            {
                if (_showFirstPerson == value) return;
                bool wasEnabled = Plugin.AvatarEnabled;
                bool wasHidden = Plugin.HideFromFirstPersonCamera;
                _showFirstPerson = value;
                if (Active == this) RefreshPreview(wasEnabled, wasHidden);
                Changed();
            }
        }

        internal void ResetDraft()
        {
            _enableAvatar = Plugin.Config.Enabled;
            _showFirstPerson = !Plugin.Config.HideFromFirstPersonCamera;
            Changed();
        }

        internal void Begin()
        {
            Active?.End();
            ResetDraft();
            Active = this;
        }

        internal void Commit()
        {
            if (Active != this) return;
            // Persist only on the native Apply/Create Avatar click, before dismissal callbacks.
            Plugin.Config.Enabled = _enableAvatar;
            Plugin.Config.HideFromFirstPersonCamera = !_showFirstPerson;
            Active = null;
        }

        internal void End()
        {
            if (Active == this)
            {
                bool wasEnabled = Plugin.AvatarEnabled;
                bool wasHidden = Plugin.HideFromFirstPersonCamera;
                Active = null;
                RefreshPreview(wasEnabled, wasHidden);
            }
            ResetDraft();
        }

        private static void RefreshPreview(bool wasEnabled, bool wasHidden)
        {
            if (wasEnabled != Plugin.AvatarEnabled) AvatarRuntimeProbe.Instance?.SetAvatarEnabled(Plugin.AvatarEnabled);
            if (wasHidden != Plugin.HideFromFirstPersonCamera) AvatarRuntimeProbe.Instance?.ApplyVisualSettings();
        }

        private void Changed()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableAvatar)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFirstPerson)));
        }
    }
}
