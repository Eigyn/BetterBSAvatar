using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Logging;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace BetterBSAvatar
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed class Plugin
    {
        private GameObject _host;

        internal static PluginConfig Config { get; private set; }
        internal static bool AvatarEnabled => SettingsMenuHost.Active?.EnableAvatar ?? Config.Enabled;
        internal static bool HideFromFirstPersonCamera => SettingsMenuHost.Active != null
            ? !SettingsMenuHost.Active.ShowFirstPerson : Config.HideFromFirstPersonCamera;

        [Init]
        public Plugin(IPALogger logger, Config config)
        {
            Log.Init(logger);
            Config = config.Generated<PluginConfig>();
            Log.Info("Initialized");
        }

        [OnEnable]
        public void OnEnable()
        {
            Log.Info("Enabled");
            AvatarSaveHooks.Install();
            AvatarEditorHooks.Install();
            CreateHost();
        }

        [OnDisable]
        public void OnDisable()
        {
            Log.Info("Disabled");
            AvatarSaveHooks.Uninstall();
            AvatarEditorHooks.Uninstall();
            AvatarRuntime.Stop();
            if (_host != null)
            {
                Object.Destroy(_host);
                _host = null;
            }
        }

        private void CreateHost()
        {
            if (_host != null)
            {
                return;
            }

            _host = new GameObject("BetterBSAvatar");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<AvatarRuntimeProbe>();
        }
    }
}
