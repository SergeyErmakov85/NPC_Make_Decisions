// DecisionPolicy.cs — загрузка таблицы политики и запрос «состояние → стратегия».
//
// Весь «интеллект» NPC сосредоточен здесь, и он целиком объясним:
// это таблица на 243 строки, посчитанная методом MCDA в ноутбуке.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

public class DecisionPolicy : MonoBehaviour {

    [Header("Данные политики")]
    [SerializeField] TextAsset policyAsset;                 // policy_unity.json

    [Header("Поведение в неоднозначных состояниях")]
    [SerializeField] bool  stochasticWhenAmbiguous = true;
    [SerializeField] float softmaxTemperature      = 0.04f;

    PolicyFile _file;
    readonly Dictionary<string, PolicyEntry> _index = new Dictionary<string, PolicyEntry>();
    readonly Dictionary<string, float[]>     _edges = new Dictionary<string, float[]>();

    public string[] Strategies => _file.strategies;
    public string   Method     => _file.method;
    public int      EntryCount => _index.Count;

    void Awake() { Load(); }

    public void Load() {
        if (policyAsset == null) { Debug.LogError("[DecisionPolicy] не назначен policyAsset"); return; }
        _file = JsonUtility.FromJson<PolicyFile>(policyAsset.text);
        _index.Clear(); _edges.Clear();
        foreach (var e in _file.entries) _index[e.key]     = e;
        foreach (var e in _file.edges)   _edges[e.varName] = e.values;
        Debug.Log($"[DecisionPolicy] загружено состояний: {_index.Count}, метод: {_file.method}, " +
                  $"сгенерировано: {_file.generatedAt}");
    }

    /// Дискретизация непрерывного значения [0,1] в индекс бина.
    /// Границы берутся из того же JSON — в коде не должно быть «зашитых» чисел.
    public int Bin(string varName, float value) {
        if (!_edges.TryGetValue(varName, out var e)) return 0;
        int i = 0;
        while (i < e.Length && value >= e[i]) i++;
        return i;
    }

    public string StateKey(float hp, float dist, float threat, float res, float cover) {
        float[] v = { hp, dist, threat, res, cover };
        var parts = new string[_file.stateVars.Length];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = Bin(_file.stateVars[i], Mathf.Clamp01(v[i])).ToString();
        return string.Join("|", parts);
    }

    /// Главный метод: состояние → код стратегии.
    public string Decide(float hp, float dist, float threat, float res, float cover,
                         out PolicyEntry entry) {
        string key = StateKey(hp, dist, threat, res, cover);
        if (!_index.TryGetValue(key, out entry)) {
            Debug.LogWarning($"[DecisionPolicy] нет записи {key} → fallback {_file.fallback}");
            return _file.fallback;
        }
        return (stochasticWhenAmbiguous && entry.ambiguous)
             ? SoftmaxSample(entry.scores)
             : entry.strategy;
    }

    /// Мягкий выбор: чем выше балл MCDA, тем выше вероятность.
    /// Нужен там, где разрыв между первым и вторым местом мал: жёсткий argmax
    /// в таких состояниях делает поведение неестественно детерминированным.
    string SoftmaxSample(float[] scores) {
        float max = float.NegativeInfinity;
        for (int i = 0; i < scores.Length; i++) max = Mathf.Max(max, scores[i]);
        var p = new float[scores.Length];
        float sum = 0f;
        for (int i = 0; i < scores.Length; i++) {
            p[i] = Mathf.Exp((scores[i] - max) / Mathf.Max(1e-4f, softmaxTemperature));
            sum += p[i];
        }
        float r = UnityEngine.Random.value * sum, acc = 0f;
        for (int i = 0; i < p.Length; i++) { acc += p[i]; if (r <= acc) return _file.strategies[i]; }
        return _file.strategies[0];
    }

    public int IndexOfStrategy(string s) {
        for (int i = 0; i < _file.strategies.Length; i++) if (_file.strategies[i] == s) return i;
        return 0;
    }
}

}
