using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CageDuel.UI
{
    /// <summary>
    /// Touch-friendly circular action button supporting both single-press actions
    /// (Jump, Crouch, Reload) and continuous hold actions (Fire).
    /// </summary>
    public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image buttonGraphic;
        [SerializeField] private Color idleColor = new Color(0.14f, 0.18f, 0.24f, 0.78f);
        [SerializeField] private Color pressedColor = new Color(0.95f, 0.55f, 0.18f, 0.92f);
        [SerializeField] private Color toggledColor = new Color(0.20f, 0.72f, 0.92f, 0.88f);

        private int _activePointerId = int.MinValue;
        private bool _isHeld;
        private bool _pressedThisFrame;
        private bool _isToggledActive;

        public bool IsHeld => _isHeld;

        public void Configure(Image graphic, Color normal, Color pressed, Color toggled)
        {
            buttonGraphic = graphic;
            idleColor = normal;
            pressedColor = pressed;
            toggledColor = toggled;
            RefreshVisual();
        }

        private void Awake()
        {
            if (buttonGraphic != null && buttonGraphic.sprite == null)
            {
                buttonGraphic.sprite = ProceduralUISprites.GetFilledCircleSprite();
            }
            RefreshVisual();
        }

        private void OnDisable()
        {
            _activePointerId = int.MinValue;
            _isHeld = false;
            _pressedThisFrame = false;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue)
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            _isHeld = true;
            _pressedThisFrame = true;
            RefreshVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            _activePointerId = int.MinValue;
            _isHeld = false;
            RefreshVisual();
        }

        public bool ConsumePressed()
        {
            if (!_pressedThisFrame)
            {
                return false;
            }

            _pressedThisFrame = false;
            return true;
        }

        public void SetToggledState(bool active)
        {
            _isToggledActive = active;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (buttonGraphic == null)
            {
                return;
            }

            if (_isHeld)
            {
                buttonGraphic.color = pressedColor;
                transform.localScale = Vector3.one * 0.93f;
            }
            else
            {
                buttonGraphic.color = _isToggledActive ? toggledColor : idleColor;
                transform.localScale = Vector3.one;
            }
        }
    }
}
