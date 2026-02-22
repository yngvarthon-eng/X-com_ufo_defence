using UnityEngine;
using System.Collections.Generic;

public class SquadManager : MonoBehaviour
{
    public List<GameObject> squadMembers = new List<GameObject>();

    public void AddMember(GameObject member)
    {
        squadMembers.Add(member);
        Debug.Log("Squad member added: " + member.name);
    }

    public void RemoveMember(GameObject member)
    {
        squadMembers.Remove(member);
        Debug.Log("Squad member removed: " + member.name);
    }

    public void DeploySquad(Vector3 position)
    {
        // TODO: Deploy squad to position
        Debug.Log("Squad deployed to " + position);
    }
}
