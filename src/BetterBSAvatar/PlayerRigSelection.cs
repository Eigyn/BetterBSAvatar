using System;

namespace BetterBSAvatar
{
    // Keep the selection and cache rules independent of Unity so scene transitions can be tested.
    internal static class PlayerRigSelection
    {
        internal static bool IsGameplayScene(string sceneName)
        {
            return string.Equals(sceneName, "GameCore", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sceneName, "StandardGameplay", StringComparison.OrdinalIgnoreCase);
        }

        internal static int GetPriority(
            bool gameplaySceneLoaded,
            bool activeAndEnabled,
            bool sceneLoaded,
            string sceneName,
            bool tracksLocalPlayer,
            bool matchesCurrentCamera = false)
        {
            if (!gameplaySceneLoaded || !activeAndEnabled || !sceneLoaded || !tracksLocalPlayer)
            {
                return 0;
            }

            return (IsGameplayScene(sceneName) ? 4 : 2) + (matchesCurrentCamera ? 1 : 0);
        }

        internal static bool IsLocalRig(
            bool headContainsCamera,
            bool originContainsCamera,
            bool hasLocalCamera,
            bool connectedPlayer)
        {
            // Replays can replace the head and disable Camera.main while retaining the local camera rig.
            return !connectedPlayer && (headContainsCamera || originContainsCamera || hasLocalCamera);
        }

        internal static bool ShouldScan(
            bool hadCachedRig,
            bool cachedRigUsable,
            bool cameraChanged,
            float now,
            float nextScanTime)
        {
            return cameraChanged || (hadCachedRig && !cachedRigUsable) || now >= nextScanTime;
        }
    }
}
