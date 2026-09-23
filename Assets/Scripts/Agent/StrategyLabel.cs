// StrategyLabel.cs — подпись текущей стратегии над агентом и след траектории.
// Без этого на записи экрана невозможно понять, что происходит.
using UnityEngine;

namespace CorridorRisk {

public class StrategyLabel : MonoBehaviour {

    [SerializeField] TMPro.TextMeshPro label;     // world-space текст над агентом
    [SerializeField] TrailRenderer     trail;
    [SerializeField] SpriteRenderer    body;

    // Палитра совпадает со схемой уровня CorridorRisk2D_layout.png
    static readonly (string name, Color color)[] Palette = {
        ("RUSH",    new Color(0.94f, 0.27f, 0.27f)),
        ("STEALTH", new Color(0.06f, 0.65f, 0.91f)),
        ("SCOUT",   new Color(0.55f, 0.36f, 0.96f)),
        ("FARM",    new Color(0.96f, 0.62f, 0.07f)),
        ("HOLD",    new Color(0.13f, 0.77f, 0.37f)),
        ("RETREAT", new Color(0.42f, 0.45f, 0.50f)),
    };

    public static Color ColorOf(string strategy) {
        foreach (var p in Palette) if (p.name == strategy) return p.color;
        return Color.white;
    }

    public void Show(string strategy, float margin, bool ambiguous, string stateKey) {
        Color c = ColorOf(strategy);
        if (label != null) {
            label.text  = ambiguous ? $"{strategy} ?\n{stateKey}" : $"{strategy}\n{stateKey}";
            label.color = c;
        }
        if (trail != null) { trail.startColor = c; trail.endColor = new Color(c.r, c.g, c.b, 0f); }
        if (body  != null) body.color = c;
    }
}

}
