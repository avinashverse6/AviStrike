using CageDuel.Player;
using CageDuel.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CageDuel.UI
{
    /// <summary>
    /// Binds player vitals, weapon ammo/reload state, death/respawn banner, and desktop mouse-lock toggle
    /// to the generated uGUI HUD.
    /// </summary>
    public class PlayerHudController : MonoBehaviour
    {
        [Header("Player Bindings")]
        [SerializeField] private HealthArmor playerVitals;
        [SerializeField] private HitscanWeapon playerWeapon;
        [SerializeField] private PlayerInputRouter inputRouter;
        [SerializeField] private HitMarkerUI hitMarkerUI;

        [Header("Vitals UI")]
        [SerializeField] private RectTransform healthFillRect;
        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private RectTransform armorFillRect;
        [SerializeField] private TMP_Text armorValueText;

        [Header("Weapon & Ammo UI")]
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text ammoValueText;
        [SerializeField] private GameObject reloadBarRoot;
        [SerializeField] private RectTransform reloadFillRect;

        [Header("Status & Editor Mode UI")]
        [SerializeField] private GameObject deathBannerRoot;
        [SerializeField] private TMP_Text deathBannerText;
        [SerializeField] private Button mouseLockToggleButton;
        [SerializeField] private TMP_Text mouseLockButtonText;
        [SerializeField] private GameObject desktopHintRoot;

        public void Configure(
            HealthArmor vitals,
            HitscanWeapon weapon,
            PlayerInputRouter router,
            HitMarkerUI hitMarker,
            RectTransform hpFill,
            TMP_Text hpText,
            RectTransform apFill,
            TMP_Text apText,
            TMP_Text gunNameLabel,
            TMP_Text ammoLabel,
            GameObject reloadRoot,
            RectTransform reloadFill,
            GameObject eliminatedRoot,
            TMP_Text eliminatedText,
            Button cursorLockBtn,
            TMP_Text cursorLockLabel,
            GameObject pcHintRoot)
        {
            playerVitals = vitals;
            playerWeapon = weapon;
            inputRouter = router;
            hitMarkerUI = hitMarker;
            healthFillRect = hpFill;
            healthValueText = hpText;
            armorFillRect = apFill;
            armorValueText = apText;
            weaponNameText = gunNameLabel;
            ammoValueText = ammoLabel;
            reloadBarRoot = reloadRoot;
            reloadFillRect = reloadFill;
            deathBannerRoot = eliminatedRoot;
            deathBannerText = eliminatedText;
            mouseLockToggleButton = cursorLockBtn;
            mouseLockButtonText = cursorLockLabel;
            desktopHintRoot = pcHintRoot;
        }

        private void Start()
        {
            if (hitMarkerUI != null && playerWeapon != null)
            {
                hitMarkerUI.BindWeapon(playerWeapon);
            }

            if (playerVitals != null)
            {
                playerVitals.OnVitalsChanged += HandleVitalsChanged;
                HandleVitalsChanged(
                    playerVitals.CurrentHealth,
                    playerVitals.MaxHealth,
                    playerVitals.CurrentArmor,
                    playerVitals.MaxArmor);
            }

            if (playerWeapon != null)
            {
                playerWeapon.OnAmmoStateChanged += HandleAmmoStateChanged;
                if (weaponNameText != null)
                {
                    weaponNameText.text = playerWeapon.WeaponName.ToUpperInvariant();
                }
                HandleAmmoStateChanged(
                    playerWeapon.CurrentInMag,
                    playerWeapon.ReserveAmmo,
                    playerWeapon.IsReloading);
            }

            if (mouseLockToggleButton != null)
            {
                if (Application.isMobilePlatform)
                {
                    mouseLockToggleButton.gameObject.SetActive(false);
                }
                else
                {
                    mouseLockToggleButton.onClick.AddListener(OnMouseLockButtonClicked);
                }
            }

            if (desktopHintRoot != null && Application.isMobilePlatform)
            {
                desktopHintRoot.SetActive(false);
            }

            if (inputRouter != null)
            {
                inputRouter.OnMouseLockChanged += RefreshMouseLockLabel;
                RefreshMouseLockLabel(inputRouter.IsMouseLocked);
            }

            if (deathBannerRoot != null)
            {
                deathBannerRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (playerVitals != null)
            {
                playerVitals.OnVitalsChanged -= HandleVitalsChanged;
            }

            if (playerWeapon != null)
            {
                playerWeapon.OnAmmoStateChanged -= HandleAmmoStateChanged;
            }

            if (inputRouter != null)
            {
                inputRouter.OnMouseLockChanged -= RefreshMouseLockLabel;
            }

            if (mouseLockToggleButton != null)
            {
                mouseLockToggleButton.onClick.RemoveListener(OnMouseLockButtonClicked);
            }
        }

        private void Update()
        {
            // Reload progress bar
            if (playerWeapon != null && reloadBarRoot != null)
            {
                bool reloading = playerWeapon.IsReloading;
                if (reloadBarRoot.activeSelf != reloading)
                {
                    reloadBarRoot.SetActive(reloading);
                }

                if (reloading && reloadFillRect != null)
                {
                    SetHorizontalFill(reloadFillRect, playerWeapon.ReloadProgress01);
                }
            }

            // Death & sandbox respawn countdown banner
            if (playerVitals != null && deathBannerRoot != null)
            {
                bool dead = playerVitals.IsDead;
                if (deathBannerRoot.activeSelf != dead)
                {
                    deathBannerRoot.SetActive(dead);
                }

                if (dead && deathBannerText != null)
                {
                    if (playerVitals.AutoRespawnInSandbox)
                    {
                        deathBannerText.text = string.Format(
                            "ELIMINATED\n<size=65%>Respawning in {0:0.0}s...</size>",
                            playerVitals.RespawnRemainingSeconds);
                    }
                    else
                    {
                        deathBannerText.text = "ELIMINATED";
                    }
                }
            }
        }

        private void HandleVitalsChanged(float currentHp, float maxHp, float currentArmor, float maxArmor)
        {
            float hpRatio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
            float apRatio = maxArmor > 0f ? Mathf.Clamp01(currentArmor / maxArmor) : 0f;

            if (healthFillRect != null) SetHorizontalFill(healthFillRect, hpRatio);
            if (armorFillRect != null) SetHorizontalFill(armorFillRect, apRatio);

            if (healthValueText != null)
            {
                healthValueText.text = string.Format("HP  {0} / {1}", Mathf.CeilToInt(currentHp), Mathf.RoundToInt(maxHp));
            }

            if (armorValueText != null)
            {
                armorValueText.text = string.Format("ARMOR  {0} / {1}", Mathf.CeilToInt(currentArmor), Mathf.RoundToInt(maxArmor));
            }
        }

        private void HandleAmmoStateChanged(int inMag, int reserve, bool reloading)
        {
            if (ammoValueText != null)
            {
                ammoValueText.text = reloading
                    ? string.Format("RELOADING  <color=#99AABB>({0})</color>", reserve)
                    : string.Format("{0} <size=70%><color=#99AABB>/ {1}</color></size>", inMag, reserve);
            }
        }

        private void OnMouseLockButtonClicked()
        {
            if (inputRouter != null)
            {
                inputRouter.ToggleMouseLock();
            }
        }

        private void RefreshMouseLockLabel(bool isLocked)
        {
            if (mouseLockButtonText == null)
            {
                return;
            }

            mouseLockButtonText.text = isLocked
                ? "[TAB] Mouse Aim: LOCKED (Esc Unlock)"
                : "[TAB] Mouse Aim: OFF (Touch UI Mode)";
        }

        private static void SetHorizontalFill(RectTransform fillRect, float normalizedRatio)
        {
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalizedRatio), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }
    }
}
