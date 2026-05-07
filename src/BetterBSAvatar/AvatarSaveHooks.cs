using BeatSaber.BeatAvatarSDK;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace BetterBSAvatar
{
    internal static class AvatarSaveHooks
    {
        private const string HarmonyId = "Eigyn.BetterBSAvatar.AvatarSaveHooks";

        private static Harmony _harmony;

        internal static void Install()
        {
            if (_harmony != null)
            {
                return;
            }

            MethodInfo original = typeof(AvatarDataModel).GetMethod(
                nameof(AvatarDataModel.SaveAsync),
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo postfix = typeof(AvatarSaveHooks).GetMethod(
                nameof(AvatarDataModelSaveAsyncPostfix),
                BindingFlags.Static | BindingFlags.NonPublic);

            if (original == null || postfix == null)
            {
                Log.Warn("Could not install avatar save hook.");
                return;
            }

            _harmony = new Harmony(HarmonyId);
            _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            Log.Info("Installed avatar save hook.");
        }

        internal static void Uninstall()
        {
            if (_harmony == null)
            {
                return;
            }

            _harmony.UnpatchSelf();
            _harmony = null;
            Log.Info("Uninstalled avatar save hook.");
        }

        private static void AvatarDataModelSaveAsyncPostfix(Task __result)
        {
            AvatarRuntime.StartPluginCoroutine(RefreshWhenSaveCompletes(__result));
        }

        private static IEnumerator RefreshWhenSaveCompletes(Task saveTask)
        {
            while (saveTask != null && !saveTask.IsCompleted)
            {
                yield return null;
            }

            if (saveTask != null && saveTask.IsFaulted)
            {
                Log.Warn("Avatar save failed; clone refresh skipped.");
                if (saveTask.Exception != null)
                {
                    Log.Error(saveTask.Exception);
                }

                yield break;
            }

            AvatarRuntimeProbe probe = AvatarRuntimeProbe.Instance;
            if (probe == null)
            {
                yield break;
            }

            try
            {
                probe.RefreshAfterAvatarSave();
            }
            catch (Exception exception)
            {
                Log.Warn("Avatar save hook could not refresh the clone.");
                Log.Error(exception);
            }
        }
    }
}
