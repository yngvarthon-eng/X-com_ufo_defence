
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Vector2 moveInput;
    private Rigidbody rb;

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

        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
        Vector3 move = new Vector3(clampedInput.x, 0f, clampedInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
        var move = new Vector3(clampedInput.x, 0f, clampedInput.y);

        if (rb.isKinematic)
        {
            rb.MovePosition(rb.position + move * (moveSpeed * Time.fixedDeltaTime));
        }
        else
        {
            // Drive velocity directly for dynamic rigidbodies.
            var v = move * moveSpeed;
            rb.velocity = new Vector3(v.x, rb.velocity.y, v.z);
        }
    }
}