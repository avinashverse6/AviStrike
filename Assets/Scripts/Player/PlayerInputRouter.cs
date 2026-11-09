using System;
using CageDuel.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CageDuel.Player
{
    /// <summary>
    /// Unified input router combining desktop Keyboard/Mouse controls (for Editor testing)
    /// and mobile Virtual Joystick + Touch Look + Action Buttons (for Android/iOS and Editor UI testing).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputRouter : MonoBehaviour
    {
        [Header("Sensitivity")]
        [SerializeField] private float mouseLookSensitivity = 0.16f;
        [SerializeField] private float touchLookSensitivity = 0.22f;

        [Header("Mobile UI References")]
        [SerializeField] private VirtualJoystick moveJoystick;
        [SerializeField] private TouchLookZone touchLookZone;
        [SerializeField] private MobileActionButton fireButton;
        [SerializeField] private MobileActionButton jumpButton;
        [SerializeField] private MobileActionButton crouchButton;
        [SerializeField] private MobileActionButton reloadButton;

        private Vector2 _moveInput;
        private Vector2 _lookDelta;
        private bool _fireHeld;
        private bool _firePressedThisFrame;
        private bool _jumpPressedThisFrame;
        private bool _crouchTogglePressedThisFrame;
        private bool _reloadPressedThisFrame;
        private bool _selfDamageTestPressedThisFrame;
        private bool _isMouseLocked;

        public Vector2 MoveInput => _moveInput;
        public Vector2 LookDelta => _lookDelta;
        public bool FireHeld => _fireHeld;
        public bool FirePressedThisFrame => _firePressedThisFrame;
        public bool JumpPressedThisFrame => _jumpPressedThisFrame;
        public bool CrouchTogglePressedThisFrame => _crouchTogglePressedThisFrame;
        public bool ReloadPressedThisFrame => _reloadPressedThisFrame;
        public bool SelfDamageTestPressedThisFrame => _selfDamageTestPressedThisFrame;
        public bool IsMouseLocked => _isMouseLocked;

        public event Action<bool> OnMouseLockChanged;

        public void ConfigureMobileControls(
            VirtualJoystick joystick,
            TouchLookZone lookZone,
            MobileActionButton fireBtn,
            MobileActionButton jumpBtn,
            MobileActionButton crouchBtn,
            MobileActionButton reloadBtn)
        {
            moveJoystick = joystick;
            touchLookZone = lookZone;
            fireButton = fireBtn;
            jumpButton = jumpBtn;
            crouchButton = crouchBtn;
            reloadButton = reloadBtn;
        }

        public void SetCrouchButtonVisual(bool isCrouching)
        {
            if (crouchButton != null)
            {
                crouchButton.SetToggledState(isCrouching);
            }
        }

        public void ToggleMouseLock()
        {
            if (Application.isMobilePlatform)
            {
                return;
            }

            SetMouseLocked(!_isMouseLocked);
        }

        public void SetMouseLocked(bool locked)
        {
            if (Application.isMobilePlatform)
            {
                locked = false;
            }

            _isMouseLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            OnMouseLockChanged?.Invoke(_isMouseLocked);
        }

        private void OnDisable()
        {
            if (_isMouseLocked)
            {
                SetMouseLocked(false);
            }
        }

        private void Update()
        {
            Vector2 kbMove = Vector2.zero;
            Vector2 rawMouseDelta = Vector2.zero;
            bool kbJump = false;
            bool kbCrouch = false;
            bool kbReload = false;
            bool kbFireHeld = false;
            bool kbFirePressed = false;
            bool toggleLockPressed = false;
            bool unlockPressed = false;
            bool selfDamagePressed = false;
            bool handledDesktopInput = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                handledDesktopInput = true;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y += 1f;

                kbJump = kb.spaceKey.wasPressedThisFrame;
                kbCrouch = kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
                kbReload = kb.rKey.wasPressedThisFrame;
                kbFireHeld = kb.fKey.isPressed;
                kbFirePressed = kb.fKey.wasPressedThisFrame;
                toggleLockPressed = kb.tabKey.wasPressedThisFrame;
                unlockPressed = kb.escapeKey.wasPressedThisFrame;
                selfDamagePressed = kb.kKey.wasPressedThisFrame;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && _isMouseLocked)
            {
                rawMouseDelta = mouse.delta.ReadValue();
                kbFireHeld |= mouse.leftButton.isPressed;
                kbFirePressed |= mouse.leftButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!handledDesktopInput && !Application.isMobilePlatform)
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) kbMove.x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) kbMove.x += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) kbMove.y -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) kbMove.y += 1f;

                kbJump = Input.GetKeyDown(KeyCode.Space);
                kbCrouch = Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
                kbReload = Input.GetKeyDown(KeyCode.R);
                kbFireHeld = Input.GetKey(KeyCode.F);
                kbFirePressed = Input.GetKeyDown(KeyCode.F);
                toggleLockPressed = Input.GetKeyDown(KeyCode.Tab);
                unlockPressed = Input.GetKeyDown(KeyCode.Escape);
                selfDamagePressed = Input.GetKeyDown(KeyCode.K);

                if (_isMouseLocked)
                {
                    rawMouseDelta = new Vector2(Input.GetAxisRaw("Mouse X") * 12f, Input.GetAxisRaw("Mouse Y") * 12f);
                    kbFireHeld |= Input.GetMouseButton(0);
                    kbFirePressed |= Input.GetMouseButtonDown(0);
                }
            }
#endif

            if (!Application.isMobilePlatform)
            {
                if (unlockPressed && _isMouseLocked)
                {
                    SetMouseLocked(false);
                }
                else if (toggleLockPressed)
                {
                    ToggleMouseLock();
                }
            }

            Vector2 stickMove = moveJoystick != null ? moveJoystick.Value : Vector2.zero;
            _moveInput = Vector2.ClampMagnitude(kbMove + stickMove, 1f);

            Vector2 touchDelta = touchLookZone != null ? touchLookZone.ConsumeLookDelta() : Vector2.zero;
            float dpiScale = Screen.dpi > 40f ? (160f / Screen.dpi) : 1f;
            _lookDelta = (rawMouseDelta * mouseLookSensitivity) + (touchDelta * touchLookSensitivity * dpiScale);

            bool uiFirePressed = fireButton != null && fireButton.ConsumePressed();
            bool uiFireHeld = fireButton != null && fireButton.IsHeld;

            _fireHeld = kbFireHeld || uiFireHeld;
            _firePressedThisFrame = kbFirePressed || uiFirePressed;
            _jumpPressedThisFrame = kbJump || (jumpButton != null && jumpButton.ConsumePressed());
            _crouchTogglePressedThisFrame = kbCrouch || (crouchButton != null && crouchButton.ConsumePressed());
            _reloadPressedThisFrame = kbReload || (reloadButton != null && reloadButton.ConsumePressed());
            _selfDamageTestPressedThisFrame = selfDamagePressed;
        }
    }
}
