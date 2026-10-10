// PolicyLiveReload.cs — если во время игры пересохранили policy_unity.json
// (например, ноутбук пересчитал политику с новыми весами критериев),
// таблица перечитывается сразу, без перезапуска игры.
using UnityEditor;
using UnityEngine;

namespace CorridorRisk.EditorTools {

class PolicyLiveReload : AssetPostprocessor {

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) {
        if (!Application.isPlaying) return;
        foreach (var policy in Object.FindObjectsByType<DecisionPolicy>(FindObjectsSortMode.None)) {
            if (policy.Asset == null) continue;
            string path = AssetDatabase.GetAssetPath(policy.Asset);
            if (System.Array.IndexOf(imported, path) < 0) continue;
            policy.Reload();
            Debug.Log($"[PolicyLiveReload] {path} изменён — таблица политики перечитана на лету");
        }
    }
}

}
