using BeatSaber.BeatAvatarAdapter.AvatarEditor;
using BeatSaber.BeatAvatarSDK;
using HarmonyLib;
using HMUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BetterBSAvatar
{
    internal static class AvatarEditorHooks
    {
        private static readonly HashSet<AvatarEditorMenu> Menus = new HashSet<AvatarEditorMenu>();
        private static readonly Dictionary<AvatarDataModel, int> SaveVersions = new Dictionary<AvatarDataModel, int>();
        private static readonly FieldInfo EditorField = AccessTools.Field(typeof(BeatAvatarEditorFlowCoordinator), "_beatAvatarEditorViewController");
        private static readonly FieldInfo ModelField = AccessTools.Field(typeof(BeatAvatarEditorFlowCoordinator), "_avatarDataModel");
        private static readonly FieldInfo PartsField = AccessTools.Field(typeof(BeatAvatarEditorViewController), "_avatarPartsModel");
        internal static readonly MethodInfo ReportAllChanged = AccessTools.Method(typeof(BeatAvatarEditorViewController), "ReportAllChangedAndUpdate");
        internal static readonly MethodInfo SetRightScreen = AccessTools.Method(typeof(FlowCoordinator), "SetRightScreenViewController");

        private static Harmony _harmony;
        private static AvatarDataModel _displayModel;
        private static AvatarData _displayAppearance;

        internal static void Install()
        {
            if (_harmony != null) return;
            _harmony = new Harmony("Eigyn.BetterBSAvatar.AvatarEditor");
            try
            {
                if (EditorField == null || ModelField == null || PartsField == null || ReportAllChanged == null || SetRightScreen == null)
                    throw new MissingMemberException("Native avatar editor integration is unavailable in this game version.");
                Patch(typeof(FlowCoordinator), "ProvideInitialViewControllers", nameof(ProvideViewsPrefix), true);
                Patch(typeof(BeatAvatarEditorFlowCoordinator), "DidActivate", nameof(ActivatedPostfix));
                Patch(typeof(BeatAvatarEditorFlowCoordinator), "DidDeactivate", nameof(DeactivatedPostfix));
                Patch(typeof(BeatAvatarEditorFlowCoordinator), "HandleBeatAvatarEditorViewCancelButtonWasPressed", nameof(CanceledPostfix));
                Patch(typeof(BeatAvatarEditorFlowCoordinator), "HandleBeatAvatarEditorViewOkButtonWasPressed", nameof(AppliedPostfix));
                // Individual part/color edits update buttons without calling RefreshUi.
                Patch(typeof(BeatAvatarEditorViewController), "UpdateButtons", nameof(RefreshedPostfix));
                Patch(typeof(BeatAvatarEditorViewController), "HandleApplyButtonWasPressed", nameof(ApplyPrefix), true);
                Log.Info("Installed native avatar menu integration.");
            }
            catch (Exception exception)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
                Log.Error("Could not install the native avatar menu.");
                Log.Error(exception);
            }
        }

        internal static void Uninstall()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            foreach (AvatarEditorMenu menu in new List<AvatarEditorMenu>(Menus))
            {
                if (menu != null) { menu.DisposeMenu(); UnityEngine.Object.Destroy(menu); }
            }
            Menus.Clear();
            SaveVersions.Clear();
            _displayModel = null;
            _displayAppearance = null;
        }

        internal static void Forget(AvatarEditorMenu menu) => Menus.Remove(menu);

        internal static AvatarData GetDisplayAvatarData(AvatarDataModel model)
        {
            return ReferenceEquals(model, _displayModel) && _displayAppearance != null ? _displayAppearance : model.avatarData;
        }

        internal static void BeginEditing(AvatarDataModel model)
        {
            if (!ReferenceEquals(model, _displayModel))
            {
                _displayModel = model;
                _displayAppearance = model.avatarData?.Clone();
            }
        }

        internal static int SaveStarted(AvatarDataModel model)
        {
            int previous;
            SaveVersions.TryGetValue(model, out previous);
            SaveVersions[model] = previous + 1;
            return previous + 1;
        }

        internal static bool SaveCompleted(AvatarDataModel model, AvatarData appearance, int version)
        {
            int latest;
            if (!SaveVersions.TryGetValue(model, out latest) || latest != version) return false;
            _displayModel = model;
            _displayAppearance = appearance?.Clone();
            return true;
        }

        private static void Patch(Type type, string method, string callback, bool prefix = false)
        {
            MethodInfo original = AccessTools.Method(type, method);
            if (original == null) throw new MissingMethodException(type.FullName, method);
            HarmonyMethod patch = new HarmonyMethod(AccessTools.Method(typeof(AvatarEditorHooks), callback));
            if (prefix) _harmony.Patch(original, prefix: patch);
            else _harmony.Patch(original, postfix: patch);
        }

        private static AvatarEditorMenu Find(BeatAvatarEditorFlowCoordinator flow, bool create)
        {
            BeatAvatarEditorViewController editor = EditorField.GetValue(flow) as BeatAvatarEditorViewController;
            if (editor == null) return null;
            AvatarEditorMenu menu = editor.GetComponent<AvatarEditorMenu>();
            if (menu == null && create)
            {
                menu = editor.gameObject.AddComponent<AvatarEditorMenu>();
                try
                {
                    menu.Initialize(flow, editor, (AvatarDataModel)ModelField.GetValue(flow), (AvatarPartsModel)PartsField.GetValue(editor));
                    Menus.Add(menu);
                }
                catch
                {
                    menu.DisposeMenu();
                    UnityEngine.Object.Destroy(menu);
                    throw;
                }
            }
            return menu;
        }

        private static void ProvideViewsPrefix(FlowCoordinator __instance, ref ViewController __2)
        {
            BeatAvatarEditorFlowCoordinator flow = __instance as BeatAvatarEditorFlowCoordinator;
            if (flow == null) return;
            try
            {
                AvatarEditorMenu menu = Find(flow, true);
                if (menu == null) return;
                if (__2 == null || __2 == menu.SettingsView) __2 = menu.SettingsView;
                else Log.Warn("Another mod owns the avatar editor right screen; its view was preserved.");
            }
            catch (Exception exception) { Log.Error("Could not create the avatar menu."); Log.Error(exception); }
        }

        private static void ActivatedPostfix(BeatAvatarEditorFlowCoordinator __instance, bool __1)
        {
            try
            {
                AvatarEditorMenu menu = Find(__instance, true);
                if (menu != null && (__1 || !menu.IsEditing)) menu.Begin();
            }
            catch (Exception exception) { Log.Error(exception); }
        }

        private static void DeactivatedPostfix(BeatAvatarEditorFlowCoordinator __instance, bool __0)
        {
            if (__0) Find(__instance, false)?.End();
        }

        private static void CanceledPostfix(BeatAvatarEditorFlowCoordinator __instance) => Find(__instance, false)?.End();
        private static void AppliedPostfix(BeatAvatarEditorFlowCoordinator __instance) => Find(__instance, false)?.End();

        private static void ApplyPrefix(BeatAvatarEditorViewController __instance)
        {
            __instance.GetComponent<AvatarEditorMenu>()?.CommitSettings();
        }

        private static void RefreshedPostfix(BeatAvatarEditorViewController __instance)
        {
            __instance.GetComponent<AvatarEditorMenu>()?.RefreshLabels();
        }
    }
}
