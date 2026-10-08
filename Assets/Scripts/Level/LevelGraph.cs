// LevelGraph.cs — граф путевых точек: загрузка из JSON, атрибуты узлов,
// расстояние до цели и поиск пути со стратегийно-зависимой стоимостью рёбер.
//
// Это центральный класс сцены. Он отвечает за:
//   * dist01 — нормированную дистанцию до цели (переменную состояния);
//   * поиск пути, из которого САМ СОБОЙ получается выбор маршрута A/B/C.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

/// Коэффициенты стоимости ребра для конкретной стратегии.
public struct PathWeights {
    public float kThreat;
    public float kCover;
    public PathWeights(float kThreat, float kCover) { this.kThreat = kThreat; this.kCover = kCover; }
}

public class LevelGraph : MonoBehaviour {

    [Header("Данные уровня")]
    [SerializeField] TextAsset levelJson;              // level_corridor_risk_2d.json

    [Header("Отладка")]
    [SerializeField] bool drawGizmos = true;

    public LevelFile Data { get; private set; }
    public float DMax => Data != null ? Data.dMax : 65f;

    // --- внутреннее представление графа ---------------------------------------
    int          _n;
    string[]     _ids;
    Vector2[]    _pos;
    float[]      _distToGoal;      // геометрическая длина кратчайшего пути до GOAL
    float[]      _nodeThreatRaw;   // сырая угроза в узле (scale = 1, ambient = 0)
    float[]      _nodeCover;
    List<int>[]  _adj;             // индексы соседей
    List<float>[] _adjLen;         // длины рёбер
    List<float>[] _adjThreatRaw;   // средняя сырая угроза вдоль ребра
    List<float>[] _adjCover;       // среднее укрытие вдоль ребра
    Dictionary<string, int> _index;
    int _goalIndex;

    // буферы Дейкстры, чтобы не выделять память каждый вызов
    float[] _dist; int[] _prev; bool[] _done;

    void Awake() {
        LevelRegistry.EnsureBuilt();
        Build();
    }

    void Build() {
        if (levelJson == null) { Debug.LogError("[LevelGraph] не назначен levelJson"); return; }
        Data = JsonUtility.FromJson<LevelFile>(levelJson.text);

        _n = Data.nodes.Length;
        _ids = new string[_n];
        _pos = new Vector2[_n];
        _index = new Dictionary<string, int>(_n);
        for (int i = 0; i < _n; i++) {
            _ids[i] = Data.nodes[i].id;
            _pos[i] = new Vector2(Data.nodes[i].x, Data.nodes[i].y);
            _index[_ids[i]] = i;
        }
        if (!_index.TryGetValue("GOAL", out _goalIndex))
            Debug.LogError("[LevelGraph] в графе нет узла GOAL");

        _adj = new List<int>[_n]; _adjLen = new List<float>[_n];
        _adjThreatRaw = new List<float>[_n]; _adjCover = new List<float>[_n];
        for (int i = 0; i < _n; i++) {
            _adj[i] = new List<int>(); _adjLen[i] = new List<float>();
            _adjThreatRaw[i] = new List<float>(); _adjCover[i] = new List<float>();
        }

        foreach (var e in Data.edges) {
            int a = _index[e.a], b = _index[e.b];
            float len = Vector2.Distance(_pos[a], _pos[b]);
            SampleEdge(_pos[a], _pos[b], out float th, out float cv);
            AddDir(a, b, len, th, cv);
            AddDir(b, a, len, th, cv);
        }

        // Атрибуты узлов считаем от реальных зон на сцене, а не из JSON:
        // если преподаватель подвинул зону в редакторе, граф это учтёт.
        _nodeThreatRaw = new float[_n];
        _nodeCover = new float[_n];
        for (int i = 0; i < _n; i++) {
            _nodeThreatRaw[i] = LevelRegistry.RawThreat(_pos[i]);
            _nodeCover[i]     = LevelRegistry.Cover(_pos[i]);
        }

        _dist = new float[_n]; _prev = new int[_n]; _done = new bool[_n];
        _distToGoal = DijkstraGeometric(_goalIndex);

        Debug.Log($"[LevelGraph] узлов {_n}, рёбер {Data.edges.Length}, D_MAX = {Data.dMax}");
    }

    void AddDir(int from, int to, float len, float th, float cv) {
        _adj[from].Add(to); _adjLen[from].Add(len);
        _adjThreatRaw[from].Add(th); _adjCover[from].Add(cv);
    }

    /// Среднее по пяти равноотстоящим точкам внутри ребра.
    static void SampleEdge(Vector2 a, Vector2 b, out float threat, out float cover) {
        threat = 0f; cover = 0f;
        const int k = 5;
        for (int i = 1; i <= k; i++) {
            Vector2 p = Vector2.Lerp(a, b, i / (float)(k + 1));
            threat += LevelRegistry.RawThreat(p);
            cover  += LevelRegistry.Cover(p);
        }
        threat /= k; cover /= k;
    }

    // --- публичный API ---------------------------------------------------------

    public int  NodeCount => _n;
    public string IdOf(int i) => _ids[i];
    public Vector2 PositionOf(int i) => _pos[i];
    public float NodeThreatRaw(int i) => _nodeThreatRaw[i];
    public float NodeCover(int i) => _nodeCover[i];
    public float NodeDist01(int i) => Mathf.Clamp01(_distToGoal[i] / DMax);
    public int  IndexOf(string id) => _index.TryGetValue(id, out int i) ? i : -1;

    public int NearestNode(Vector2 p) {
        int best = 0; float bestD = float.MaxValue;
        for (int i = 0; i < _n; i++) {
            float d = (p - _pos[i]).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// Нормированная дистанция до цели: длина по графу от ближайшего узла + подход к нему.
    public float PathDistanceToGoal(Vector2 p) {
        int i = NearestNode(p);
        return _distToGoal[i] + Vector2.Distance(p, _pos[i]);
    }

    /// Путь от позиции до целевого узла с учётом весов стратегии.
    /// Возвращает список мировых точек, первая — ближайший узел, последняя — цель.
    public List<Vector2> FindPath(Vector2 from, int targetNode, PathWeights w,
                                  float threatScale, float ambientThreat) {
        var result = new List<Vector2>();
        if (targetNode < 0) return result;
        int src = NearestNode(from);

        for (int i = 0; i < _n; i++) { _dist[i] = float.MaxValue; _prev[i] = -1; _done[i] = false; }
        _dist[src] = 0f;

        // Граф маленький (25 узлов), поэтому обычная O(n^2) Дейкстра —
        // она проще и на таком размере быстрее кучи.
        for (int it = 0; it < _n; it++) {
            int u = -1; float best = float.MaxValue;
            for (int i = 0; i < _n; i++) if (!_done[i] && _dist[i] < best) { best = _dist[i]; u = i; }
            if (u < 0) break;
            _done[u] = true;
            if (u == targetNode) break;

            for (int k = 0; k < _adj[u].Count; k++) {
                int v = _adj[u][k];
                if (_done[v]) continue;
                float th = Mathf.Clamp01(_adjThreatRaw[u][k] * threatScale + ambientThreat);
                float cv = _adjCover[u][k];
                float cost = _adjLen[u][k] * (1f + w.kThreat * th) / (1f + w.kCover * cv);
                if (_dist[u] + cost < _dist[v]) { _dist[v] = _dist[u] + cost; _prev[v] = u; }
            }
        }

        if (_dist[targetNode] == float.MaxValue) return result;   // недостижимо
        var stack = new List<int>();
        for (int at = targetNode; at != -1; at = _prev[at]) stack.Add(at);
        stack.Reverse();
        foreach (int i in stack) result.Add(_pos[i]);
        return result;
    }

    float[] DijkstraGeometric(int src) {
        var d = new float[_n];
        var done = new bool[_n];
        for (int i = 0; i < _n; i++) d[i] = float.MaxValue;
        d[src] = 0f;
        for (int it = 0; it < _n; it++) {
            int u = -1; float best = float.MaxValue;
            for (int i = 0; i < _n; i++) if (!done[i] && d[i] < best) { best = d[i]; u = i; }
            if (u < 0) break;
            done[u] = true;
            for (int k = 0; k < _adj[u].Count; k++) {
                int v = _adj[u][k];
                if (d[u] + _adjLen[u][k] < d[v]) d[v] = d[u] + _adjLen[u][k];
            }
        }
        return d;
    }

    void OnDrawGizmos() {
        if (!drawGizmos || Data == null || _pos == null) return;
        Gizmos.color = new Color(0.6f, 0.65f, 0.75f, 0.7f);
        foreach (var e in Data.edges) {
            if (!_index.ContainsKey(e.a) || !_index.ContainsKey(e.b)) continue;
            Gizmos.DrawLine(_pos[_index[e.a]], _pos[_index[e.b]]);
        }
        Gizmos.color = Color.black;
        for (int i = 0; i < _n; i++) Gizmos.DrawSphere(_pos[i], 0.35f);
    }
}

}
