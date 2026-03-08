
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Vector2 moveInput;
    private Rigidbody rb;

    private static Vector2 GetKeyboardMoveFallback(Vector2 current)
    {
        // Only apply fallback if we currently have no input.
        if (current.sqrMagnitude > 0.0001f)
        {
            return current;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return current;
        }

        var x = 0f;
        var y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        return new Vector2(x, y);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Common top-down defaults; keeps physics from tipping the player over.
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

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

    private void Update()
    {
        // If we have a Rigidbody, we move in FixedUpdate.
        if (rb != null)
        {
            return;
        }

        var effectiveInput = GetKeyboardMoveFallback(moveInput);
        Vector2 clampedInput = Vector2.ClampMagnitude(effectiveInput, 1f);
        Vector3 move = new Vector3(clampedInput.x, 0f, clampedInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        var effectiveInput = GetKeyboardMoveFallback(moveInput);
        Vector2 clampedInput = Vector2.ClampMagnitude(effectiveInput, 1f);
        var move = new Vector3(clampedInput.x, 0f, clampedInput.y);

        if (rb.isKinematic)
        {
            rb.MovePosition(rb.position + move * (moveSpeed * Time.fixedDeltaTime));
        }
        else
        {
            // Drive velocity directly for dynamic rigidbodies.
            var v = move * moveSpeed;
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }
    }
}