// PolicyModel.cs — контейнеры для разбора policy_unity.json,
// который порождает ноутбук MCDA_Agent_Policy_Unity_v1.ipynb.
//
// Файл специально сделан «плоским» (только массивы), чтобы читался
// штатным UnityEngine.JsonUtility без сторонних JSON-библиотек.
using System;

namespace CorridorRisk {

[Serializable]
public class PolicyEntry {
    public string  key;          // "hp|dist|threat|res|cover", например "0|2|2|1|1"
    public string  strategy;
    public string  runnerUp;
    public float   margin;
    public bool    ambiguous;
    public float[] scores;       // порядок соответствует strategies[]
}

[Serializable]
public class EdgeSpec {
    public string  varName;      // имя переменной состояния
    public float[] values;       // границы между бинами
}

[Serializable]
public class CriterionSpec {
    public string name;
    public string direction;
    public float  baseWeight;
}

[Serializable]
public class PolicyFile {
    public string          schemaVersion;
    public string          generatedAt;
    public string          method;
    public string[]        strategies;
    public string[]        stateVars;
    public string          fallback;
    public float           ambiguityThreshold;
    public CriterionSpec[] criteria;
    public EdgeSpec[]      edges;
    public PolicyEntry[]   entries;
}

}
