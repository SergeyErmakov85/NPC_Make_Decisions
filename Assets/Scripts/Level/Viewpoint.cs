// Viewpoint.cs — смотровая точка: цель варианта SURVEY («осмотреться»).
// Поднявшись сюда, путешественник узнаёт о местности больше.
using UnityEngine;

namespace CorridorRisk {

[RequireComponent(typeof(CircleCollider2D))]
public class Viewpoint : MonoBehaviour {
    public string id = "V?";
    public bool Visited { get; private set; }

    void Reset() {
        var c = GetComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 1.5f;
    }

    public void ResetViewpoint() { Visited = false; }
    public void MarkVisited()    { Visited = true; }

    void OnDrawGizmos() {
        Gizmos.color = new Color(0.55f, 0.35f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}

}
