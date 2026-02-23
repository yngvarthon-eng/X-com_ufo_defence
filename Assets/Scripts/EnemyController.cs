using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float moveSpeed = 2f;
    public Transform target;

    void Update()
    {
        if (target != null)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;
        }
    }
}