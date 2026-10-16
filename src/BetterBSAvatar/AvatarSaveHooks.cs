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
        private static int _generation;

        private sealed class SaveSnapshot
        {
            internal AvatarDataModel Model;
            internal AvatarData Appearance;
            internal int Version;
            internal int Generation;
        }

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
            MethodInfo prefix = AccessTools.Method(typeof(AvatarSaveHooks), nameof(AvatarDataModelSaveAsyncPrefix));

            if (original == null || postfix == null || prefix == null)
            {
                Log.Warn("Could not install avatar save hook.");
                return;
            }

            _harmony = new Harmony(HarmonyId);
            _generation++;
            _harmony.Patch(original, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
            Log.Info("Installed avatar save hook.");
        }

        internal static void Uninstall()
        {
            if (_harmony == null)
            {
                return;
            }

            _harmony.UnpatchSelf();
            _generation++;
            _harmony = null;
            Log.Info("Uninstalled avatar save hook.");
        }

        private static void AvatarDataModelSaveAsyncPrefix(AvatarDataModel __instance, out SaveSnapshot __state)
        {
            __state = new SaveSnapshot
            {
                Model = __instance,
                Appearance = __instance.avatarData?.Clone(),
                Version = AvatarEditorHooks.SaveStarted(__instance),
                Generation = _generation
            };
        }

        private static void AvatarDataModelSaveAsyncPostfix(Task __result, SaveSnapshot __state)
        {
            AvatarRuntime.StartPluginCoroutine(RefreshWhenSaveCompletes(__result, __state));
        }

        private static IEnumerator RefreshWhenSaveCompletes(Task saveTask, SaveSnapshot snapshot)
        {
            while (saveTask != null && !saveTask.IsCompleted)
            {
                yield return null;
            }

            if (_harmony == null || snapshot.Generation != _generation) yield break;

            if (saveTask == null || saveTask.IsCanceled || saveTask.IsFaulted)
            {
                Log.Warn("Avatar save failed; clone refresh skipped.");
                if (saveTask?.Exception != null)
                {
                    Log.Error(saveTask.Exception);
                }

                yield break;
            }

            Task<bool> resultTask = saveTask as Task<bool>;
            if (resultTask != null && !resultTask.Result) yield break;
            if (!AvatarEditorHooks.SaveCompleted(snapshot.Model, snapshot.Appearance, snapshot.Version)) yield break;

            AvatarRuntimeProbe probe = AvatarRuntimeProbe.Instance;
            if (probe == null)
            {
                yield break;
            }

            try
            {
                probe.RefreshAfterAvatarSave(snapshot.Model);
            }
            catch (Exception exception)
            {
                Log.Warn("Avatar save hook could not refresh the clone.");
                Log.Error(exception);
            }
        }
    }
}
