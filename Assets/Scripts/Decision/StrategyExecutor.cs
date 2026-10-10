// StrategyExecutor.cs — выполнение выбранного варианта действий:
// выбор цели, поиск пути, ходьба.
//
// КЛЮЧЕВАЯ ИДЕЯ: здесь нет слов «тропа A/B/C». Вариант действий задаёт только
// цель поиска и коэффициенты стоимости ребра; тропа выбирается сама.
//
//   DIRECT    — идти к приюту напрямик, не обращая внимания на погоду;
//   SHELTERED — идти к приюту защищённой тропой: через лес и мимо навесов;
//   SURVEY    — подняться на ближайшую смотровую точку, осмотреться;
//   GATHER    — дойти до родника (или ягодника, если сил мало);
//   WAIT      — укрыться поблизости и переждать непогоду;
//   RETURN    — вернуться к стоянке или назад, туда, где тише.
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

    public string CurrentStrategy { get; private set; } = "WAIT";
    public int    StrategyChanges { get; private set; }
    public int    Decisions       { get; private set; }
    public string LastStateKey    { get; private set; } = "";

    /// Ключ ситуации, заданный вручную (панель SituationPanel). Пока он не пуст,
    /// политика опрашивается по нему, а не по реальному состоянию путешественника.
    public string OverrideKey     { get; private set; } = "";

    // Коэффициенты стоимости ребра (kWeather, kShelter) и множители скорости —
    // таблица из §6.2 спецификации. Их можно править в инспекторе прямо во время игры:
    // путь пересчитывается сразу.
    [System.Serializable]
    public struct Profile { public string strategy; public PathWeights w; public float speed; }

    [Header("Веса пути по вариантам действий (меняются на лету)")]
    [SerializeField] Profile[] profiles = {
        new Profile { strategy = "DIRECT",    w = new PathWeights(0.0f, 0.0f), speed = 1.00f },
        new Profile { strategy = "SHELTERED", w = new PathWeights(2.5f, 1.2f), speed = 0.55f },
        new Profile { strategy = "SURVEY",    w = new PathWeights(1.5f, 0.8f), speed = 0.75f },
        new Profile { strategy = "GATHER",    w = new PathWeights(1.8f, 0.8f), speed = 0.80f },
        new Profile { strategy = "WAIT",      w = new PathWeights(3.0f, 1.5f), speed = 0.35f },
        new Profile { strategy = "RETURN",    w = new PathWeights(3.5f, 1.0f), speed = 0.95f },
    };

    float _timer;
    bool  _running;
    bool  _forceNext;
    bool  _settingsChanged;           // в инспекторе поменяли веса или forcedStrategy

    // Правка в инспекторе во время игры. Само решение принимаем в Update, а не здесь:
    // OnValidate вызывается посреди сериализации, трогать из него сцену нельзя.
    void OnValidate() { if (Application.isPlaying) _settingsChanged = true; }

    void OnEnable()  { if (policy != null) policy.Reloaded += OnPolicyReloaded; }
    void OnDisable() { if (policy != null) policy.Reloaded -= OnPolicyReloaded; }
    void OnPolicyReloaded() { _settingsChanged = true; }

    bool TryGetProfile(string strategy, out Profile p) {
        foreach (var x in profiles) if (x.strategy == strategy) { p = x; return true; }
        p = default; return false;
    }

    public void BeginEpisode() {
        _running = true;
        _timer = 0f;
        StrategyChanges = 0;
        Decisions = 0;
        CurrentStrategy = "WAIT";
        motor.Stop();
    }

    public void EndEpisode() { _running = false; motor.Stop(); }

    public void SetOverrideKey(string key) { OverrideKey = key ?? ""; }

    /// Принять решение немедленно (работает и на паузе). force — без гистерезиса.
    public void DecideNow(bool force) {
        if (!_running || externalControl) return;
        _forceNext = force;
        _timer = balance.decisionInterval;
        Decide();
    }

    /// Уровень B: стратегию назначает ML-Agents.
    public void SetStrategyExternal(string s) {
        if (s != CurrentStrategy) { CurrentStrategy = s; StrategyChanges++; }
        Decisions++;
        LastStateKey = policy.StateKey(state.Energy01, state.Dist01, state.Weather01,
                                       state.Supplies01, state.Shelter01);
        Retarget();
        if (label != null) label.Show(CurrentStrategy, 1f, false, LastStateKey);
    }

    void Update() {
        if (_settingsChanged) {                       // работает и на паузе
            _settingsChanged = false;
            if (_running && !externalControl) { DecideNow(true); return; }
            if (_running) Retarget();
        }
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
            LastStateKey = policy.StateKey(state.Energy01, state.Dist01, state.Weather01,
                                           state.Supplies01, state.Shelter01);
        } else if (!string.IsNullOrEmpty(OverrideKey)) {
            next = policy.DecideByKey(OverrideKey, out entry);
            if (entry == null)
                entry = new PolicyEntry { key = OverrideKey, strategy = next, margin = 1f, ambiguous = false };
            LastStateKey = OverrideKey;
        } else {
            next = policy.Decide(state.Energy01, state.Dist01, state.Weather01,
                                 state.Supplies01, state.Shelter01, out entry);
            if (entry == null)                        // состояния нет в таблице — fallback
                entry = new PolicyEntry { key = "?", strategy = next, margin = 1f, ambiguous = false };
            LastStateKey = entry.key ?? "?";
        }

        bool changed = false;
        // Гистерезис: не меняем стратегию, если новая выигрывает у ТЕКУЩЕЙ еле-еле.
        // Без него путешественник «мечется» на границах бинов.
        // Сравниваем баллы новой и текущей стратегии, а не entry.margin: в неоднозначных
        // состояниях margin < ambiguityThreshold < hysteresis, и путешественник навсегда
        // застревал бы в стартовом WAIT. Первое решение эпизода принимается всегда.
        if (next != CurrentStrategy &&
            (Decisions == 1 || _forceNext || ScoreGain(entry, next) >= balance.hysteresis)) {
            CurrentStrategy = next;
            StrategyChanges++;
            changed = true;
        }
        _forceNext = false;

        Retarget();                                   // путь пересчитываем каждое решение:
                                                      // цели (родники, смотровые точки) могли измениться
        if (label != null)
            label.Show(CurrentStrategy, entry.margin, entry.ambiguous,
                       string.IsNullOrEmpty(OverrideKey) ? LastStateKey : LastStateKey + " MANUAL");
        if (logger != null)
            logger.LogDecision(state, entry, CurrentStrategy, LastStateKey, changed, transform.position);
    }

    /// На сколько балл стратегии next выше балла текущей стратегии в этом состоянии.
    float ScoreGain(PolicyEntry entry, string next) {
        if (entry.scores == null || entry.scores.Length == 0) return float.MaxValue;   // forced / fallback
        return entry.scores[policy.IndexOfStrategy(next)]
             - entry.scores[policy.IndexOfStrategy(CurrentStrategy)];
    }

    void Retarget() {
        if (!TryGetProfile(CurrentStrategy, out var prof) && !TryGetProfile("WAIT", out prof))
            prof = new Profile { strategy = "WAIT", w = new PathWeights(3.0f, 1.5f), speed = 0.35f };
        int target = TargetNodeFor(CurrentStrategy);

        if (CurrentStrategy == "WAIT" && target == graph.NearestNode(transform.position)) {
            motor.Stop();                             // уже в укрытии — пережидаем на месте
            return;
        }

        var path = graph.FindPath(transform.position, target, prof.w,
                                  state.globalWeatherScale, state.ambientWeather);
        motor.SetPath(path, prof.speed);
    }

    int TargetNodeFor(string strategy) {
        Vector2 p = transform.position;
        switch (strategy) {
            case "DIRECT":
            case "SHELTERED":
                return graph.IndexOf("HUT");

            case "SURVEY": {
                int best = -1; float bestD = float.MaxValue;
                foreach (var v in LevelRegistry.Viewpoints) {
                    if (v.Visited) continue;
                    int n = graph.NearestNode(v.transform.position);
                    float d = Vector2.Distance(p, v.transform.position);
                    if (d < bestD) { bestD = d; best = n; }
                }
                return best >= 0 ? best : graph.IndexOf("HUT");
            }

            case "GATHER": {
                bool needFood = state.Energy01 < 0.5f;
                int best = -1; float bestD = float.MaxValue;
                foreach (var sp in LevelRegistry.SupplyPoints) {
                    if (!sp.IsActive) continue;
                    if (sp.kind == SupplyKind.Berries && !needFood) continue;   // ягодник — только если сил мало
                    float d = Vector2.Distance(p, sp.transform.position);
                    if (d < bestD) { bestD = d; best = graph.NearestNode(sp.transform.position); }
                }
                return best >= 0 ? best : graph.IndexOf("HUT");
            }

            case "WAIT": {
                int best = graph.NearestNode(p); float bestShelter = graph.NodeShelter(best);
                for (int i = 0; i < graph.NodeCount; i++) {
                    if (Vector2.Distance(p, graph.PositionOf(i)) > 12f) continue;
                    if (graph.NodeShelter(i) > bestShelter) { bestShelter = graph.NodeShelter(i); best = i; }
                }
                return best;
            }

            case "RETURN": {
                var camp = LevelRegistry.Camp;
                if (camp != null && Vector2.Distance(p, camp.transform.position) < 20f)
                    return graph.NearestNode(camp.transform.position);
                // иначе — ближайший узел «назад» (дальше от приюта), где непогода слабее всего
                int cur = graph.NearestNode(p);
                int best = cur; float bestWeather = float.MaxValue;
                for (int i = 0; i < graph.NodeCount; i++) {
                    if (Vector2.Distance(p, graph.PositionOf(i)) > 20f) continue;
                    if (graph.NodeDist01(i) <= graph.NodeDist01(cur)) continue;
                    float t = graph.NodeWeatherRaw(i);
                    if (t < bestWeather) { bestWeather = t; best = i; }
                }
                return best;
            }
        }
        return graph.IndexOf("HUT");
    }
}

}
