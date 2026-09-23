// Zones.cs — безопасная зона и точка эвакуации. Оба — просто круги.
using UnityEngine;

namespace CorridorRisk {

public class SafeZone : MonoBehaviour {
    public float radius = 6f;
    public float hpRegenPerSecond = 3f;
    public bool Contains(Vector2 p) => Vector2.Distance(p, (Vector2)transform.position) <= radius;

    void OnDrawGizmos() {
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

public class GoalZone : MonoBehaviour {
    public float radius = 2.5f;
    public bool Contains(Vector2 p) => Vector2.Distance(p, (Vector2)transform.position) <= radius;

    void OnDrawGizmos() {
        Gizmos.color = new Color(0.15f, 0.85f, 0.35f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

}
