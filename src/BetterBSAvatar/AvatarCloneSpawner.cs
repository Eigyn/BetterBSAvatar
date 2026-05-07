using BeatSaber.BeatAvatarAdapter;
using BeatSaber.BeatAvatarSDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BetterBSAvatar
{
    internal sealed class AvatarCloneSpawner
    {
        private const string CloneName = "BetterBSAvatar_BeatAvatarClone";
        private const int EditorPreviewPenalty = 30000;
        private const int LiveObjectBonus = 50000;
        private const int LoadedSceneBonus = 10000;
        private const int MultiplayerPathBonus = 20000;
        private const int BeatAvatarBonus = 100000;

        private static readonly FieldInfo PoseLeftHandTransformField = typeof(BeatAvatarPoseController).GetField(
            "_leftHandTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo PoseRightHandTransformField = typeof(BeatAvatarPoseController).GetField(
            "_rightHandTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo VisualLeftHandMeshFilterField = typeof(BeatAvatarVisualController).GetField(
            "_leftHandsHairMeshFilter",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo VisualRightHandMeshFilterField = typeof(BeatAvatarVisualController).GetField(
            "_rightHandsHairMeshFilter",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static AvatarHandTransformSnapshot _handTransformBaseline;

        private GameObject _clone;
        private AvatarDataModel _avatarDataModel;
        private AvatarDataModel _subscribedAvatarDataModel;
        private BeatAvatarVisualController _visualController;
        private readonly Action<AvatarData> _avatarDataChangedHandler;
        private bool _refreshRunning;

        internal AvatarCloneSpawner()
        {
            _avatarDataChangedHandler = HandleAvatarDataChanged;
        }

        internal bool HasClone => _clone != null;

        internal void ApplyConfiguredTransformToClone()
        {
            if (_clone != null)
            {
                ApplyPlacementMode(_clone);
            }
        }

        internal bool TryCloneExistingAvatar()
        {
            if (_clone != null)
            {
                ApplyPlacementMode(_clone);
                PrepareCloneForViewing(_clone, false);
                return true;
            }

            return TryCreateCloneFromBestSource();
        }

        internal bool TryReloadExistingAvatar()
        {
            return TryCreateCloneFromBestSource();
        }

        private bool TryCreateCloneFromBestSource()
        {
            GameObject source = FindBestSourceAvatar();
            if (source == null)
            {
                return false;
            }

            GameObject oldClone = _clone;
            GameObject clone = UnityEngine.Object.Instantiate(source);
            _clone = clone;
            UnsubscribeAvatarDataModel();
            _avatarDataModel = null;
            _visualController = null;
            _refreshRunning = false;
            _clone.name = CloneName;
            UnityEngine.Object.DontDestroyOnLoad(_clone);
            EnsureTrackingDriver(_clone);
            CaptureHandTransformBaselineIfNeeded(_clone);
            RestoreHandTransformBaseline(_clone);
            ApplyPlacementMode(_clone);
            PrepareCloneForViewing(_clone, true);
            Log.Info($"Clone source: {DescribeGameObject(source)}");
            RefreshCloneFromAvatarData("clone");

            if (oldClone != null)
            {
                UnityEngine.Object.Destroy(oldClone);
            }

            return true;
        }

        internal void ApplyVisualSettingsToClone()
        {
            if (_clone != null)
            {
                PrepareCloneForViewing(_clone, false);
            }
        }

        internal void InvalidateAvatarDataCache()
        {
            UnsubscribeAvatarDataModel();
            _avatarDataModel = null;
        }

        internal bool HasBrokenRendererMaterials()
        {
            if (_clone == null)
            {
                return false;
            }

            foreach (Renderer renderer in _clone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    continue;
                }

                foreach (Material material in materials)
                {
                    if (IsBrokenMaterial(material))
                    {
                        Log.Warn($"Clone renderer has a broken material shader: {GetPath(renderer.transform)}");
                        return true;
                    }
                }
            }

            return false;
        }

        internal void RefreshCloneFromAvatarData(string reason)
        {
            if (_clone == null || _refreshRunning)
            {
                return;
            }

            AvatarDataModel dataModel = GetAvatarDataModel();
            BeatAvatarVisualController visualController = GetVisualController();
            if (dataModel == null || visualController == null)
            {
                if (reason != "timer")
                {
                    Log.Warn("Clone exists, but avatar data/model visual controller was not available for refresh.");
                }

                return;
            }

            _refreshRunning = true;
            try
            {
                RefreshVisualFromAvatarData(dataModel, visualController, dataModel.avatarData, reason);
            }
            finally
            {
                _refreshRunning = false;
            }
        }

        internal void DestroyClone()
        {
            if (_clone == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_clone);
            _clone = null;
            UnsubscribeAvatarDataModel();
            _avatarDataModel = null;
            _visualController = null;
            _refreshRunning = false;
        }

        private static GameObject FindBestSourceAvatar()
        {
            List<SourceCandidate> candidates = BuildSourceCandidates()
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.RendererCount)
                .ToList();

            LogCandidateSummary(candidates);

            SourceCandidate selected = candidates.FirstOrDefault();
            return selected != null ? selected.GameObject : null;
        }

        private static bool IsUsableSource(Component component)
        {
            if (component == null || component.gameObject == null)
            {
                return false;
            }

            if (component.gameObject.name == CloneName)
            {
                return false;
            }

            if (component.transform != null &&
                component.transform.root != null &&
                component.transform.root.name == CloneName)
            {
                return false;
            }

            Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
            return renderers.Any(renderer => renderer != null);
        }

        private AvatarDataModel GetAvatarDataModel()
        {
            if (_avatarDataModel == null)
            {
                _avatarDataModel = AvatarDataModelFinder.FindFirst();
                SubscribeToAvatarDataModel(_avatarDataModel);
            }

            return _avatarDataModel;
        }

        private BeatAvatarVisualController GetVisualController()
        {
            if (_visualController == null && _clone != null)
            {
                _visualController = _clone.GetComponentInChildren<BeatAvatarVisualController>(true);
            }

            return _visualController;
        }

        private static IEnumerable<SourceCandidate> BuildSourceCandidates()
        {
            HashSet<GameObject> seen = new HashSet<GameObject>();

            foreach (BeatAvatar beatAvatar in Resources.FindObjectsOfTypeAll<BeatAvatar>())
            {
                if (!IsUsableSource(beatAvatar) || !seen.Add(beatAvatar.gameObject))
                {
                    continue;
                }

                yield return SourceCandidate.Create(beatAvatar, "BeatAvatar");
            }

            foreach (BeatAvatarVisualController visualController in Resources.FindObjectsOfTypeAll<BeatAvatarVisualController>())
            {
                if (!IsUsableSource(visualController) || !seen.Add(visualController.gameObject))
                {
                    continue;
                }

                yield return SourceCandidate.Create(visualController, "BeatAvatarVisualController");
            }
        }

        private static void LogCandidateSummary(List<SourceCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                Log.Info("Clone candidates: none");
                return;
            }

            string summary = string.Join(
                " | ",
                candidates
                    .Take(5)
                    .Select(candidate =>
                        $"{candidate.Kind} score={candidate.Score} renderers={candidate.RendererCount} " +
                        $"active={candidate.ActiveInHierarchy} scene={candidate.SceneName} path={candidate.Path}"));
            Log.Info("Clone candidates: " + summary);
        }

        private static string DescribeGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "<null>";
            }

            int renderers = gameObject.GetComponentsInChildren<Renderer>(true).Length;
            int avatarRenderers = CollectAvatarVisualRenderers(gameObject).Count;
            int sdkVisuals = gameObject.GetComponentsInChildren<BeatAvatarVisualController>(true).Length;
            int beatAvatars = gameObject.GetComponentsInChildren<BeatAvatar>(true).Length;
            return
                $"{GetPath(gameObject.transform)} active={gameObject.activeInHierarchy} " +
                $"scene={GetSceneName(gameObject)} renderers={renderers} avatarRenderers={avatarRenderers} " +
                $"sdkVisuals={sdkVisuals} beatAvatars={beatAvatars}";
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void ApplyConfiguredTransform(Transform transform)
        {
            PluginConfig config = Plugin.Config;
            transform.SetParent(null, false);
            transform.gameObject.SetActive(true);
            transform.position = config.MenuPosition;
            transform.rotation = Quaternion.Euler(0.0f, config.MenuYawDegrees, 0.0f);
            transform.localScale = Vector3.one * config.MenuScale;
        }

        private static void ApplyPlacementMode(GameObject clone)
        {
            if (clone == null)
            {
                return;
            }

            AvatarTrackedPoseDriver trackingDriver = EnsureTrackingDriver(clone);
            trackingDriver.enabled = Plugin.Config.TrackPlayer;
            if (!Plugin.Config.TrackPlayer)
            {
                ApplyConfiguredTransform(clone.transform);
            }
        }

        private static AvatarTrackedPoseDriver EnsureTrackingDriver(GameObject clone)
        {
            AvatarTrackedPoseDriver trackingDriver = clone.GetComponent<AvatarTrackedPoseDriver>();
            if (trackingDriver == null)
            {
                trackingDriver = clone.AddComponent<AvatarTrackedPoseDriver>();
            }

            trackingDriver.Initialize(clone);
            return trackingDriver;
        }

        private static void PrepareCloneForViewing(GameObject avatar, bool log)
        {
            DestroyExternalPoseDrivers(avatar, log);
            PrepareRenderers(avatar, log);
            DisableCloneInteractions(avatar, log);
            DisableRuntimeBehaviours(avatar, log);
        }

        private static void DestroyExternalPoseDrivers(GameObject avatar, bool log)
        {
            int destroyed = 0;
            foreach (BeatAvatar beatAvatar in avatar.GetComponentsInChildren<BeatAvatar>(true))
            {
                if (beatAvatar == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(beatAvatar);
                destroyed++;
            }

            if (log && destroyed > 0)
            {
                Log.Info($"Removed {destroyed} BeatAvatar component(s) from clone to prevent external pose overrides.");
            }
        }

        private static void PrepareRenderers(GameObject avatar, bool log)
        {
            int layer = Plugin.Config.HideFromFirstPersonCamera ? 3 : 0;
            HashSet<Renderer> avatarRenderers = CollectAvatarVisualRenderers(avatar);
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                bool keepRenderer = avatarRenderers.Count == 0 || avatarRenderers.Contains(renderer);
                renderer.gameObject.layer = layer;
                renderer.enabled = keepRenderer;
                renderer.forceRenderingOff = !keepRenderer;
                if (keepRenderer)
                {
                    ActivateSelfAndParents(renderer.transform, avatar.transform);
                }
            }

            if (!log)
            {
                return;
            }

            if (avatarRenderers.Count > 0)
            {
                Log.Info($"Prepared clone renderers: kept {avatarRenderers.Count} BeatAvatar visual renderers.");
            }
            else
            {
                Log.Warn("Prepared clone renderers without a BeatAvatarVisualController renderer map; kept all renderers.");
            }
        }

        private static void DisableCloneInteractions(GameObject avatar, bool log)
        {
            int colliderCount = 0;
            foreach (Collider collider in avatar.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                colliderCount++;
            }

            foreach (Rigidbody rigidbody in avatar.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
            }

            if (log && colliderCount > 0)
            {
                Log.Info($"Disabled {colliderCount} clone colliders so it cannot block menu pointers.");
            }
        }

        private static void DisableRuntimeBehaviours(GameObject avatar, bool log)
        {
            int behaviourCount = 0;
            foreach (Behaviour behaviour in avatar.GetComponentsInChildren<Behaviour>(true))
            {
                if (ShouldKeepBehaviourEnabled(behaviour))
                {
                    behaviour.enabled = true;
                    continue;
                }

                behaviour.enabled = false;
                behaviourCount++;
            }

            if (log && behaviourCount > 0)
            {
                Log.Info($"Disabled {behaviourCount} clone runtime behaviours.");
            }
        }

        private static bool ShouldKeepBehaviourEnabled(Behaviour behaviour)
        {
            if (behaviour == null)
            {
                return false;
            }

            if (behaviour is BeatAvatarVisualController)
            {
                return true;
            }

            if (behaviour is BeatAvatarPoseController || behaviour is AvatarTrackedPoseDriver)
            {
                return true;
            }

            string typeName = behaviour.GetType().FullName ?? string.Empty;
            return
                typeName == "BeatSaber.BeatAvatarSDK.AvatarPropertyBlockColorSetter" ||
                typeName == "BeatSaber.BeatAvatarSDK.MulticolorAvatarPartPropertyBlockSetter";
        }

        private static bool IsBrokenMaterial(Material material)
        {
            if (material == null || material.shader == null)
            {
                return true;
            }

            return material.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ActivateSelfAndParents(Transform transform, Transform stopAt)
        {
            while (transform != null)
            {
                transform.gameObject.SetActive(true);
                if (transform == stopAt)
                {
                    return;
                }

                transform = transform.parent;
            }
        }

        private static HashSet<Renderer> CollectAvatarVisualRenderers(GameObject avatar)
        {
            HashSet<Renderer> renderers = new HashSet<Renderer>();
            if (avatar == null)
            {
                return renderers;
            }

            foreach (BeatAvatarVisualController controller in avatar.GetComponentsInChildren<BeatAvatarVisualController>(true))
            {
                CollectAvatarVisualRenderers(controller, renderers);
            }

            return renderers;
        }

        private static void CollectAvatarVisualRenderers(BeatAvatarVisualController controller, HashSet<Renderer> renderers)
        {
            if (controller == null)
            {
                return;
            }

            FieldInfo[] fields = typeof(BeatAvatarVisualController).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                if (IsDisabledMultiplayerAccessoryField(field.Name))
                {
                    continue;
                }

                try
                {
                    if (typeof(MeshFilter).IsAssignableFrom(field.FieldType))
                    {
                        MeshFilter meshFilter = field.GetValue(controller) as MeshFilter;
                        AddRenderer(meshFilter != null ? meshFilter.GetComponent<Renderer>() : null, renderers);
                    }
                    else if (typeof(Renderer).IsAssignableFrom(field.FieldType))
                    {
                        AddRenderer(field.GetValue(controller) as Renderer, renderers);
                    }
                    else
                    {
                        AddRendererFromOwner(field.GetValue(controller), renderers);
                    }
                }
                catch (Exception exception)
                {
                    Log.Debug($"Skipping renderer field {field.Name}: {exception.Message}");
                }
            }
        }

        private static bool IsDisabledMultiplayerAccessoryField(string fieldName)
        {
            return ContainsAny(fieldName, "glasses", "facialHair");
        }

        private static void AddRendererFromOwner(object owner, HashSet<Renderer> renderers)
        {
            if (owner == null)
            {
                return;
            }

            FieldInfo rendererField = owner.GetType().GetField(
                "_renderer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (rendererField == null || !typeof(Renderer).IsAssignableFrom(rendererField.FieldType))
            {
                return;
            }

            AddRenderer(rendererField.GetValue(owner) as Renderer, renderers);
        }

        private static void AddRenderer(Renderer renderer, HashSet<Renderer> renderers)
        {
            if (renderer != null)
            {
                renderers.Add(renderer);
            }
        }

        private static string GetSceneName(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.scene.IsValid())
            {
                return "<invalid>";
            }

            return gameObject.scene.name;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleAvatarDataChanged(AvatarData avatarData)
        {
            if (_clone == null || _refreshRunning)
            {
                return;
            }

            AvatarDataModel dataModel = GetAvatarDataModel();
            BeatAvatarVisualController visualController = GetVisualController();
            if (dataModel == null || visualController == null)
            {
                return;
            }

            _refreshRunning = true;
            try
            {
                RefreshVisualFromAvatarData(dataModel, visualController, avatarData, "avatar-data-changed");
            }
            finally
            {
                _refreshRunning = false;
            }
        }

        private void RefreshVisualFromAvatarData(
            AvatarDataModel dataModel,
            BeatAvatarVisualController visualController,
            AvatarData avatarData,
            string reason)
        {
            if (avatarData != null)
            {
                try
                {
                    TryCopyAvatarPartsModel(dataModel, visualController);
                    visualController.UpdateAvatarVisual(avatarData.Clone());
                    GameObject clone = visualController.gameObject != null
                        ? visualController.transform.root.gameObject
                        : null;
                    if (clone != null && clone.name == CloneName)
                    {
                        RestoreHandTransformBaseline(clone);
                        PrepareCloneForViewing(clone, false);
                    }

                    if (reason == "timer")
                    {
                        Log.Debug("Clone visual refreshed from AvatarDataModel.avatarData.");
                    }
                    else
                    {
                        Log.Info("Clone visual refreshed from AvatarDataModel.avatarData.");
                    }
                }
                catch (Exception exception)
                {
                    Log.Warn("Avatar data refresh failed; keeping cloned source visual.");
                    Log.Error(exception);
                }
            }
            else
            {
                Log.Warn("AvatarDataModel.avatarData was null; clone visual refresh skipped.");
            }
        }

        private void SubscribeToAvatarDataModel(AvatarDataModel dataModel)
        {
            if (dataModel == null || ReferenceEquals(dataModel, _subscribedAvatarDataModel))
            {
                return;
            }

            UnsubscribeAvatarDataModel();
            dataModel.didChangeAvatarDataEvent += _avatarDataChangedHandler;
            _subscribedAvatarDataModel = dataModel;
        }

        private void UnsubscribeAvatarDataModel()
        {
            if (_subscribedAvatarDataModel == null)
            {
                return;
            }

            _subscribedAvatarDataModel.didChangeAvatarDataEvent -= _avatarDataChangedHandler;
            _subscribedAvatarDataModel = null;
        }

        private static void CaptureHandTransformBaselineIfNeeded(GameObject clone)
        {
            if (_handTransformBaseline != null || clone == null)
            {
                return;
            }

            AvatarHandTransformSnapshot snapshot = AvatarHandTransformSnapshot.Capture(clone);
            if (!snapshot.HasAny)
            {
                Log.Warn("Could not capture avatar hand transform baseline.");
                return;
            }

            _handTransformBaseline = snapshot;
            Log.Info("Captured avatar hand transform baseline.");
        }

        private static void RestoreHandTransformBaseline(GameObject clone)
        {
            if (_handTransformBaseline == null || clone == null)
            {
                return;
            }

            _handTransformBaseline.Restore(clone);
        }

        private static void TryCopyAvatarPartsModel(
            AvatarDataModel dataModel,
            BeatAvatarVisualController visualController)
        {
            try
            {
                FieldInfo dataModelPartsField = typeof(AvatarDataModel).GetField(
                    "_avatarPartsModel",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo visualPartsField = typeof(BeatAvatarVisualController).GetField(
                    "_avatarPartsModel",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (dataModelPartsField == null || visualPartsField == null)
                {
                    Log.Warn("Could not find avatar parts model fields for clone refresh.");
                    return;
                }

                AvatarPartsModel partsModel = dataModelPartsField.GetValue(dataModel) as AvatarPartsModel;
                if (partsModel == null)
                {
                    Log.Warn("AvatarDataModel parts model was null; clone refresh may fall back to preview meshes.");
                    return;
                }

                visualPartsField.SetValue(visualController, partsModel);
            }
            catch (Exception exception)
            {
                Log.Warn("Could not copy AvatarPartsModel into cloned visual controller.");
                Log.Error(exception);
            }
        }

        private sealed class AvatarHandTransformSnapshot
        {
            private readonly TransformSnapshot _leftPoseTransform;
            private readonly TransformSnapshot _rightPoseTransform;
            private readonly TransformSnapshot _leftHandMeshTransform;
            private readonly TransformSnapshot _rightHandMeshTransform;

            private AvatarHandTransformSnapshot(
                TransformSnapshot leftPoseTransform,
                TransformSnapshot rightPoseTransform,
                TransformSnapshot leftHandMeshTransform,
                TransformSnapshot rightHandMeshTransform)
            {
                _leftPoseTransform = leftPoseTransform;
                _rightPoseTransform = rightPoseTransform;
                _leftHandMeshTransform = leftHandMeshTransform;
                _rightHandMeshTransform = rightHandMeshTransform;
            }

            internal bool HasAny =>
                _leftPoseTransform.HasValue ||
                _rightPoseTransform.HasValue ||
                _leftHandMeshTransform.HasValue ||
                _rightHandMeshTransform.HasValue;

            internal static AvatarHandTransformSnapshot Capture(GameObject clone)
            {
                BeatAvatarPoseController poseController = clone != null
                    ? clone.GetComponentInChildren<BeatAvatarPoseController>(true)
                    : null;
                BeatAvatarVisualController visualController = clone != null
                    ? clone.GetComponentInChildren<BeatAvatarVisualController>(true)
                    : null;

                return new AvatarHandTransformSnapshot(
                    TransformSnapshot.Capture(GetTransformFieldValue(poseController, PoseLeftHandTransformField)),
                    TransformSnapshot.Capture(GetTransformFieldValue(poseController, PoseRightHandTransformField)),
                    TransformSnapshot.Capture(GetMeshFilterTransformFieldValue(visualController, VisualLeftHandMeshFilterField)),
                    TransformSnapshot.Capture(GetMeshFilterTransformFieldValue(visualController, VisualRightHandMeshFilterField)));
            }

            internal void Restore(GameObject clone)
            {
                BeatAvatarPoseController poseController = clone != null
                    ? clone.GetComponentInChildren<BeatAvatarPoseController>(true)
                    : null;
                BeatAvatarVisualController visualController = clone != null
                    ? clone.GetComponentInChildren<BeatAvatarVisualController>(true)
                    : null;

                _leftPoseTransform.Restore(GetTransformFieldValue(poseController, PoseLeftHandTransformField));
                _rightPoseTransform.Restore(GetTransformFieldValue(poseController, PoseRightHandTransformField));
                _leftHandMeshTransform.Restore(GetMeshFilterTransformFieldValue(visualController, VisualLeftHandMeshFilterField));
                _rightHandMeshTransform.Restore(GetMeshFilterTransformFieldValue(visualController, VisualRightHandMeshFilterField));
            }

            private static Transform GetTransformFieldValue(Component component, FieldInfo field)
            {
                if (component == null || field == null)
                {
                    return null;
                }

                try
                {
                    return field.GetValue(component) as Transform;
                }
                catch (Exception exception)
                {
                    Log.Debug($"Could not read transform field {field.Name}: {exception.Message}");
                    return null;
                }
            }

            private static Transform GetMeshFilterTransformFieldValue(Component component, FieldInfo field)
            {
                if (component == null || field == null)
                {
                    return null;
                }

                try
                {
                    MeshFilter meshFilter = field.GetValue(component) as MeshFilter;
                    return meshFilter != null ? meshFilter.transform : null;
                }
                catch (Exception exception)
                {
                    Log.Debug($"Could not read mesh filter field {field.Name}: {exception.Message}");
                    return null;
                }
            }
        }

        private struct TransformSnapshot
        {
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;
            private readonly Vector3 _localScale;

            private TransformSnapshot(Transform transform)
            {
                HasValue = transform != null;
                _localPosition = HasValue ? transform.localPosition : Vector3.zero;
                _localRotation = HasValue ? transform.localRotation : Quaternion.identity;
                _localScale = HasValue ? transform.localScale : Vector3.one;
            }

            internal bool HasValue { get; }

            internal static TransformSnapshot Capture(Transform transform)
            {
                return new TransformSnapshot(transform);
            }

            internal void Restore(Transform transform)
            {
                if (!HasValue || transform == null)
                {
                    return;
                }

                transform.localScale = _localScale;
                transform.SetLocalPositionAndRotation(_localPosition, _localRotation);
            }
        }

        private sealed class SourceCandidate
        {
            private SourceCandidate(
                GameObject gameObject,
                string kind,
                string path,
                string sceneName,
                bool activeInHierarchy,
                int rendererCount,
                int score)
            {
                GameObject = gameObject;
                Kind = kind;
                Path = path;
                SceneName = sceneName;
                ActiveInHierarchy = activeInHierarchy;
                RendererCount = rendererCount;
                Score = score;
            }

            internal GameObject GameObject { get; }

            internal string Kind { get; }

            internal string Path { get; }

            internal string SceneName { get; }

            internal bool ActiveInHierarchy { get; }

            internal int RendererCount { get; }

            internal int Score { get; }

            internal static SourceCandidate Create(Component component, string kind)
            {
                GameObject gameObject = component.gameObject;
                string path = GetPath(gameObject.transform);
                int rendererCount = gameObject.GetComponentsInChildren<Renderer>(true).Length;
                int score = rendererCount;

                if (component is BeatAvatar)
                {
                    score += BeatAvatarBonus;
                }

                if (gameObject.activeInHierarchy)
                {
                    score += LiveObjectBonus;
                }

                if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                {
                    score += LoadedSceneBonus;
                }

                if (ContainsAny(path, "Multiplayer", "ConnectedPlayer", "Lobby", "Gameplay", "AvatarController"))
                {
                    score += MultiplayerPathBonus;
                }

                if (ContainsAny(path, "BeatAvatarEditorFlowCoordinator", "AvatarEditor", "AvatarSelectionView"))
                {
                    score -= EditorPreviewPenalty;
                }

                return new SourceCandidate(
                    gameObject,
                    kind,
                    path,
                    GetSceneName(gameObject),
                    gameObject.activeInHierarchy,
                    rendererCount,
                    score);
            }
        }
    }
}
