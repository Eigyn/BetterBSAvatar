using BeatSaberMarkupLanguage.Attributes;

namespace BetterBSAvatar
{
    internal sealed class SettingsMenuHost
    {
        [UIValue("enable-avatar")]
        public bool EnableAvatar
        {
            get => Plugin.Config.Enabled;
            set
            {
                Plugin.Config.Enabled = value;
                AvatarRuntimeProbe.Instance?.SetAvatarEnabled(value);
            }
        }

        [UIValue("show-first-person")]
        public bool ShowFirstPerson
        {
            get => !Plugin.Config.HideFromFirstPersonCamera;
            set
            {
                Plugin.Config.HideFromFirstPersonCamera = !value;
                AvatarRuntimeProbe.Instance?.ApplyVisualSettings();
            }
        }
    }
}
