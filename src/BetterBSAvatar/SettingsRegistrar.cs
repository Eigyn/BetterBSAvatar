using BeatSaberMarkupLanguage.Settings;
using System;
using System.Collections;
using UnityEngine.SceneManagement;

namespace BetterBSAvatar
{
    internal static class SettingsRegistrar
    {
        private const string MenuName = "BetterBSAvatar";
        private const string SettingsResource = "BetterBSAvatar.Views.settings.bsml";

        private static SettingsMenuHost _host;
        private static BSMLSettings _registeredSettings;
        private static bool _watching;

        internal static IEnumerator RegisterWhenReady()
        {
            if (_watching)
            {
                yield break;
            }

            _watching = true;
            _host = _host ?? new SettingsMenuHost();
            while (_watching)
            {
                if (SceneManager.GetActiveScene().name == "MainMenu")
                {
                    for (int frame = 0; frame < 120 && _watching; frame++)
                    {
                        yield return null;
                    }

                    TryRegisterForCurrentSettings();
                }

                for (int frame = 0; frame < 30 && _watching; frame++)
                {
                    yield return null;
                }
            }
        }

        internal static void Unregister()
        {
            _watching = false;
            if (_registeredSettings == null || _host == null)
            {
                return;
            }

            try
            {
                _registeredSettings.RemoveSettingsMenu(_host);
            }
            catch (Exception exception)
            {
                Log.Warn("Failed to unregister BSML settings menu.");
                Log.Error(exception);
            }

            _registeredSettings = null;
        }

        private static void TryRegisterForCurrentSettings()
        {
            BSMLSettings settings = GetSettingsInstance();
            if (settings == null || ReferenceEquals(settings, _registeredSettings))
            {
                return;
            }

            settings.AddSettingsMenu(MenuName, SettingsResource, _host);
            _registeredSettings = settings;
            Log.Info("Registered BSML settings menu.");
        }

        private static BSMLSettings GetSettingsInstance()
        {
            try
            {
                return BSMLSettings.Instance;
            }
            catch
            {
                return null;
            }
        }
    }
}
