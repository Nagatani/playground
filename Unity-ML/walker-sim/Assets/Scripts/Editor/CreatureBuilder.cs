#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using WalkerSim;

namespace WalkerSim.Editor
{
    /// <summary>
    /// Unityエディタのメニューから4足クリーチャーおよび訓練環境を一発自動生成するエディタ拡張
    /// </summary>
    public static class CreatureBuilder
    {
        [MenuItem("WalkerSim/Force Recompile Scripts", false, 0)]
        public static void ForceRecompile()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            EditorUtility.RequestScriptReload();
            Debug.Log("<color=green>[WalkerSim]</color> スクリプトの強制再コンパイルを要求しました。");
        }

        [MenuItem("WalkerSim/Build Complete Training Environment", false, 1)]
        public static void BuildCompleteEnvironment()
        {
            // 既存のオブジェクトがあればクリーンアップ
            GameObject existingEnv = GameObject.Find("TrainingEnvironment");
            if (existingEnv != null)
            {
                Undo.DestroyObjectImmediate(existingEnv);
            }
            GameObject existingBody = GameObject.Find("Creature_Body");
            if (existingBody != null)
            {
                Undo.DestroyObjectImmediate(existingBody);
            }

            // 訓練環境ルート
            GameObject envRoot = new GameObject("TrainingEnvironment");
            envRoot.transform.position = Vector3.zero;

            // 地形ジェネレータ
            GameObject terrainObj = new GameObject("TerrainManager");
            terrainObj.transform.SetParent(envRoot.transform, false);
            TerrainGenerator terrainGen = terrainObj.AddComponent<TerrainGenerator>();
            terrainGen.InitializePool();
            terrainGen.RebuildTerrain(0f);

            // クリーチャー生成
            GameObject creature = BuildCreature(envRoot.transform);

            // クリーチャーに地形マネージャーを紐付け
            CreatureAgent agent = creature.GetComponent<CreatureAgent>();
            var propTerrain = typeof(CreatureAgent).GetField("terrainGenerator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (propTerrain != null) propTerrain.SetValue(agent, terrainGen);

            Selection.activeGameObject = creature;
            Undo.RegisterCreatedObjectUndo(envRoot, "Create Walker Environment");
            Debug.Log("<color=green>[WalkerSim]</color> 4足クリーチャー訓練環境の生成が完了しました！");
        }

        [MenuItem("WalkerSim/Build Creature Only", false, 2)]
        public static GameObject BuildCreatureOnlyMenu()
        {
            GameObject existingBody = GameObject.Find("Creature_Body");
            if (existingBody != null)
            {
                Undo.DestroyObjectImmediate(existingBody);
            }

            GameObject creature = BuildCreature(null);
            Selection.activeGameObject = creature;
            Undo.RegisterCreatedObjectUndo(creature, "Create Creature");
            return creature;
        }

        public static GameObject BuildCreature(Transform parent)
        {
            // 足先用 PhysicMaterial（高摩擦）
            PhysicsMaterial footMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/FootFrictionMaterial.physicMaterial");
            if (footMaterial == null)
            {
                footMaterial = new PhysicsMaterial("FootFrictionMaterial")
                {
                    dynamicFriction = 1.0f,
                    staticFriction = 1.0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounciness = 0.0f,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(footMaterial, "Assets/FootFrictionMaterial.physicMaterial");
                AssetDatabase.SaveAssets();
            }

            // 1. 胴体 (Body)
            // ※ 親の非一様スケールによる物理破綻を防ぐため、BodyのlocalScaleはVector3.oneに保ち、
            //    形状はBoxColliderと子メッシュで設定する
            GameObject body = new GameObject("Creature_Body");
            if (parent != null) body.transform.SetParent(parent, false);
            body.transform.position = new Vector3(0f, 0.58f, 0f);
            body.transform.localScale = Vector3.one;

            // 胴体ビジュアル
            GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyVisual.name = "Body_Visual";
            bodyVisual.transform.SetParent(body.transform, false);
            bodyVisual.transform.localPosition = Vector3.zero;
            bodyVisual.transform.localScale = new Vector3(0.6f, 0.24f, 0.9f);
            Object.DestroyImmediate(bodyVisual.GetComponent<BoxCollider>());

            // 胴体コライダー
            BoxCollider bodyCol = body.AddComponent<BoxCollider>();
            bodyCol.size = new Vector3(0.6f, 0.24f, 0.9f);

            // Rigidbody
            Rigidbody bodyRb = body.AddComponent<Rigidbody>();
            bodyRb.mass = 12.0f;
            bodyRb.linearDamping = 0.5f;
            bodyRb.angularDamping = 0.5f;
            bodyRb.interpolation = RigidbodyInterpolation.Interpolate;

            // 胴体接地判定
            BodyContact bodyContact = body.AddComponent<BodyContact>();

            // ML-Agents コンポーネント
            DecisionRequester dr = body.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = true;

            CreatureAgent agent = body.AddComponent<CreatureAgent>();
            BehaviorParameters bp = body.GetComponent<BehaviorParameters>();
            if (bp == null) bp = body.AddComponent<BehaviorParameters>();
            bp.BehaviorName = "CreatureAgent";
            bp.BrainParameters.VectorObservationSize = 52;
            bp.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeContinuous(8);
            bp.BehaviorType = BehaviorType.HeuristicOnly; // 初期確認用に Heuristic Only に設定

            // リフレクション用フィールドキャッシュ
            var propBodyRb = typeof(CreatureAgent).GetField("bodyRb", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propJoints = typeof(CreatureAgent).GetField("joints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propFeet = typeof(CreatureAgent).GetField("feetContacts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propBodyContact = typeof(CreatureAgent).GetField("bodyContact", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propGroundLayer = typeof(CreatureAgent).GetField("groundLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (propBodyRb != null) propBodyRb.SetValue(agent, bodyRb);
            if (propBodyContact != null) propBodyContact.SetValue(agent, bodyContact);
            if (propGroundLayer != null) propGroundLayer.SetValue(agent, (LayerMask)LayerMask.GetMask("Ground"));

            List<LegJoint> jointList = new List<LegJoint>();
            List<GroundContact> footList = new List<GroundContact>();

            // 脚の配置オフセット (FL, FR, BL, BR)
            (string name, Vector3 hipOffset)[] legConfigs = new (string, Vector3)[]
            {
                ("FL", new Vector3(-0.32f, 0f, 0.35f)),
                ("FR", new Vector3(0.32f, 0f, 0.35f)),
                ("BL", new Vector3(-0.32f, 0f, -0.35f)),
                ("BR", new Vector3(0.32f, 0f, -0.35f))
            };

            foreach (var leg in legConfigs)
            {
                // UpperLeg (大腿)
                // 長さ 0.25m, 直径 0.12m
                GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                upper.name = $"UpperLeg_{leg.name}";
                upper.transform.SetParent(body.transform, false);
                upper.transform.localPosition = leg.hipOffset + new Vector3(0f, -0.125f, 0f);
                upper.transform.localScale = new Vector3(0.12f, 0.125f, 0.12f); // Capsule base height is 2, so 0.125 * 2 = 0.25m

                Rigidbody upperRb = upper.GetComponent<Rigidbody>();
                if (upperRb == null) upperRb = upper.AddComponent<Rigidbody>();
                upperRb.mass = 2.0f;
                upperRb.linearDamping = 0.2f;
                upperRb.angularDamping = 0.2f;
                upperRb.interpolation = RigidbodyInterpolation.Interpolate;

                // 股関節: カプセルの上端 (local y = +1.0) をアンカーとしてBodyに接続
                ConfigurableJoint upperJoint = SetupJoint(upper, bodyRb, Vector3.up * 1.0f);

                jointList.Add(new LegJoint
                {
                    jointName = upper.name,
                    joint = upperJoint,
                    jointRb = upperRb,
                    motionAxis = Vector3.right,
                    minAngle = -45f,
                    maxAngle = 45f,
                    spring = 4000f,
                    damper = 200f,
                    maxForce = 10000f,
                    initialLocalRotation = Quaternion.identity
                });

                // LowerLeg (下腿)
                // 長さ 0.22m, 直径 0.10m
                GameObject lower = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                lower.name = $"LowerLeg_{leg.name}";
                lower.transform.SetParent(body.transform, false);
                lower.transform.localPosition = leg.hipOffset + new Vector3(0f, -0.36f, 0f);
                lower.transform.localScale = new Vector3(0.10f, 0.11f, 0.10f); // 0.11 * 2 = 0.22m

                Rigidbody lowerRb = lower.GetComponent<Rigidbody>();
                if (lowerRb == null) lowerRb = lower.AddComponent<Rigidbody>();
                lowerRb.mass = 1.5f;
                lowerRb.linearDamping = 0.2f;
                lowerRb.angularDamping = 0.2f;
                lowerRb.interpolation = RigidbodyInterpolation.Interpolate;

                // 膝関節: カプセルの上端 (local y = +1.0) をアンカーとしてUpperLegの下端に接続
                ConfigurableJoint lowerJoint = SetupJoint(lower, upperRb, Vector3.up * 1.0f);

                jointList.Add(new LegJoint
                {
                    jointName = lower.name,
                    joint = lowerJoint,
                    jointRb = lowerRb,
                    motionAxis = Vector3.right,
                    minAngle = -60f,
                    maxAngle = 20f,
                    spring = 4000f,
                    damper = 200f,
                    maxForce = 10000f,
                    initialLocalRotation = Quaternion.identity
                });

                // Foot (足先)
                // 下腿の下端 (local y = -1.0) に球を配置
                GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foot.name = $"Foot_{leg.name}";
                foot.transform.SetParent(lower.transform, false);
                foot.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                foot.transform.localScale = new Vector3(1.3f, 0.6f, 1.3f);

                Object.DestroyImmediate(foot.GetComponent<Rigidbody>());

                var footCollider = foot.GetComponent<SphereCollider>();
                if (footCollider != null) footCollider.sharedMaterial = footMaterial;

                GroundContact footContact = foot.AddComponent<GroundContact>();
                footList.Add(footContact);
            }

            if (propJoints != null) propJoints.SetValue(agent, jointList);
            if (propFeet != null) propFeet.SetValue(agent, footList);

            return body;
        }

        private static ConfigurableJoint SetupJoint(GameObject targetObj, Rigidbody connectedBody, Vector3 anchorPos)
        {
            ConfigurableJoint joint = targetObj.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.anchor = anchorPos;
            joint.autoConfigureConnectedAnchor = true;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            SoftJointLimit lowX = new SoftJointLimit { limit = -65f };
            SoftJointLimit highX = new SoftJointLimit { limit = 65f };
            SoftJointLimit yLimit = new SoftJointLimit { limit = 20f };
            SoftJointLimit zLimit = new SoftJointLimit { limit = 20f };

            joint.lowAngularXLimit = lowX;
            joint.highAngularXLimit = highX;
            joint.angularYLimit = yLimit;
            joint.angularZLimit = zLimit;

            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = 4000f,
                positionDamper = 200f,
                maximumForce = 10000f
            };

            joint.enableCollision = false;
            return joint;
        }
    }
}
#endif
