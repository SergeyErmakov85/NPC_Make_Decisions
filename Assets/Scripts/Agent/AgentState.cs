// AgentState.cs — вычисление пяти переменных состояния путешественника.
//
// ЭТО САМЫЙ ОТВЕТСТВЕННЫЙ ФАЙЛ. Имена и диапазоны обязаны совпадать
// с ноутбуком (energy, dist, weather, supplies, shelter), иначе policy_unity.json
// будет читаться неверно, а поведение получится бессмысленным, но правдоподобным.
using UnityEngine;

namespace CorridorRisk {

public class AgentState : MonoBehaviour {

    [Header("Ссылки")]
    [SerializeField] LevelGraph graph;
    [SerializeField] CorridorRiskBalance balance;

    [Header("Силы и запасы путешественника")]
    public float energy   = 100f;   // силы
    public float supplies = 100f;   // запас воды и еды

    [Header("Настройки похода (ставит EpisodeManager)")]
    public float globalWeatherScale = 1f;   // во сколько раз усилить непогоду на участках
    public float ambientWeather     = 0f;   // «фоновая» непогода по всей карте

    float _weatherEma, _shelterEma;

    // Пять переменных состояния в [0, 1] — в том же порядке, что в ноутбуке.
    public float Energy01   => Mathf.Clamp01(energy   / balance.energyMax);
    public float Dist01     => Mathf.Clamp01(graph.PathDistanceToHut(transform.position) / graph.DMax);
    public float Weather01  => _weatherEma;
    public float Supplies01 => Mathf.Clamp01(supplies / balance.suppliesMax);
    public float Shelter01  => _shelterEma;

    /// Множитель скорости от запасов — та же функция, что supplies_factor() в ноутбуке:
    /// голодный и мучимый жаждой путник идёт медленно.
    public float SuppliesFactor => 0.10f + 0.90f * Mathf.Pow(Supplies01, 1.4f);

    /// Сколько сил потеряно с прошлого решения (нужно для награды на уровне B).
    public float EnergyLostThisStep { get; private set; }

    public void ResetState(float energy01, float supplies01, float weatherScale, float ambient) {
        energy   = energy01   * balance.energyMax;
        supplies = supplies01 * balance.suppliesMax;
        globalWeatherScale = weatherScale;
        ambientWeather     = ambient;
        // Сглаженные значения начинаем с мгновенных, иначе первые решения похода
        // принимаются по пустому состоянию.
        SnapSmoothing();
        EnergyLostThisStep = 0f;
    }

    /// Поменять непогоду посреди похода (ползунок Weather в Demo).
    public void SetWeather(float weatherScale, float ambient) {
        globalWeatherScale = weatherScale;
        ambientWeather     = ambient;
    }

    /// Сбросить сглаживание: weather01 и shelter01 сразу равны значениям в текущей точке.
    /// Нужно, когда ситуацию меняют вручную, — иначе ключ догонял бы её секунду-другую.
    public void SnapSmoothing() {
        Vector2 p = transform.position;
        _weatherEma = RawWeatherNow(p);
        _shelterEma = LevelRegistry.Shelter(p);
    }

    float RawWeatherNow(Vector2 p) {
        if (LevelRegistry.Camp != null && LevelRegistry.Camp.Contains(p)) return 0f;   // у костра сухо и тихо
        // на солнечной поляне стихает и фоновая непогода, а не только участки
        float ambient = ambientWeather * (1f - LevelRegistry.SunCalm(p));
        return Mathf.Clamp01(LevelRegistry.RawWeather(p) * globalWeatherScale + ambient);
    }

    void FixedUpdate() {
        Vector2 p = transform.position;
        float weather = RawWeatherNow(p);
        float shelter = LevelRegistry.Shelter(p);

        // Экспоненциальное сглаживание. Без него путешественник «дёргается» на границах участков:
        // один шаг через край участка перебрасывает weather01 через границу бина.
        float k = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.01f, balance.smoothing));
        _weatherEma = Mathf.Lerp(_weatherEma, weather, k);
        _shelterEma = Mathf.Lerp(_shelterEma, shelter, k);

        // Непогода отнимает силы: ветер и дождь выматывают. Укрытия (лес, навесы)
        // снижают потерю сил до 70 % при максимальной доступности.
        float loss = balance.kFatigue * _weatherEma * (1f - 0.7f * _shelterEma) * Time.fixedDeltaTime;
        if (loss > 0f) { energy -= loss; EnergyLostThisStep += loss; }

        // Отдых у костра на стоянке восстанавливает силы.
        if (LevelRegistry.Camp != null && LevelRegistry.Camp.Contains(p))
            energy += LevelRegistry.Camp.energyRegenPerSecond * Time.fixedDeltaTime;

        // На солнечной поляне путешественник отогревается и восстанавливает силы.
        energy += LevelRegistry.SunRegen(p) * Time.fixedDeltaTime;

        energy   = Mathf.Clamp(energy,   0f, balance.energyMax);
        supplies = Mathf.Clamp(supplies, 0f, balance.suppliesMax);
    }

    public void ConsumeSupplies(float perSecond) { supplies -= perSecond * Time.deltaTime; }
    public void Regen(float energyPerSecond)     { energy   += energyPerSecond * Time.deltaTime; }
    public void ClearStepCounters()              { EnergyLostThisStep = 0f; }

    /// Родник пополняет запас воды и еды, ягодник восстанавливает силы.
    public void ApplySupply(SupplyPoint p) {
        if (p.kind == SupplyKind.Spring) supplies += p.amount * balance.suppliesMax;
        else                             energy   += p.amount * balance.energyMax;
        energy   = Mathf.Clamp(energy,   0f, balance.energyMax);
        supplies = Mathf.Clamp(supplies, 0f, balance.suppliesMax);
    }
}

}
