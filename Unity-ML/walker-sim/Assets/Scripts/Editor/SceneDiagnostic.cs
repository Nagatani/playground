#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;

namespace WalkerSim.Editor
{
    [InitializeOnLoad]
    public static class SceneDiagnostic
    {
        [MenuItem("WalkerSim/Run Diagnostic", false, -1)]
        public static void RunDiagnostic()
        {
            Debug.Log("========== [WalkerSim Diagnostic START] ==========");

            var allAgents = Object.FindObjectsByType<Agent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"Total Agent count in scene: {allAgents.Length}");

            foreach (var agent in allAgents)
            {
                Debug.Log($"Agent found on GameObject: '{agent.gameObject.name}', Type: {agent.GetType().FullName}, Enabled: {agent.enabled}, Active: {agent.gameObject.activeInHierarchy}");

                var components = agent.GetComponents<Component>();
                foreach (var c in components)
                {
                    Debug.Log($"  - Component: {c.GetType().FullName} (ID: {c.GetInstanceID()})");
                }
            }

            Debug.Log("========== [WalkerSim Diagnostic END] ==========");
        }
    }
}
#endif
