using UnityEngine;

/// <summary>
/// Idle animation for the X-Con saucer: the body spins slowly and the lights pulse.
/// Sits on the prefab root; spins the "Body" child so the UFO manager can still scale and move the root.
/// </summary>
public sealed class SaucerIdle : MonoBehaviour
{
    [SerializeField] private Transform body;
    [SerializeField] private float spinDegreesPerSecond = 35f;
    [SerializeField] private Color lightColor = new(1f, 0.62f, 0.22f);
    [SerializeField] private float lightMinIntensity = 0.9f;
    [SerializeField] private float lightMaxIntensity = 2.6f;
    [SerializeField] private float pulsesPerSecond = 0.9f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MeshRenderer bodyRenderer;
    private MaterialPropertyBlock block;
    private float phase;

    private void Awake()
    {
        if (body == null)
        {
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        bodyRenderer = body.GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        phase = Random.value * Mathf.PI * 2f; // Saucers on screen together don't blink in sync.
    }

    private void Update()
    {
        // Unscaled time keeps the saucer alive while the menu has paused the game.
        var dt = Time.unscaledDeltaTime;
        body.Rotate(0f, spinDegreesPerSecond * dt, 0f, Space.Self);

        if (bodyRenderer == null)
        {
            return;
        }

        phase += dt * pulsesPerSecond * Mathf.PI * 2f;
        var t = 0.5f + 0.5f * Mathf.Sin(phase);
        var intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, t);
        bodyRenderer.GetPropertyBlock(block, SaucerMeshBuilder.LightsSubmesh);
        block.SetColor(EmissionColorId, lightColor * intensity);
        bodyRenderer.SetPropertyBlock(block, SaucerMeshBuilder.LightsSubmesh);
    }
}
