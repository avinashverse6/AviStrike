using UnityEngine;
using UnityEngine.EventSystems;

namespace CageDuel.UI
{
    /// <summary>
    /// Transparent right-half touch area that captures swipe deltas for camera aiming
    /// without interfering with simultaneous virtual joystick or button touches.
    /// </summary>
    public class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private int _activePointerId = int.MinValue;
        private Vector2 _accumulatedDelta;

        public bool IsDragging => _activePointerId != int.MinValue;

        private void OnDisable()
        {
            _activePointerId = int.MinValue;
            _accumulatedDelta = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue)
            {
                return;
            }

            _activePointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            _accumulatedDelta += eventData.delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            _activePointerId = int.MinValue;
        }

        public Vector2 ConsumeLookDelta()
        {
            Vector2 delta = _accumulatedDelta;
            _accumulatedDelta = Vector2.zero;
            return delta;
        }
    }
}
