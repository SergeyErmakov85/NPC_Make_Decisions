// AgentTriggers.cs — подбор предметов и посещение постов.
using UnityEngine;

namespace CorridorRisk {

public class AgentTriggers : MonoBehaviour {

    [SerializeField] AgentState state;

    public int PickupsCollected { get; private set; }
    public int PostsVisited     { get; private set; }

    public void ResetCounters() { PickupsCollected = 0; PostsVisited = 0; }

    void OnTriggerEnter2D(Collider2D other) {
        var pickup = other.GetComponent<Pickup>();
        if (pickup != null && pickup.IsActive) {
            state.ApplyPickup(pickup);
            pickup.Consume();
            PickupsCollected++;
            return;
        }
        var post = other.GetComponent<ObservationPost>();
        if (post != null && !post.Visited) {
            post.MarkVisited();
            PostsVisited++;
        }
    }
}

}
