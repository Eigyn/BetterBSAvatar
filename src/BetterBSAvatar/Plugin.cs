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

        [Init]
        public Plugin(IPALogger logger, Config config)
        {
            Log.Init(logger);
            Config = config.Generated<PluginConfig>();
            MigrateConfig();
            Log.Info("Initialized");
        }

        [OnEnable]
        public void OnEnable()
        {
            Log.Info("Enabled");
            AvatarSaveHooks.Install();
            CreateHost();
            AvatarRuntime.StartPluginCoroutine(SettingsRegistrar.RegisterWhenReady());
        }

        [OnDisable]
        public void OnDisable()
        {
            Log.Info("Disabled");
            AvatarSaveHooks.Uninstall();
            SettingsRegistrar.Unregister();
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

        private static void MigrateConfig()
        {
            if (Config.ConfigVersion < 1)
            {
                Config.RefreshCloneFromAvatarData = true;
                Config.ConfigVersion = 1;
                Log.Info("Migrated config defaults.");
            }

            if (Config.ConfigVersion < 2)
            {
                Config.ProbeOnly = false;
                Config.AutoCloneWhenPossible = true;
                Config.RefreshCloneFromAvatarData = true;
                Config.TrackPlayer = true;
                Config.ConfigVersion = 2;
                Log.Info("Migrated config for simplified tracked-avatar settings.");
            }
        }
    }
}
