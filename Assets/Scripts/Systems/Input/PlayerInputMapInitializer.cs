using UnityEngine;
using UnityEngine.InputSystem;

namespace XCon.Systems.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    [AddComponentMenu("XCon/Input/Player Input Map Initializer")]
    public sealed class PlayerInputMapInitializer : MonoBehaviour
    {
        [Header("Recommended Defaults")]
        [SerializeField] private bool cloneActionsPerPlayer = true;
        [SerializeField] private bool enforceSingleActionMapEnabled = true;

        [SerializeField] private string overrideDefaultActionMap;

        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                return;
            }

            var actions = playerInput.actions;
            if (actions == null)
            {
                return;
            }

            if (cloneActionsPerPlayer)
            {
                // Project-wide action assets are shared singletons.
                // Cloning avoids cross-talk when maps are enabled/disabled and matches PlayerInput guidance.
                var cloned = Instantiate(actions);
                cloned.name = $"{actions.name} (Runtime)";
                cloned.hideFlags = HideFlags.DontSave;
                playerInput.actions = cloned;
                actions = cloned;
            }

            if (!enforceSingleActionMapEnabled)
            {
                return;
            }

            // Unity warning context:
            // Project-wide action assets are singletons and may have all maps enabled by default.
            // Ensure we start from a known state.
            actions.Disable();

            var mapName = !string.IsNullOrWhiteSpace(overrideDefaultActionMap)
                ? overrideDefaultActionMap
                : playerInput.defaultActionMap;

            if (string.IsNullOrWhiteSpace(mapName))
            {
                // Fall back to first map if no default is defined.
                if (actions.actionMaps.Count > 0)
                {
                    mapName = actions.actionMaps[0].name;
                }
            }

            if (!string.IsNullOrWhiteSpace(mapName))
            {
                playerInput.SwitchCurrentActionMap(mapName);

                // Be explicit: ensure the selected map is enabled.
                // (In some configurations, switching maps may not result in an enabled current map.)
                playerInput.currentActionMap?.Enable();
            }
        }
    }
}
