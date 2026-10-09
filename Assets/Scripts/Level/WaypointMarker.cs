// WaypointMarker.cs — визуальная метка узла графа тропинок в редакторе.
using UnityEngine;

namespace CorridorRisk {

public class WaypointMarker : MonoBehaviour {
    public string id = "N?";

    void OnDrawGizmosSelected() {
        Gizmos.color = id == "HUT" ? Color.green : new Color(0.1f, 0.1f, 0.15f);
        Gizmos.DrawSphere(transform.position, 0.4f);
    }
}

}
