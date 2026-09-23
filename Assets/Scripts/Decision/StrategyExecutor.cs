// StrategyExecutor.cs — исполнение стратегии: выбор цели, поиск пути, движение.
//
// КЛЮЧЕВАЯ ИДЕЯ: здесь нет слов «маршрут A/B/C». Стратегия задаёт только
// цель поиска и коэффициенты стоимости ребра; маршрут получается сам.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

public class StrategyExecutor : MonoBehaviour {

    [Header("Ссылки")]
    [SerializeField] LevelGraph          graph;
    [SerializeField] AgentState          state;
    [SerializeField] AgentMotor          motor;
    [SerializeField] DecisionPolicy      policy;
    [SerializeField] StrategyLabel       label;
    [SerializeField] CorridorRiskBalance balance;
    [SerializeField] EpisodeLogger       logger;

    [Header("Режим")]
    [Tooltip("Включается на уровне B: стратегию задаёт ML-Agents, а не таблица политики.")]
    public bool externalControl = false;

    [Tooltip("Если задано — стратегия не берётся из политики, а фиксируется. Для приёмочных проверок.")]
    [SerializeField] string forcedStrategy = "";

    public string CurrentStrategy { get; private set; } = "HOLD";
    public int    StrategyChanges { get; private set; }
    public int    Decisions       { get; private set; }
    public string LastStateKey    { get; private set; } = "";

    float _timer;
    bool  _running;

    // Коэффициенты стоимости ребра и множители скорости — таблица из §6.2 спецификации.
    struct Profile { public PathWeights w; public float speed; }

    static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile> {
        { "RUSH",    new Profile { w = new PathWeights(0.0f, 0.0f), speed = 1.00f } },
        { "STEALTH", new Profile { w = new PathWeights(2.5f, 1.2f), speed = 0.55f } },
        { "SCOUT",   new Profile { w = new PathWeights(1.5f, 0.8f), speed = 0.75f } },
        { "FARM",    new Profile { w = new PathWeights(1.8f, 0.8f), speed = 0.80f } },
        { "HOLD",    new Profile { w = new PathWeights(3.0f, 1.5f), speed = 0.35f } },
        { "RETREAT", new Profile { w = new PathWeights(3.5f, 1.0f), speed = 0.95f } },
    };

    public void BeginEpisode() {
        _running = true;
        _timer = 0f;
        StrategyChanges = 0;
        Decisions = 0;
        CurrentStrategy = "HOLD";
        motor.Stop();
    }

    public void EndEpisode() { _running = false; motor.Stop(); }

    /// Уровень B: стратегию назначает ML-Agents.
    public void SetStrategyExternal(string s) {
        if (s != CurrentStrategy) { CurrentStrategy = s; StrategyChanges++; }
        Decisions++;
        LastStateKey = policy.StateKey(state.Hp01, state.Dist01, state.Threat01,
                                       state.Res01, state.Cover01);
        Retarget();
        if (label != null) label.Show(CurrentStrategy, 1f, false, LastStateKey);
    }

    void Update() {
        if (!_running || externalControl) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = balance.decisionInterval;
        Decide();
    }

    void Decide() {
        Decisions++;
        PolicyEntry entry;
        string next;

        if (!string.IsNullOrEmpty(forcedStrategy)) {
            next = forcedStrategy;
            entry = new PolicyEntry { key = "forced", strategy = next, margin = 1f, ambiguous = false };
            LastStateKey = policy.StateKey(state.Hp01, state.Dist01, state.Threat01,
                                           state.Res01, state.Cover01);
        } else {
            next = policy.Decide(state.Hp01, state.Dist01, state.Threat01,
                                 state.Res01, state.Cover01, out entry);
            LastStateKey = entry.key ?? "?";
        }

        bool changed = false;
        // Гистерезис: не меняем стратегию, если новая выигрывает еле-еле.
        // Без него агент «дрожит» на границах бинов.
        if (next != CurrentStrategy && entry.margin >= balance.hysteresis) {
            CurrentStrategy = next;
            StrategyChanges++;
            changed = true;
        }

        Retarget();                                   // путь пересчитываем каждое решение:
                                                      // цели (предметы, посты) могли измениться
        if (label != null)
            label.Show(CurrentStrategy, entry.margin, entry.ambiguous, LastStateKey);
        if (logger != null)
            logger.LogDecision(state, entry, CurrentStrategy, LastStateKey, changed, transform.position);
    }

    void Retarget() {
        var prof = Profiles.TryGetValue(CurrentStrategy, out var p)
                 ? p : Profiles["HOLD"];
        int target = TargetNodeFor(CurrentStrategy);

        if (CurrentStrategy == "HOLD" && target == graph.NearestNode(transform.position)) {
            motor.Stop();                             // уже в укрытии — стоим
            return;
        }

        var path = graph.FindPath(transform.position, target, prof.w,
                                  state.globalThreatScale, state.ambientThreat);
        motor.SetPath(path, prof.speed);
    }

    int TargetNodeFor(string strategy) {
        Vector2 p = transform.position;
        switch (strategy) {
            case "RUSH":
            case "STEALTH":
                return graph.IndexOf("GOAL");

            case "SCOUT": {
                int best = -1; float bestD = float.MaxValue;
                foreach (var o in LevelRegistry.Posts) {
                    if (o.Visited) continue;
                    int n = graph.NearestNode(o.transform.position);
                    float d = Vector2.Distance(p, o.transform.position);
                    if (d < bestD) { bestD = d; best = n; }
                }
                return best >= 0 ? best : graph.IndexOf("GOAL");
            }

            case "FARM": {
                bool needMed = state.Hp01 < 0.5f;
                int best = -1; float bestD = float.MaxValue;
                foreach (var pu in LevelRegistry.Pickups) {
                    if (!pu.IsActive) continue;
                    if (pu.kind == PickupKind.Med && !needMed) continue;
                    float d = Vector2.Distance(p, pu.transform.position);
                    if (d < bestD) { bestD = d; best = graph.NearestNode(pu.transform.position); }
                }
                return best >= 0 ? best : graph.IndexOf("GOAL");
            }

            case "HOLD": {
                int best = graph.NearestNode(p); float bestCover = graph.NodeCover(best);
                for (int i = 0; i < graph.NodeCount; i++) {
                    if (Vector2.Distance(p, graph.PositionOf(i)) > 12f) continue;
                    if (graph.NodeCover(i) > bestCover) { bestCover = graph.NodeCover(i); best = i; }
                }
                return best;
            }

            case "RETREAT": {
                var safe = LevelRegistry.Safe;
                if (safe != null && Vector2.Distance(p, safe.transform.position) < 20f)
                    return graph.NearestNode(safe.transform.position);
                // иначе — ближайший узел «назад» (дальше от цели) с минимальной угрозой
                int cur = graph.NearestNode(p);
                int best = cur; float bestThreat = float.MaxValue;
                for (int i = 0; i < graph.NodeCount; i++) {
                    if (Vector2.Distance(p, graph.PositionOf(i)) > 20f) continue;
                    if (graph.NodeDist01(i) <= graph.NodeDist01(cur)) continue;
                    float t = graph.NodeThreatRaw(i);
                    if (t < bestThreat) { bestThreat = t; best = i; }
                }
                return best;
            }
        }
        return graph.IndexOf("GOAL");
    }
}

}
