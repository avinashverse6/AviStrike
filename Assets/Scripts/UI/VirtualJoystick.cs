using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CageDuel.UI
{
    /// <summary>
    /// On-screen virtual thumbstick supporting multi-touch pointer tracking.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform backgroundRect;
        [SerializeField] private RectTransform handleRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image handleImage;
        [SerializeField] private float handleRange = 64f;
        [Range(0f, 0.45f)]
        [SerializeField] private float deadZone = 0.1f;

        private int _activePointerId = int.MinValue;
        private Vector2 _value;

        public Vector2 Value => _value;
        public bool IsActive => _activePointerId != int.MinValue;

        public void Configure(
            RectTransform bgRect,
            RectTransform knobRect,
            Image bgImg,
            Image knobImg,
            float maxRange = 64f)
        {
            backgroundRect = bgRect;
            handleRect = knobRect;
            backgroundImage = bgImg;
            handleImage = knobImg;
            handleRange = Mathf.Max(16f, maxRange);
        }

        private void Awake()
        {
            if (backgroundImage != null && backgroundImage.sprite == null)
            {
                backgroundImage.sprite = ProceduralUISprites.GetRingSprite();
            }

            if (handleImage != null && handleImage.sprite == null)
            {
                handleImage.sprite = ProceduralUISprites.GetFilledCircleSprite();
            }
        }

        private void OnDisable()
        {
            ResetStick();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue)
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            UpdateStickFromEvent(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            UpdateStickFromEvent(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            ResetStick();
        }

        private void UpdateStickFromEvent(PointerEventData eventData)
        {
            if (backgroundRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    backgroundRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Vector2 rawNormalized = localPoint / Mathf.Max(1f, handleRange);
            float magnitude = rawNormalized.magnitude;
            Vector2 clamped = magnitude > 1f ? rawNormalized / magnitude : rawNormalized;

            if (clamped.magnitude < deadZone)
            {
                _value = Vector2.zero;
            }
            else
            {
                float remappedMag = (clamped.magnitude - deadZone) / (1f - deadZone);
                _value = clamped.normalized * Mathf.Clamp01(remappedMag);
            }

            if (handleRect != null)
            {
                handleRect.anchoredPosition = clamped * handleRange;
            }
        }

        private void ResetStick()
        {
            _activePointerId = int.MinValue;
            _value = Vector2.zero;
            if (handleRect != null)
            {
                handleRect.anchoredPosition = Vector2.zero;
            }
        }
    }
}
