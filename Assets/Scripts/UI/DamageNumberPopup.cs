using CageDuel.Player;
using TMPro;
using UnityEngine;

namespace CageDuel.UI
{
    /// <summary>
    /// Pooled floating damage number overlay projected from 3D world hit coordinates onto the HUD Canvas.
    /// </summary>
    public class DamageNumberPopup : MonoBehaviour
    {
        private struct ActiveEntry
        {
            public bool InUse;
            public RectTransform Rect;
            public TextMeshProUGUI Label;
            public Vector3 BaseWorldPosition;
            public Vector2 ScreenJitter;
            public float Elapsed;
            public float Duration;
            public Color BaseColor;
        }

        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameObject localPlayerObject;
        [SerializeField] private int poolSize = 16;
        [SerializeField] private float floatDuration = 0.78f;
        [SerializeField] private float riseWorldMeters = 0.95f;

        private ActiveEntry[] _pool;
        private int _cursor;
        private Canvas _rootCanvas;

        public void Configure(RectTransform container, Camera cam, GameObject localPlayer)
        {
            containerRect = container;
            worldCamera = cam;
            localPlayerObject = localPlayer;
        }

        private void Awake()
        {
            if (containerRect == null)
            {
                containerRect = GetComponent<RectTransform>();
            }
            _rootCanvas = GetComponentInParent<Canvas>();
            BuildPool();
        }

        private void OnEnable()
        {
            CombatEvents.OnAnyDamageApplied += HandleAnyDamageApplied;
        }

        private void OnDisable()
        {
            CombatEvents.OnAnyDamageApplied -= HandleAnyDamageApplied;
        }

        private void BuildPool()
        {
            int count = Mathf.Max(4, poolSize);
            _pool = new ActiveEntry[count];

            for (int i = 0; i < count; i++)
            {
                var itemGo = new GameObject("DmgNum_" + i, typeof(RectTransform));
                itemGo.transform.SetParent(containerRect, false);

                var rt = itemGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(160f, 44f);

                var label = itemGo.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.fontSize = 26f;
                label.raycastTarget = false;
                label.enableWordWrapping = false;
                itemGo.SetActive(false);

                _pool[i] = new ActiveEntry
                {
                    InUse = false,
                    Rect = rt,
                    Label = label
                };
            }
        }

        private void HandleAnyDamageApplied(GameObject target, DamageInfo info, DamageResult result)
        {
            if (!result.Applied || _pool == null || _pool.Length == 0)
            {
                return;
            }

            // Avoid spawning floating world numbers directly on the local player's own camera body
            if (localPlayerObject != null && target == localPlayerObject)
            {
                return;
            }

            int idx = _cursor % _pool.Length;
            _cursor++;

            ref ActiveEntry entry = ref _pool[idx];
            entry.InUse = true;
            entry.BaseWorldPosition = result.HitPoint;
            entry.ScreenJitter = new Vector2(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(4f, 16f));
            entry.Elapsed = 0f;
            entry.Duration = floatDuration;

            int roundedDamage = Mathf.Max(1, Mathf.RoundToInt(result.TotalDamageDealt));
            if (result.KilledTarget)
            {
                entry.BaseColor = new Color(1.0f, 0.28f, 0.30f, 1f);
                entry.Label.text = roundedDamage + " KO";
                entry.Label.fontSize = 31f;
            }
            else if (result.IsHeadshot)
            {
                entry.BaseColor = new Color(1.0f, 0.84f, 0.20f, 1f);
                entry.Label.text = roundedDamage + "!";
                entry.Label.fontSize = 30f;
            }
            else if (result.ArmorAbsorbed >= result.HealthLost && result.ArmorAbsorbed > 0.1f)
            {
                entry.BaseColor = new Color(0.32f, 0.84f, 1.0f, 1f);
                entry.Label.text = roundedDamage.ToString();
                entry.Label.fontSize = 25f;
            }
            else
            {
                entry.BaseColor = Color.white;
                entry.Label.text = roundedDamage.ToString();
                entry.Label.fontSize = 25f;
            }

            entry.Label.color = entry.BaseColor;
            entry.Rect.gameObject.SetActive(true);
            UpdateSingleEntryPosition(ref entry);
        }

        private void LateUpdate()
        {
            if (_pool == null)
            {
                return;
            }

            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].InUse)
                {
                    continue;
                }

                _pool[i].Elapsed += Time.deltaTime;
                if (_pool[i].Elapsed >= _pool[i].Duration)
                {
                    _pool[i].InUse = false;
                    _pool[i].Rect.gameObject.SetActive(false);
                    continue;
                }

                UpdateSingleEntryPosition(ref _pool[i]);
            }
        }

        private void UpdateSingleEntryPosition(ref ActiveEntry entry)
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null || containerRect == null)
            {
                return;
            }

            float t = Mathf.Clamp01(entry.Elapsed / Mathf.Max(0.01f, entry.Duration));
            Vector3 worldPos = entry.BaseWorldPosition + Vector3.up * (t * riseWorldMeters);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0.05f)
            {
                entry.Label.enabled = false;
                return;
            }

            entry.Label.enabled = true;
            Camera uiCamera = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _rootCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    containerRect,
                    new Vector2(screenPos.x, screenPos.y),
                    uiCamera,
                    out Vector2 localPos))
            {
                entry.Rect.anchoredPosition = localPos + entry.ScreenJitter;
            }

            Color c = entry.BaseColor;
            c.a = 1f - Mathf.SmoothStep(0.45f, 1f, t);
            entry.Label.color = c;
        }
    }
}
