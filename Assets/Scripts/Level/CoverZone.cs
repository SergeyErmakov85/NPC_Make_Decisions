// CoverZone.cs — зона укрытий. Плотность максимальна в центре
// и к краю падает на 35 %.
using UnityEngine;

namespace CorridorRisk {

public class CoverZone : MonoBehaviour {
    public string id = "CV?";
    public float radius = 6f;
    [Range(0f, 1f)] public float density = 0.5f;

    public float Evaluate(Vector2 p) {
        float d = Vector2.Distance(p, (Vector2)transform.position);
        if (d > radius) return 0f;
        return density * (1f - 0.35f * d / radius);
    }

    void OnDrawGizmos() {
        Gizmos.color = new Color(0.1f, 0.8f, 0.3f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

}
