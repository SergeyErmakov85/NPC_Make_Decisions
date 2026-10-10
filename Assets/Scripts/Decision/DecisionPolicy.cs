// DecisionPolicy.cs — загрузка таблицы политики и запрос «состояние → стратегия».
//
// Весь «здравый смысл» путешественника (NPC) сосредоточен здесь, и он целиком объясним:
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
    public TextAsset Asset      => policyAsset;

    /// Таблицу перечитали посреди игры (новый JSON или другой policyAsset) —
    /// путешественник должен сразу решить заново.
    public event System.Action Reloaded;

    void Awake() { Load(); }

    // Правка в инспекторе во время игры: подменили policyAsset или настройки мягкого выбора.
    void OnValidate() { if (Application.isPlaying && _file != null) _reloadPending = true; }
    bool _reloadPending;

    void Update() {
        if (!_reloadPending) return;
        _reloadPending = false;
        Reload();
    }

    /// Перечитать таблицу и сообщить об этом (вызывается и из редактора при реимпорте JSON).
    public void Reload() {
        Load();
        Reloaded?.Invoke();
    }

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

    public string StateKey(float energy, float dist, float weather, float supplies, float shelter) {
        float[] v = { energy, dist, weather, supplies, shelter };
        var parts = new string[_file.stateVars.Length];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = Bin(_file.stateVars[i], Mathf.Clamp01(v[i])).ToString();
        return string.Join("|", parts);
    }

    public string[] StateVars => _file.stateVars;

    /// Сколько бинов у переменной (границ + 1).
    public int BinCount(string varName) => _edges.TryGetValue(varName, out var e) ? e.Length + 1 : 1;

    /// Середина бина — «типичное» значение переменной для заданной цифры ключа.
    public float BinCenter(string varName, int bin) {
        if (!_edges.TryGetValue(varName, out var e)) return 0.5f;
        bin = Mathf.Clamp(bin, 0, e.Length);
        float lo = bin == 0        ? 0f : e[bin - 1];
        float hi = bin == e.Length ? 1f : e[bin];
        return 0.5f * (lo + hi);
    }

    public bool TryGetEntry(string key, out PolicyEntry entry) => _index.TryGetValue(key, out entry);

    /// Главный метод: состояние → код стратегии.
    public string Decide(float energy, float dist, float weather, float supplies, float shelter,
                         out PolicyEntry entry) {
        return DecideByKey(StateKey(energy, dist, weather, supplies, shelter), out entry);
    }

    /// То же, но по готовому ключу ситуации (например, заданному вручную в панели).
    public string DecideByKey(string key, out PolicyEntry entry) {
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
