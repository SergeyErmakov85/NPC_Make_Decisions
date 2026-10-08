// WaypointMarker.cs — визуальная метка узла графа в редакторе.
using UnityEngine;

namespace CorridorRisk {

public class WaypointMarker : MonoBehaviour {
    public string id = "N?";

    void OnDrawGizmos() {
        Gizmos.color = id == "GOAL" ? Color.green : new Color(0.1f, 0.1f, 0.15f);
        Gizmos.DrawSphere(transform.position, 0.4f);
    }
}

}
