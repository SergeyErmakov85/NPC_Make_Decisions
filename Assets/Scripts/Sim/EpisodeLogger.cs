// EpisodeLogger.cs — два CSV: по походам (эпизодам) и по решениям.
// Разделитель ';', десятичная точка — файлы потом разбираются тем же ноутбуком.
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CorridorRisk {

public class EpisodeLogger : MonoBehaviour {

    [SerializeField] string runId = "run01";
    [SerializeField] bool   logDecisions = true;

    StreamWriter _ep, _dec;
    int _episode;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string Folder { get; private set; }

    void Awake() {
        Folder = Path.Combine(Application.persistentDataPath, "CorridorRisk", runId);
        Directory.CreateDirectory(Folder);

        _ep = new StreamWriter(Path.Combine(Folder, "episodes.csv"), false, Encoding.UTF8);
        _ep.WriteLine("episode;seed;policySource;spawnNode;" +
                      "targetEnergy;targetDist;targetWeather;targetSupplies;targetShelter;" +
                      "actualEnergy0;actualDist0;actualWeather0;actualSupplies0;actualShelter0;" +
                      "globalWeatherScale;ambientWeather;" +
                      "result;decisions;durationSec;finalEnergy;finalSupplies;" +
                      "suppliesCollected;viewpointsVisited;energyLost;strategyChanges");

        if (logDecisions) {
            _dec = new StreamWriter(Path.Combine(Folder, "decisions.csv"), false, Encoding.UTF8);
            _dec.WriteLine("episode;t;step;energy01;dist01;weather01;supplies01;shelter01;" +
                           "stateKey;strategy;runnerUp;margin;ambiguous;changed;x;y");
        }
        Debug.Log($"[EpisodeLogger] пишу в {Folder}");
    }

    public void SetEpisode(int index) { _episode = index; }

    public void LogDecision(AgentState s, PolicyEntry e, string strategy,
                            string stateKey, bool changed, Vector3 pos) {
        if (_dec == null) return;
        _dec.WriteLine(string.Join(";", new[] {
            _episode.ToString(),
            F(Time.time), "0",
            F(s.Energy01), F(s.Dist01), F(s.Weather01), F(s.Supplies01), F(s.Shelter01),
            stateKey, strategy, e.runnerUp ?? "", F(e.margin),
            e.ambiguous ? "1" : "0", changed ? "1" : "0",
            F(pos.x), F(pos.y)
        }));
    }

    public void LogEpisode(EpisodeRecord r) {
        _ep.WriteLine(string.Join(";", new[] {
            r.episode.ToString(), r.seed.ToString(), r.policySource, r.spawnNode,
            F(r.targetEnergy), F(r.targetDist), F(r.targetWeather), F(r.targetSupplies), F(r.targetShelter),
            F(r.actualEnergy0), F(r.actualDist0), F(r.actualWeather0), F(r.actualSupplies0), F(r.actualShelter0),
            F(r.globalWeatherScale), F(r.ambientWeather),
            r.result, r.decisions.ToString(), F(r.durationSec), F(r.finalEnergy), F(r.finalSupplies),
            r.suppliesCollected.ToString(), r.viewpointsVisited.ToString(),
            F(r.energyLost), r.strategyChanges.ToString()
        }));
        _ep.Flush();
        if (_dec != null) _dec.Flush();
    }

    static string F(float v) => v.ToString("0.####", Inv);

    void OnDestroy() {
        _ep?.Flush();  _ep?.Dispose();
        _dec?.Flush(); _dec?.Dispose();
    }
}

public struct EpisodeRecord {
    public int    episode, seed;
    public string policySource, spawnNode, result;
    public float  targetEnergy, targetDist, targetWeather, targetSupplies, targetShelter;
    public float  actualEnergy0, actualDist0, actualWeather0, actualSupplies0, actualShelter0;
    public float  globalWeatherScale, ambientWeather;
    public int    decisions, suppliesCollected, viewpointsVisited, strategyChanges;
    public float  durationSec, finalEnergy, finalSupplies, energyLost;
}

}
