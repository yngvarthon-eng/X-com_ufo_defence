
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Vector2 moveInput;

    // Compatible with PlayerInput "Send Messages" behavior.
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // Compatible with PlayerInput "Invoke Unity Events" behavior.
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
        Vector3 move = new Vector3(clampedInput.x, 0f, clampedInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }
}