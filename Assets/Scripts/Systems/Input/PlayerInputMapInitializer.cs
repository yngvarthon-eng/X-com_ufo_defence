using UnityEngine;
using UnityEngine.InputSystem;

namespace XCon.Systems.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputMapInitializer : MonoBehaviour
    {
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
            }
        }
    }
}
