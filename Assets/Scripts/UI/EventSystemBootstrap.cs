using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CageDuel.UI
{
    /// <summary>
    /// Ensures the active EventSystem always uses the UI Input Module matching
    /// the project's Active Input Handling setting.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(EventSystem))]
    public class EventSystemBootstrap : MonoBehaviour
    {
        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            var legacy = GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Destroy(legacy);
            }

            var inputSystemModule = GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = gameObject.AddComponent<InputSystemUIInputModule>();
            }
            inputSystemModule.AssignDefaultActions();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (GetComponent<StandaloneInputModule>() == null)
            {
                gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }
    }
}
