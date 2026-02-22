using UnityEngine;

public class UFOManager : MonoBehaviour
{
    public void SpawnUFO(Vector3 position)
    {
        // TODO: Instantiate UFO prefab at position
        Debug.Log("UFO spawned at " + position);
    }

    public void MoveUFO(GameObject ufo, Vector3 targetPosition)
    {
        // TODO: Move UFO to target position
        ufo.transform.position = targetPosition;
        Debug.Log("UFO moved to " + targetPosition);
    }
}
