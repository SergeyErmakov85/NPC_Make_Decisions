// AgentState.cs — вычисление пяти переменных состояния.
//
// ЭТО САМЫЙ ОТВЕТСТВЕННЫЙ ФАЙЛ. Имена и диапазоны обязаны совпадать
// с ноутбуком, иначе policy_unity.json будет читаться неверно,
// а поведение получится бессмысленным, но правдоподобным.
using UnityEngine;

namespace CorridorRisk {

public class AgentState : MonoBehaviour {

    [Header("Ссылки")]
    [SerializeField] LevelGraph graph;
    [SerializeField] CorridorRiskBalance balance;

    [Header("Текущие ресурсы")]
    public float hp   = 100f;
    public float ammo = 100f;

    [Header("Настройки эпизода (ставит EpisodeManager)")]
    public float globalThreatScale = 1f;
    public float ambientThreat     = 0f;

    float _threatEma, _coverEma;

    public float Hp01     => Mathf.Clamp01(hp   / balance.hpMax);
    public float Res01    => Mathf.Clamp01(ammo / balance.ammoMax);
    public float Dist01   => Mathf.Clamp01(graph.PathDistanceToGoal(transform.position) / graph.DMax);
    public float Threat01 => _threatEma;
    public float Cover01  => _coverEma;

    /// Множитель скорости от запаса — та же функция, что ammo_factor() в ноутбуке.
    public float AmmoFactor => 0.10f + 0.90f * Mathf.Pow(Res01, 1.4f);

    public float DamageTakenThisStep { get; private set; }

    public void ResetState(float hp01, float res01, float threatScale, float ambient) {
        hp   = hp01  * balance.hpMax;
        ammo = res01 * balance.ammoMax;
        globalThreatScale = threatScale;
        ambientThreat     = ambient;
        // EMA инициализируем мгновенным значением, иначе первые решения эпизода
        // принимаются по пустому состоянию.
        Vector2 p = transform.position;
        _threatEma = RawThreatNow(p);
        _coverEma  = LevelRegistry.Cover(p);
        DamageTakenThisStep = 0f;
    }

    float RawThreatNow(Vector2 p) {
        if (LevelRegistry.Safe != null && LevelRegistry.Safe.Contains(p)) return 0f;
        return Mathf.Clamp01(LevelRegistry.RawThreat(p) * globalThreatScale + ambientThreat);
    }

    void FixedUpdate() {
        Vector2 p = transform.position;
        float threat = RawThreatNow(p);
        float cover  = LevelRegistry.Cover(p);

        // Экспоненциальное сглаживание. Без него агент дёргается на границах зон:
        // одиночный шаг через край зоны перебрасывает threat01 через границу бина.
        float k = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.01f, balance.smoothing));
        _threatEma = Mathf.Lerp(_threatEma, threat, k);
        _coverEma  = Mathf.Lerp(_coverEma,  cover,  k);

        // Урон от угрозы: укрытие снижает его на 70 % при полной плотности.
        float dmg = balance.kDamage * _threatEma * (1f - 0.7f * _coverEma) * Time.fixedDeltaTime;
        if (dmg > 0f) { hp -= dmg; DamageTakenThisStep += dmg; }

        // Регенерация в безопасной зоне.
        if (LevelRegistry.Safe != null && LevelRegistry.Safe.Contains(p))
            hp += LevelRegistry.Safe.hpRegenPerSecond * Time.fixedDeltaTime;

        hp   = Mathf.Clamp(hp,   0f, balance.hpMax);
        ammo = Mathf.Clamp(ammo, 0f, balance.ammoMax);
    }

    public void ConsumeAmmo(float perSecond) { ammo -= perSecond * Time.deltaTime; }
    public void Regen(float hpPerSecond)     { hp   += hpPerSecond * Time.deltaTime; }
    public void ClearStepCounters()          { DamageTakenThisStep = 0f; }

    public void ApplyPickup(Pickup p) {
        if (p.kind == PickupKind.Ammo) ammo += p.amount * balance.ammoMax;
        else                           hp   += p.amount * balance.hpMax;
        hp   = Mathf.Clamp(hp,   0f, balance.hpMax);
        ammo = Mathf.Clamp(ammo, 0f, balance.ammoMax);
    }
}

}
