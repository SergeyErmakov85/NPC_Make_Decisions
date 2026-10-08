// EpisodeLogger.cs — два CSV: по эпизодам и по решениям.
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
                      "targetHp;targetDist;targetThreat;targetRes;targetCover;" +
                      "actualHp0;actualDist0;actualThreat0;actualRes0;actualCover0;" +
                      "globalThreatScale;ambientThreat;" +
                      "result;decisions;durationSec;finalHp;finalRes;" +
                      "pickupsCollected;postsVisited;damageTaken;strategyChanges");

        if (logDecisions) {
            _dec = new StreamWriter(Path.Combine(Folder, "decisions.csv"), false, Encoding.UTF8);
            _dec.WriteLine("episode;t;step;hp01;dist01;threat01;res01;cover01;" +
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
            F(s.Hp01), F(s.Dist01), F(s.Threat01), F(s.Res01), F(s.Cover01),
            stateKey, strategy, e.runnerUp ?? "", F(e.margin),
            e.ambiguous ? "1" : "0", changed ? "1" : "0",
            F(pos.x), F(pos.y)
        }));
    }

    public void LogEpisode(EpisodeRecord r) {
        _ep.WriteLine(string.Join(";", new[] {
            r.episode.ToString(), r.seed.ToString(), r.policySource, r.spawnNode,
            F(r.targetHp), F(r.targetDist), F(r.targetThreat), F(r.targetRes), F(r.targetCover),
            F(r.actualHp0), F(r.actualDist0), F(r.actualThreat0), F(r.actualRes0), F(r.actualCover0),
            F(r.globalThreatScale), F(r.ambientThreat),
            r.result, r.decisions.ToString(), F(r.durationSec), F(r.finalHp), F(r.finalRes),
            r.pickupsCollected.ToString(), r.postsVisited.ToString(),
            F(r.damageTaken), r.strategyChanges.ToString()
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
    public float  targetHp, targetDist, targetThreat, targetRes, targetCover;
    public float  actualHp0, actualDist0, actualThreat0, actualRes0, actualCover0;
    public float  globalThreatScale, ambientThreat;
    public int    decisions, pickupsCollected, postsVisited, strategyChanges;
    public float  durationSec, finalHp, finalRes, damageTaken;
}

}
