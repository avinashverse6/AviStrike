using CageDuel.Player;
using CageDuel.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace CageDuel.UI
{
    /// <summary>
    /// Renders a dynamic 4-tick spread crosshair plus a color-coded diagonal hit marker
    /// (white on body hit, gold on headshot, crimson on kill).
    /// </summary>
    public class HitMarkerUI : MonoBehaviour
    {
        [Header("Crosshair Ticks")]
        [SerializeField] private RectTransform topTick;
        [SerializeField] private RectTransform bottomTick;
        [SerializeField] private RectTransform leftTick;
        [SerializeField] private RectTransform rightTick;

        [Header("Diagonal Hit Marker Ticks")]
        [SerializeField] private Image[] hitMarkerTicks;
        [SerializeField] private RectTransform hitMarkerRoot;

        [Header("Tuning")]
        [SerializeField] private float minGapPixels = 10f;
        [SerializeField] private float pixelsPerSpreadDegree = 9.5f;
        [SerializeField] private float hitMarkerDuration = 0.20f;

        private HitscanWeapon _weapon;
        private float _hitTimer;
        private Color _activeHitColor = Color.white;
        private float _currentGap = 14f;

        public void Configure(
            RectTransform top,
            RectTransform bottom,
            RectTransform left,
            RectTransform right,
            RectTransform markerRoot,
            Image[] markerImages)
        {
            topTick = top;
            bottomTick = bottom;
            leftTick = left;
            rightTick = right;
            hitMarkerRoot = markerRoot;
            hitMarkerTicks = markerImages;
            SetHitMarkerAlpha(0f);
        }

        public void BindWeapon(HitscanWeapon weapon)
        {
            if (_weapon != null)
            {
                _weapon.OnHitConfirmed -= HandleHitConfirmed;
            }

            _weapon = weapon;

            if (_weapon != null)
            {
                _weapon.OnHitConfirmed += HandleHitConfirmed;
            }
        }

        private void Awake()
        {
            SetHitMarkerAlpha(0f);
        }

        private void OnDestroy()
        {
            if (_weapon != null)
            {
                _weapon.OnHitConfirmed -= HandleHitConfirmed;
            }
        }

        private void Update()
        {
            // Dynamic crosshair gap from weapon spread
            float desiredGap = minGapPixels + (_weapon != null ? _weapon.EffectiveSpreadDegrees * pixelsPerSpreadDegree : 4f);
            _currentGap = Mathf.Lerp(_currentGap, desiredGap, Time.deltaTime * 18f);

            if (topTick != null) topTick.anchoredPosition = new Vector2(0f, _currentGap);
            if (bottomTick != null) bottomTick.anchoredPosition = new Vector2(0f, -_currentGap);
            if (leftTick != null) leftTick.anchoredPosition = new Vector2(-_currentGap, 0f);
            if (rightTick != null) rightTick.anchoredPosition = new Vector2(_currentGap, 0f);

            // Fade hit marker
            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
                float norm = Mathf.Clamp01(_hitTimer / Mathf.Max(0.01f, hitMarkerDuration));
                SetHitMarkerAlpha(norm);

                if (hitMarkerRoot != null)
                {
                    float scale = Mathf.Lerp(1.0f, 1.28f, norm);
                    hitMarkerRoot.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        private void HandleHitConfirmed(DamageResult result)
        {
            if (result.KilledTarget)
            {
                _activeHitColor = new Color(1.0f, 0.25f, 0.28f, 1f);
                _hitTimer = hitMarkerDuration * 1.35f;
            }
            else if (result.IsHeadshot)
            {
                _activeHitColor = new Color(1.0f, 0.84f, 0.20f, 1f);
                _hitTimer = hitMarkerDuration * 1.15f;
            }
            else
            {
                _activeHitColor = Color.white;
                _hitTimer = hitMarkerDuration;
            }

            SetHitMarkerAlpha(1f);
        }

        private void SetHitMarkerAlpha(float alpha)
        {
            if (hitMarkerTicks == null)
            {
                return;
            }

            Color c = _activeHitColor;
            c.a = alpha;
            for (int i = 0; i < hitMarkerTicks.Length; i++)
            {
                if (hitMarkerTicks[i] != null)
                {
                    hitMarkerTicks[i].color = c;
                }
            }
        }
    }
}
