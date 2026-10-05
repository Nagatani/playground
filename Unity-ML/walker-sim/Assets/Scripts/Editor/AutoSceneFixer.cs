#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor.SceneManagement;
using WalkerSim;

namespace WalkerSim.Editor
{
    [InitializeOnLoad]
    public static class AutoSceneFixer
    {
        static AutoSceneFixer()
        {
            EditorApplication.delayCall += () => RunFix(false);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RunFix(false);
            }
        }

        [MenuItem("WalkerSim/Fix Scene Immediately", false, -2)]
        public static void ManualFix()
        {
            RunFix(true);
        }

        [MenuItem("WalkerSim/Rebuild Ground Contact Creature", false, -3)]
        public static void RebuildCreatureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[WalkerSim] Playモード中は再生成できません。Playモードを停止してください。");
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            CreatureBuilder.BuildCompleteEnvironment();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("<color=green>[WalkerSim]</color> 地面に接地する新プロポーションで訓練環境を完全再生成しました！");
        }

        public static void RunFix(bool forceLog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            bool modified = false;

            // 高摩擦マテリアルの確保
            PhysicsMaterial highFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/FootFrictionMaterial.physicMaterial");
            if (highFriction == null)
            {
                highFriction = new PhysicsMaterial("FootFrictionMaterial")
                {
                    dynamicFriction = 1.0f,
                    staticFriction = 1.0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounciness = 0.0f,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(highFriction, "Assets/FootFrictionMaterial.physicMaterial");
                AssetDatabase.SaveAssets();
            }

            // 1. 全ての TerrainBlock のコライダーに高摩擦マテリアルを設定
            var allColliders = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var col in allColliders)
            {
                if (col.gameObject.name.StartsWith("TerrainBlock"))
                {
                    if (col.sharedMaterial != highFriction)
                    {
                        col.sharedMaterial = highFriction;
                        modified = true;
                    }
                }
            }

            // 2. クリーチャーの検出と修復
            var allCreatureBodies = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allCreatureBodies)
            {
                if (go.name == "Creature_Body")
                {
                    // 基底 Agent の重複排除
                    var agents = go.GetComponents<Agent>();
                    foreach (var agent in agents)
                    {
                        if (agent.GetType() == typeof(Agent))
                        {
                            Debug.LogWarning($"<color=yellow>[WalkerSim AutoFix]</color> 不要な純粋基底 Agent を削除: {agent}");
                            Object.DestroyImmediate(agent, true);
                            modified = true;
                        }
                    }

                    // CreatureAgent の取得と修復
                    var creatureAgent = go.GetComponent<CreatureAgent>();
                    if (creatureAgent != null)
                    {
                        var rb = go.GetComponent<Rigidbody>();
                        var so = new SerializedObject(creatureAgent);

                        var bodyRbProp = so.FindProperty("bodyRb");
                        if (bodyRbProp != null && bodyRbProp.objectReferenceValue == null && rb != null)
                        {
                            bodyRbProp.objectReferenceValue = rb;
                            modified = true;
                        }

                        // walkCycleSpeed を 1.2f に調整
                        var walkSpeedProp = so.FindProperty("walkCycleSpeed");
                        if (walkSpeedProp != null)
                        {
                            walkSpeedProp.floatValue = 1.2f;
                        }

                        // 各関節の initialLocalRotation 修復
                        var jointsProp = so.FindProperty("joints");
                        if (jointsProp != null)
                        {
                            for (int i = 0; i < jointsProp.arraySize; i++)
                            {
                                var elem = jointsProp.GetArrayElementAtIndex(i);
                                var rotProp = elem.FindPropertyRelative("initialLocalRotation");
                                var jointProp = elem.FindPropertyRelative("joint");
                                if (jointProp != null && jointProp.objectReferenceValue != null)
                                {
                                    var cj = jointProp.objectReferenceValue as ConfigurableJoint;
                                    if (cj != null)
                                    {
                                        var rot = cj.transform.localRotation;
                                        if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0)
                                        {
                                            rot = Quaternion.identity;
                                        }
                                        rotProp.quaternionValue = rot;
                                        modified = true;
                                    }
                                }
                            }
                        }
                        so.ApplyModifiedProperties();

                        // DecisionRequester
                        var dr = go.GetComponent<DecisionRequester>();
                        if (dr == null)
                        {
                            dr = go.AddComponent<DecisionRequester>();
                            dr.DecisionPeriod = 5;
                            dr.TakeActionsBetweenDecisions = true;
                            modified = true;
                        }

                        // BehaviorParameters
                        var bp = go.GetComponent<BehaviorParameters>();
                        if (bp != null)
                        {
                            if (bp.BrainParameters.VectorObservationSize != 52)
                            {
                                bp.BrainParameters.VectorObservationSize = 52;
                                modified = true;
                            }
                        }
                    }
                }
                else if (go.name.StartsWith("Foot_"))
                {
                    // 足先のコライダーに高摩擦マテリアルを設定
                    var sphereCol = go.GetComponent<SphereCollider>();
                    if (sphereCol != null && sphereCol.sharedMaterial != highFriction)
                    {
                        sphereCol.sharedMaterial = highFriction;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("<color=green>[WalkerSim AutoFix]</color> 高摩擦マテリアルと設定の適用・シーン保存が完了しました！");
            }
            else if (forceLog)
            {
                Debug.Log("<color=green>[WalkerSim AutoFix]</color> シーンは完全に正常な状態です。");
            }
        }
    }
}
#endif
