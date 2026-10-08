// AgentTriggers.cs — посещение родников, ягодников и смотровых точек.
using UnityEngine;

namespace CorridorRisk {

public class AgentTriggers : MonoBehaviour {

    [SerializeField] AgentState state;

    public int SuppliesCollected { get; private set; }   // сколько раз пополнил запасы или подкрепился
    public int ViewpointsVisited { get; private set; }   // сколько смотровых точек посетил

    public void ResetCounters() { SuppliesCollected = 0; ViewpointsVisited = 0; }

    void OnTriggerEnter2D(Collider2D other) {
        var supply = other.GetComponent<SupplyPoint>();
        if (supply != null && supply.IsActive) {
            state.ApplySupply(supply);
            supply.Consume();
            SuppliesCollected++;
            return;
        }
        var vp = other.GetComponent<Viewpoint>();
        if (vp != null && !vp.Visited) {
            vp.MarkVisited();
            ViewpointsVisited++;
        }
    }
}

}
