using BeatSaber.AvatarCore;
using BeatSaber.BeatAvatarAdapter;
using BeatSaber.BeatAvatarSDK;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterBSAvatar
{
    internal sealed class AvatarRuntimeProbe : MonoBehaviour
    {
        private const float MenuAvatarSyncIntervalSeconds = 5.0f;
        private const float GameplayAvatarSyncIntervalSeconds = 20.0f;
        private const float SettingsReloadDelaySeconds = 0.75f;

        internal static AvatarRuntimeProbe Instance { get; private set; }
        internal static string LastStatus { get; private set; } = "Ready";

        private readonly AvatarCloneSpawner _spawner = new AvatarCloneSpawner();
        private float _nextAvatarSyncTime;
        private float _reloadTime;
        private int _probeCount;
        private bool _reloadScheduled;
        private string _reloadReason;

        private void OnEnable()
        {
            Instance = this;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Log.Info("Runtime enabled. Avatar clone will follow the Enable Avatar setting.");
            Probe("OnEnable");
            _nextAvatarSyncTime = 0.0f;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _spawner.DestroyClone();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!Plugin.Config.Enabled)
            {
                if (_spawner.HasClone)
                {
                    DestroyClone("disabled");
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                ScheduleCloneReload("F8", 0.0f);
            }

            if (_reloadScheduled && Time.unscaledTime >= _reloadTime)
            {
                ReloadClone(_reloadReason);
            }

            if (Time.unscaledTime >= _nextAvatarSyncTime)
            {
                _nextAvatarSyncTime = Time.unscaledTime + GetAvatarSyncIntervalSeconds();
                EnsureAvatarVisible("timer", ShouldRefreshAvatarDataOnTimer());
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Probe("sceneLoaded:" + scene.name);
            _spawner.InvalidateAvatarDataCache();
            _nextAvatarSyncTime = 0.0f;
            if (Plugin.Config.Enabled && !_spawner.HasClone)
            {
                ScheduleCloneReload("sceneLoaded:" + scene.name, SettingsReloadDelaySeconds);
            }
        }

        internal void Probe(string reason)
        {
            _probeCount++;
            try
            {
                int coreVisuals = Resources.FindObjectsOfTypeAll<AvatarVisualController>().Length;
                int sdkVisuals = Resources.FindObjectsOfTypeAll<BeatAvatarVisualController>().Length;
                int beatAvatars = Resources.FindObjectsOfTypeAll<BeatAvatar>().Length;
                int dataModels = AvatarDataModelFinder.CountReferences();

                string details =
                    $"Probe #{_probeCount} ({reason}): " +
                    $"coreVisuals={coreVisuals}, sdkVisuals={sdkVisuals}, " +
                    $"beatAvatars={beatAvatars}, avatarDataModels={dataModels}, " +
                    $"clone={_spawner.HasClone}";
                LastStatus =
                    $"Probe #{_probeCount}: sdk {sdkVisuals}, data {dataModels}, " +
                    $"clone {(_spawner.HasClone ? "on" : "off")}";
                Log.Info(details);
            }
            catch (Exception exception)
            {
                LastStatus = "Probe failed: " + exception.Message;
                Log.Error("Probe failed");
                Log.Error(exception);
            }
        }

        internal void TryClone(string reason)
        {
            try
            {
                bool cloned = _spawner.TryCloneExistingAvatar();
                LastStatus = cloned ? "Clone: success" : "Clone: no source avatar found";
                Log.Info(LastStatus);
            }
            catch (Exception exception)
            {
                LastStatus = "Clone attempt failed: " + exception.Message;
                Log.Error("Clone attempt failed");
                Log.Error(exception);
            }
        }

        internal void SetAvatarEnabled(bool enabled)
        {
            if (!enabled)
            {
                DestroyClone("settings-disabled");
                return;
            }

            ScheduleCloneReload("settings-enabled", SettingsReloadDelaySeconds);
        }

        internal void DestroyClone(string reason)
        {
            _spawner.DestroyClone();
            LastStatus = "Clone destroyed (" + reason + ")";
            Log.Info(LastStatus);
        }

        internal void ApplyConfiguredTransform()
        {
            _spawner.ApplyConfiguredTransformToClone();
        }

        internal void ApplyVisualSettings()
        {
            _spawner.ApplyVisualSettingsToClone();
        }

        private void EnsureAvatarVisible(string reason)
        {
            EnsureAvatarVisible(reason, true);
        }

        private void EnsureAvatarVisible(string reason, bool refreshAvatarData)
        {
            if (!_spawner.HasClone)
            {
                TryClone(reason);
                return;
            }

            if (_spawner.HasBrokenRendererMaterials())
            {
                ScheduleCloneReload("broken-materials", SettingsReloadDelaySeconds);
                return;
            }

            _spawner.ApplyConfiguredTransformToClone();
            if (refreshAvatarData)
            {
                _spawner.RefreshCloneFromAvatarData(reason);
            }
        }

        internal void RequestCloneReload(string reason)
        {
            ScheduleCloneReload(reason, SettingsReloadDelaySeconds);
        }

        private void ScheduleCloneReload(string reason, float delaySeconds)
        {
            if (!Plugin.Config.Enabled)
            {
                return;
            }

            _reloadScheduled = true;
            _reloadReason = reason;
            _reloadTime = Time.unscaledTime + Mathf.Max(0.0f, delaySeconds);
            LastStatus = "Clone reload scheduled (" + reason + ")";
            Log.Info(LastStatus);
        }

        private void ReloadClone(string reason)
        {
            _reloadScheduled = false;
            bool cloned = _spawner.TryReloadExistingAvatar();
            LastStatus = cloned
                ? "Clone reloaded (" + reason + ")"
                : "Clone reload found no source avatar (" + reason + ")";
            _nextAvatarSyncTime = Time.unscaledTime + GetAvatarSyncIntervalSeconds();
            Log.Info(LastStatus);
        }

        private static bool ShouldRefreshAvatarDataOnTimer()
        {
            return string.Equals(
                SceneManager.GetActiveScene().name,
                "MainMenu",
                StringComparison.OrdinalIgnoreCase);
        }

        private static float GetAvatarSyncIntervalSeconds()
        {
            return ShouldRefreshAvatarDataOnTimer()
                ? MenuAvatarSyncIntervalSeconds
                : GameplayAvatarSyncIntervalSeconds;
        }
    }
}
