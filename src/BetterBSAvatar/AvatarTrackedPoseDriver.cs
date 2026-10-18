using BeatSaber.BeatAvatarSDK;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace BetterBSAvatar
{
    internal sealed class AvatarTrackedPoseDriver : MonoBehaviour
    {
        private const float PlayerTransformsScanIntervalSeconds = 2.0f;
        private const float MenuPlayerControllerScanIntervalSeconds = 2.0f;
        private const float SaberManagerScanIntervalSeconds = 2.0f;
        private const float VrControllerScanIntervalSeconds = 2.0f;
        private const float MinSaberBladeLength = 0.05f;
        private const float MinViewAnchorOffsetAngleDegrees = 1.0f;
        private const float AvatarScale = 1.0f;

        private static Quaternion _capturedLeftViewAnchorOffset = Quaternion.identity;
        private static Quaternion _capturedRightViewAnchorOffset = Quaternion.identity;
        private static bool _leftViewAnchorOffsetCaptured;
        private static bool _rightViewAnchorOffsetCaptured;

        private static readonly FieldInfo OriginParentField = typeof(PlayerTransforms).GetField(
            "_originParentTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo OriginField = typeof(PlayerTransforms).GetField(
            "_originTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerHeadTransformField = typeof(PlayerTransforms).GetField(
            "_headTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerLeftHandTransformField = typeof(PlayerTransforms).GetField(
            "_leftHandTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerRightHandTransformField = typeof(PlayerTransforms).GetField(
            "_rightHandTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo HeadTransformField = typeof(BeatAvatarPoseController).GetField(
            "_headTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo LeftHandTransformField = typeof(BeatAvatarPoseController).GetField(
            "_leftHandTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo RightHandTransformField = typeof(BeatAvatarPoseController).GetField(
            "_rightHandTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo BodyTransformField = typeof(BeatAvatarPoseController).GetField(
            "_bodyTransform",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private GameObject _clone;
        private BeatAvatarPoseController _poseController;
        private Transform _headTransform;
        private Transform _leftHandTransform;
        private Transform _rightHandTransform;
        private Transform _bodyTransform;
        private PlayerTransforms _playerTransforms;
        private MenuPlayerController _menuPlayerController;
        private SaberManager _saberManager;
        private VRController _leftVrController;
        private VRController _rightVrController;
        private Transform _playerRigCameraTransform;
        private float _nextPlayerTransformsScanTime;
        private float _nextMenuPlayerControllerScanTime;
        private float _nextSaberManagerScanTime;
        private float _nextVrControllerScanTime;
        private string _poseSource;
        private UnityEngine.Object _poseSourceObject;
        private bool _loggedSaberPoseSource;
        private bool _loggedHandRotationCorrection;
        private bool _loggedMissingPose;
        private bool _poseControllerFailed;

        internal void Initialize(GameObject clone)
        {
            if (_clone == clone && _poseController != null)
            {
                return;
            }

            _clone = clone;
            _poseController = clone != null ? clone.GetComponentInChildren<BeatAvatarPoseController>(true) : null;
            CachePoseTransforms();
            _playerTransforms = null;
            _menuPlayerController = null;
            _saberManager = null;
            _leftVrController = null;
            _rightVrController = null;
            _playerRigCameraTransform = null;
            _nextPlayerTransformsScanTime = 0.0f;
            _nextMenuPlayerControllerScanTime = 0.0f;
            _nextSaberManagerScanTime = 0.0f;
            _nextVrControllerScanTime = 0.0f;
            _poseSource = null;
            _poseSourceObject = null;
            _loggedSaberPoseSource = false;
            _loggedHandRotationCorrection = false;
            _loggedMissingPose = false;
            _poseControllerFailed = false;
            ClearGameplaySaberState();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            InvalidateTrackingSources();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InvalidateTrackingSources();
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            InvalidateTrackingSources();
        }

        private void InvalidateTrackingSources()
        {
            _playerTransforms = null;
            _playerRigCameraTransform = null;
            _menuPlayerController = null;
            _saberManager = null;
            _leftVrController = null;
            _rightVrController = null;
            _nextPlayerTransformsScanTime = 0.0f;
            _nextMenuPlayerControllerScanTime = 0.0f;
            _nextSaberManagerScanTime = 0.0f;
            _nextVrControllerScanTime = 0.0f;
        }

        private void LateUpdate()
        {
            if (_clone == null)
            {
                return;
            }

            bool gameplaySceneLoaded = IsGameplaySaberSceneLoaded();
            Transform cameraTransform = GetActiveMainCameraTransform();
            if (!gameplaySceneLoaded)
            {
                ClearGameplaySaberState();
                _playerTransforms = null;
                _playerRigCameraTransform = null;
            }

            if (gameplaySceneLoaded && TryUpdateFromPlayerTransforms(cameraTransform))
            {
                return;
            }

            if (!gameplaySceneLoaded && TryUpdateFromMenuPlayerController())
            {
                return;
            }

            if (TryUpdateFromCameraAndXrNodes(cameraTransform))
            {
                return;
            }

            LogPoseSource("none", null);
        }

        private bool TryUpdateFromMenuPlayerController()
        {
            MenuPlayerController menuPlayerController = GetCachedMenuPlayerController();
            if (menuPlayerController == null)
            {
                return false;
            }

            Vector3 leftHandPosition;
            Vector3 rightHandPosition;
            Quaternion leftHandRotation;
            Quaternion rightHandRotation;
            if (!TryGetMenuControllerPose(menuPlayerController.leftController, out leftHandPosition, out leftHandRotation) ||
                !TryGetMenuControllerPose(menuPlayerController.rightController, out rightHandPosition, out rightHandRotation))
            {
                return false;
            }

            CaptureViewAnchorOffset(
                menuPlayerController.leftController,
                ref _capturedLeftViewAnchorOffset,
                ref _leftViewAnchorOffsetCaptured);
            CaptureViewAnchorOffset(
                menuPlayerController.rightController,
                ref _capturedRightViewAnchorOffset,
                ref _rightViewAnchorOffsetCaptured);

            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * AvatarScale;

            Quaternion inverseRootRotation = Quaternion.Inverse(transform.rotation);
            bool applied = ApplyLocalPose(
                transform.InverseTransformPoint(menuPlayerController.headPos),
                transform.InverseTransformPoint(leftHandPosition),
                transform.InverseTransformPoint(rightHandPosition),
                inverseRootRotation * menuPlayerController.headRot,
                inverseRootRotation * leftHandRotation,
                inverseRootRotation * rightHandRotation);
            if (applied)
            {
                LogPoseSource("MenuPlayerController", menuPlayerController);
            }

            return applied;
        }

        private bool TryUpdateFromPlayerTransforms(Transform cameraTransform)
        {
            PlayerTransforms playerTransforms = GetCachedPlayerTransforms(cameraTransform);
            if (playerTransforms == null)
            {
                return false;
            }

            Transform leftHand = GetPlayerTransform(playerTransforms, PlayerLeftHandTransformField);
            Transform rightHand = GetPlayerTransform(playerTransforms, PlayerRightHandTransformField);
            if (leftHand == null || rightHand == null)
            {
                return false;
            }

            SetTrackingRoot(GetTrackingRoot(playerTransforms));
            Quaternion inverseRootRotation = Quaternion.Inverse(transform.rotation);
            Vector3 leftHandPosition = transform.InverseTransformPoint(leftHand.position);
            Vector3 rightHandPosition = transform.InverseTransformPoint(rightHand.position);
            Quaternion leftHandRotation = inverseRootRotation * leftHand.rotation;
            Quaternion rightHandRotation = inverseRootRotation * rightHand.rotation;
            ApplyViewAnchorRotationOverride(ref leftHandRotation, ref rightHandRotation);
            ApplySaberPoseOverrides(
                ref leftHandPosition,
                ref rightHandPosition,
                ref leftHandRotation,
                ref rightHandRotation);

            bool applied = ApplyLocalPose(
                transform.InverseTransformPoint(playerTransforms.headWorldPos),
                leftHandPosition,
                rightHandPosition,
                inverseRootRotation * playerTransforms.headWorldRot,
                leftHandRotation,
                rightHandRotation);
            if (applied)
            {
                LogPoseSource("PlayerTransforms", playerTransforms);
            }

            return applied;
        }

        private bool TryUpdateFromCameraAndXrNodes(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return false;
            }

            Transform trackingRoot = cameraTransform.parent;
            SetTrackingRoot(trackingRoot);

            Vector3 leftHandPosition = GetDevicePosition(XRNode.LeftHand);
            Vector3 rightHandPosition = GetDevicePosition(XRNode.RightHand);
            Quaternion leftHandRotation = GetDeviceRotation(XRNode.LeftHand);
            Quaternion rightHandRotation = GetDeviceRotation(XRNode.RightHand);
            // XR hands are in tracking space; include the camera parent's world scale and offset.
            if (trackingRoot != null)
            {
                leftHandPosition = trackingRoot.TransformPoint(leftHandPosition);
                rightHandPosition = trackingRoot.TransformPoint(rightHandPosition);
                leftHandRotation = trackingRoot.rotation * leftHandRotation;
                rightHandRotation = trackingRoot.rotation * rightHandRotation;
            }

            Quaternion inverseRootRotation = Quaternion.Inverse(transform.rotation);
            leftHandPosition = transform.InverseTransformPoint(leftHandPosition);
            rightHandPosition = transform.InverseTransformPoint(rightHandPosition);
            leftHandRotation = inverseRootRotation * leftHandRotation;
            rightHandRotation = inverseRootRotation * rightHandRotation;
            ApplyViewAnchorRotationOverride(ref leftHandRotation, ref rightHandRotation);
            ApplySaberPoseOverrides(
                ref leftHandPosition,
                ref rightHandPosition,
                ref leftHandRotation,
                ref rightHandRotation);

            bool applied = ApplyLocalPose(
                transform.InverseTransformPoint(cameraTransform.position),
                leftHandPosition,
                rightHandPosition,
                inverseRootRotation * cameraTransform.rotation,
                leftHandRotation,
                rightHandRotation);
            if (applied)
            {
                LogPoseSource("Camera + XR nodes", cameraTransform);
            }

            return applied;
        }

        private void SetTrackingRoot(Transform trackingRoot)
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            transform.position = trackingRoot != null ? trackingRoot.position : Vector3.zero;
            transform.rotation = trackingRoot != null ? trackingRoot.rotation : Quaternion.identity;
            transform.localScale = Vector3.one * AvatarScale;
        }

        private static Transform GetActiveMainCameraTransform()
        {
            Camera camera = Camera.main;
            return camera != null && camera.isActiveAndEnabled && camera.gameObject.scene.IsValid() &&
                camera.gameObject.scene.isLoaded ? camera.transform : null;
        }

        private void LogPoseSource(string source, Component sourceObject)
        {
            if (_poseSource == source && _poseSourceObject == sourceObject)
            {
                return;
            }

            _poseSource = source;
            _poseSourceObject = sourceObject;
            if (sourceObject == null)
            {
                Log.Info("Avatar pose source: none (waiting for a local player rig or main camera).");
                return;
            }

            string path = sourceObject.name;
            for (Transform parent = sourceObject.transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            Log.Info($"Avatar pose source: {source} ({sourceObject.gameObject.scene.name}/{path}, id={sourceObject.GetInstanceID()}).");
        }

        private static Vector3 GetDevicePosition(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            Vector3 position;
            return device.isValid && device.TryGetFeatureValue(CommonUsages.devicePosition, out position)
                ? position
                : Vector3.zero;
        }

        private static Quaternion GetDeviceRotation(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            Quaternion rotation;
            return device.isValid && device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation)
                ? rotation
                : Quaternion.identity;
        }

        private bool ApplyLocalPose(
            Vector3 headPosition,
            Vector3 leftHandPosition,
            Vector3 rightHandPosition,
            Quaternion headRotation,
            Quaternion leftHandRotation,
            Quaternion rightHandRotation)
        {
            if (_poseController != null && !_poseControllerFailed)
            {
                try
                {
                    _poseController.UpdateTransforms(
                        headPosition,
                        leftHandPosition,
                        rightHandPosition,
                        headRotation,
                        leftHandRotation,
                        rightHandRotation);
                    return true;
                }
                catch (Exception exception)
                {
                    _poseControllerFailed = true;
                    Log.Warn("BeatAvatarPoseController tracking failed; falling back to direct transform tracking.");
                    Log.Error(exception);
                }
            }

            if (_headTransform == null || _leftHandTransform == null || _rightHandTransform == null)
            {
                if (!_loggedMissingPose)
                {
                    _loggedMissingPose = true;
                    Log.Warn("Clone does not expose enough avatar pose transforms for player tracking.");
                }

                return false;
            }

            _headTransform.SetLocalPositionAndRotation(headPosition, headRotation);
            _leftHandTransform.SetLocalPositionAndRotation(leftHandPosition, leftHandRotation);
            _rightHandTransform.SetLocalPositionAndRotation(rightHandPosition, rightHandRotation);

            if (_bodyTransform != null)
            {
                Vector3 bodyPosition = headPosition + (headRotation * new Vector3(0.0f, -0.45f, -0.06f));
                Quaternion bodyRotation = Quaternion.Euler(0.0f, headRotation.eulerAngles.y, 0.0f);
                _bodyTransform.SetLocalPositionAndRotation(bodyPosition, bodyRotation);
            }

            return true;
        }

        private void CachePoseTransforms()
        {
            _headTransform = GetPoseTransform(HeadTransformField);
            _leftHandTransform = GetPoseTransform(LeftHandTransformField);
            _rightHandTransform = GetPoseTransform(RightHandTransformField);
            _bodyTransform = GetPoseTransform(BodyTransformField);
        }

        private Transform GetPoseTransform(FieldInfo field)
        {
            if (_poseController == null || field == null)
            {
                return null;
            }

            return field.GetValue(_poseController) as Transform;
        }

        private void ApplyViewAnchorRotationOverride(
            ref Quaternion leftHandRotation,
            ref Quaternion rightHandRotation)
        {
            EnsureVrControllers();
            if (!_leftViewAnchorOffsetCaptured && _leftVrController != null)
            {
                CaptureViewAnchorOffset(_leftVrController, ref _capturedLeftViewAnchorOffset, ref _leftViewAnchorOffsetCaptured);
            }

            if (!_rightViewAnchorOffsetCaptured && _rightVrController != null)
            {
                CaptureViewAnchorOffset(_rightVrController, ref _capturedRightViewAnchorOffset, ref _rightViewAnchorOffsetCaptured);
            }

            bool corrected = false;
            if (_leftViewAnchorOffsetCaptured)
            {
                leftHandRotation = leftHandRotation * _capturedLeftViewAnchorOffset;
                corrected = true;
            }

            if (_rightViewAnchorOffsetCaptured)
            {
                rightHandRotation = rightHandRotation * _capturedRightViewAnchorOffset;
                corrected = true;
            }

            if (corrected && !_loggedHandRotationCorrection)
            {
                _loggedHandRotationCorrection = true;
                Log.Info("Aligning avatar hand rotations to captured VR controller viewAnchor offsets.");
            }
        }

        private static void CaptureViewAnchorOffset(
            VRController controller,
            ref Quaternion offsetField,
            ref bool capturedFlag)
        {
            if (capturedFlag || controller == null)
            {
                return;
            }

            Transform anchor = controller.viewAnchorTransform;
            if (anchor == null)
            {
                return;
            }

            Quaternion offset = anchor.localRotation;
            if (Quaternion.Angle(offset, Quaternion.identity) < MinViewAnchorOffsetAngleDegrees)
            {
                return;
            }

            offsetField = offset;
            capturedFlag = true;
        }

        private void EnsureVrControllers()
        {
            bool leftValid = _leftVrController != null && _leftVrController.gameObject.activeInHierarchy;
            bool rightValid = _rightVrController != null && _rightVrController.gameObject.activeInHierarchy;
            if (leftValid && rightValid)
            {
                return;
            }

            if (Time.unscaledTime < _nextVrControllerScanTime)
            {
                return;
            }

            _nextVrControllerScanTime = Time.unscaledTime + VrControllerScanIntervalSeconds;

            VRController bestLeft = leftValid ? _leftVrController : null;
            VRController bestRight = rightValid ? _rightVrController : null;
            foreach (VRController controller in Resources.FindObjectsOfTypeAll<VRController>())
            {
                if (controller == null || controller.viewAnchorTransform == null)
                {
                    continue;
                }

                bool active = controller.gameObject.activeInHierarchy;
                if (controller.node == XRNode.LeftHand)
                {
                    if (bestLeft == null || (active && !bestLeft.gameObject.activeInHierarchy))
                    {
                        bestLeft = controller;
                    }
                }
                else if (controller.node == XRNode.RightHand)
                {
                    if (bestRight == null || (active && !bestRight.gameObject.activeInHierarchy))
                    {
                        bestRight = controller;
                    }
                }
            }

            _leftVrController = bestLeft;
            _rightVrController = bestRight;
        }

        private void ApplySaberPoseOverrides(
            ref Vector3 leftHandPosition,
            ref Vector3 rightHandPosition,
            ref Quaternion leftHandRotation,
            ref Quaternion rightHandRotation)
        {
            if (!IsGameplaySaberSceneLoaded())
            {
                return;
            }

            SaberManager saberManager = GetCachedSaberManager();
            if (saberManager == null)
            {
                return;
            }

            bool alignedLeft = TryAlignHandToSaber(saberManager.leftSaber, ref leftHandPosition);
            bool alignedRight = TryAlignHandToSaber(saberManager.rightSaber, ref rightHandPosition);
            if ((alignedLeft || alignedRight) && !_loggedSaberPoseSource)
            {
                _loggedSaberPoseSource = true;
                Log.Info("Aligning avatar hand positions to Beat Saber saber handle positions.");
            }
        }

        private bool TryAlignHandToSaber(Saber saber, ref Vector3 handLocalPosition)
        {
            if (saber == null || !saber.gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector3 bladeWorldDirection = saber.saberBladeTopPos - saber.saberBladeBottomPos;
            if (bladeWorldDirection.sqrMagnitude < MinSaberBladeLength * MinSaberBladeLength)
            {
                return false;
            }

            handLocalPosition = transform.InverseTransformPoint(GetSaberHandleWorldPosition(saber));
            return true;
        }

        private static Vector3 GetSaberHandleWorldPosition(Saber saber)
        {
            Vector3 handlePosition = saber.handlePos;
            return handlePosition.sqrMagnitude > 0.0001f
                ? handlePosition
                : saber.transform.position;
        }

        private PlayerTransforms GetCachedPlayerTransforms(Transform cameraTransform)
        {
            bool cachedRigUsable = GetPlayerRigPriority(_playerTransforms, cameraTransform) > 0;
            if (!PlayerRigSelection.ShouldScan(
                !ReferenceEquals(_playerTransforms, null),
                cachedRigUsable,
                _playerRigCameraTransform != cameraTransform,
                Time.unscaledTime,
                _nextPlayerTransformsScanTime))
            {
                return cachedRigUsable ? _playerTransforms : null;
            }

            _nextPlayerTransformsScanTime = Time.unscaledTime + PlayerTransformsScanIntervalSeconds;
            _playerRigCameraTransform = cameraTransform;
            _playerTransforms = FindBestPlayerTransforms(cameraTransform, _playerTransforms);
            return _playerTransforms;
        }

        private static PlayerTransforms FindBestPlayerTransforms(Transform cameraTransform, PlayerTransforms preferred)
        {
            int bestPriority = GetPlayerRigPriority(preferred, cameraTransform);
            PlayerTransforms best = bestPriority > 0 ? preferred : null;
            foreach (PlayerTransforms transforms in Resources.FindObjectsOfTypeAll<PlayerTransforms>())
            {
                int priority = GetPlayerRigPriority(transforms, cameraTransform);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = transforms;
                }
            }

            return best;
        }

        private static int GetPlayerRigPriority(PlayerTransforms playerTransforms, Transform cameraTransform)
        {
            if (playerTransforms == null)
            {
                return 0;
            }

            Transform head = GetPlayerTransform(playerTransforms, PlayerHeadTransformField);
            Transform origin = GetPlayerTransform(playerTransforms, OriginField);
            Scene scene = playerTransforms.gameObject.scene;
            // BeatLeader can disable the original camera and replace the head during replay playback.
            // MainCamera remains a local-rig marker even when it is inactive; the rig itself must be live.
            MainCamera localCamera = origin != null
                ? origin.GetComponentInChildren<MainCamera>(true)
                : (head != null ? head.GetComponentInChildren<MainCamera>(true) : null);
            bool connectedPlayer = playerTransforms.GetComponentInParent<MultiplayerConnectedPlayerFacade>() != null ||
                (head != null && head.GetComponentInParent<MultiplayerConnectedPlayerFacade>() != null);
            bool headContainsCamera = cameraTransform != null && head != null && cameraTransform.IsChildOf(head);
            bool originContainsCamera = cameraTransform != null && origin != null && cameraTransform.IsChildOf(origin);
            return PlayerRigSelection.GetPriority(
                IsGameplaySaberSceneLoaded(),
                playerTransforms.isActiveAndEnabled,
                scene.IsValid() && scene.isLoaded,
                scene.name,
                PlayerRigSelection.IsLocalRig(
                    headContainsCamera,
                    originContainsCamera,
                    localCamera != null,
                    connectedPlayer),
                headContainsCamera || originContainsCamera);
        }

        private static Transform GetPlayerTransform(PlayerTransforms playerTransforms, FieldInfo field)
        {
            return field != null ? field.GetValue(playerTransforms) as Transform : null;
        }

        private MenuPlayerController GetCachedMenuPlayerController()
        {
            if (_menuPlayerController != null && _menuPlayerController.gameObject.activeInHierarchy)
            {
                return _menuPlayerController;
            }

            if (Time.unscaledTime < _nextMenuPlayerControllerScanTime)
            {
                return null;
            }

            _nextMenuPlayerControllerScanTime = Time.unscaledTime + MenuPlayerControllerScanIntervalSeconds;
            _menuPlayerController = FindBestMenuPlayerController();
            return _menuPlayerController;
        }

        private static MenuPlayerController FindBestMenuPlayerController()
        {
            MenuPlayerController[] menuPlayerControllers = Resources.FindObjectsOfTypeAll<MenuPlayerController>();
            MenuPlayerController best = null;
            foreach (MenuPlayerController controller in menuPlayerControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                if (best == null ||
                    (controller.gameObject.activeInHierarchy && !best.gameObject.activeInHierarchy))
                {
                    best = controller;
                }
            }

            return best != null && best.gameObject.activeInHierarchy ? best : null;
        }

        private static bool IsUsableMenuController(VRController controller)
        {
            return controller != null && controller.active;
        }

        private static bool TryGetMenuControllerPose(VRController controller, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (!IsUsableMenuController(controller))
            {
                return false;
            }

            Transform pointerAnchor = controller.viewAnchorTransform;
            if (pointerAnchor != null)
            {
                position = pointerAnchor.position;
                rotation = pointerAnchor.rotation;
                return true;
            }

            position = controller.position;
            rotation = controller.rotation;
            return true;
        }

        private SaberManager GetCachedSaberManager()
        {
            if (IsUsableSaberManager(_saberManager))
            {
                return _saberManager;
            }

            _saberManager = null;

            if (Time.unscaledTime < _nextSaberManagerScanTime)
            {
                return null;
            }

            _nextSaberManagerScanTime = Time.unscaledTime + SaberManagerScanIntervalSeconds;
            _saberManager = FindBestSaberManager();
            return _saberManager;
        }

        private static SaberManager FindBestSaberManager()
        {
            SaberManager best = null;
            foreach (SaberManager manager in Resources.FindObjectsOfTypeAll<SaberManager>())
            {
                if (!IsUsableSaberManager(manager))
                {
                    continue;
                }

                if (best == null || (PlayerRigSelection.IsGameplayScene(manager.gameObject.scene.name) &&
                    !PlayerRigSelection.IsGameplayScene(best.gameObject.scene.name)))
                {
                    best = manager;
                }
            }

            return best;
        }

        private static bool IsUsableSaberManager(SaberManager manager)
        {
            return manager != null && manager.isActiveAndEnabled && manager.gameObject.scene.IsValid() &&
                manager.gameObject.scene.isLoaded && manager.GetComponentInParent<MultiplayerConnectedPlayerFacade>() == null;
        }

        private static bool IsGameplaySaberSceneLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && PlayerRigSelection.IsGameplayScene(scene.name))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearGameplaySaberState()
        {
            _saberManager = null;
        }

        private static Transform GetTrackingRoot(PlayerTransforms playerTransforms)
        {
            Transform originParent = OriginParentField != null
                ? OriginParentField.GetValue(playerTransforms) as Transform
                : null;
            if (originParent != null)
            {
                return originParent;
            }

            return OriginField != null
                ? OriginField.GetValue(playerTransforms) as Transform
                : null;
        }
    }
}
