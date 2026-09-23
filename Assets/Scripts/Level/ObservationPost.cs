// ObservationPost.cs — пост наблюдения: цель стратегии SCOUT.
using UnityEngine;

namespace CorridorRisk {

[RequireComponent(typeof(CircleCollider2D))]
public class ObservationPost : MonoBehaviour {
    public string id = "O?";
    public bool Visited { get; private set; }

    void Reset() {
        var c = GetComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 1.5f;
    }

    public void ResetPost()  { Visited = false; }
    public void MarkVisited() { Visited = true; }

    void OnDrawGizmos() {
        Gizmos.color = new Color(0.55f, 0.35f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}

}
