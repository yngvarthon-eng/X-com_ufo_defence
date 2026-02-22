using UnityEngine;

public class BaseManager : MonoBehaviour
{
    public void BuildBase(Vector3 position)
    {
        // TODO: Instantiate base prefab at position
        Debug.Log("Base built at " + position);
    }

    public void UpgradeBase(GameObject baseObj)
    {
        // TODO: Upgrade base logic
        Debug.Log("Base upgraded: " + baseObj.name);
    }
}
