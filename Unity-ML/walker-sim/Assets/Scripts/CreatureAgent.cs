using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace WalkerSim
{

    /// <summary>
    /// 4足歩行クリーチャーの学習エージェント
    /// </summary>
    [RequireComponent(typeof(DecisionRequester))]
    [RequireComponent(typeof(Rigidbody))]
    public class CreatureAgent : Agent, IHeuristicProvider
    {
        [Header("Body & Transform")]
        [Tooltip("クリーチャーの胴体Rigidbody")]
        [SerializeField] private Rigidbody bodyRb;

        [Tooltip("胴体接触検知スクリプト")]
        [SerializeField] private BodyContact bodyContact;

        [Tooltip("胴体の初期スポーン高さ（オフセット）")]
        [SerializeField] private float spawnHeightOffset = 0.05f;

        [Header("Leg Joints (8 Joints)")]
        [Tooltip("各脚の2関節（上腿・下腿）× 4本 = 合計8関節")]
        [SerializeField] private List<LegJoint> joints = new List<LegJoint>();

        [Header("Ground Contacts (4 Feet)")]
        [Tooltip("4つの足先の接地判定スクリプト [FL, FR, BL, BR]")]
        [SerializeField] private List<GroundContact> feetContacts = new List<GroundContact>();

        [Header("Terrain & Environment")]
        [Tooltip("訓練エリアの地形ジェネレータ")]
        [SerializeField] private TerrainGenerator terrainGenerator;

        [Tooltip("地面検知用レイヤー")]
        [SerializeField] private LayerMask groundLayer;

        [Header("Reward Parameters")]
        [Tooltip("前進速度に対する報酬重み")]
        [SerializeField] private float forwardSpeedRewardWeight = 1.5f;

        [Tooltip("目標前進速度 (m/s)")]
        [SerializeField] private float targetSpeed = 1.5f;

        [Tooltip("姿勢維持（胴体が上を向いている）に対する報酬重み")]
        [SerializeField] private float uprightRewardWeight = 0.5f;

        [Tooltip("アクション（エネルギー消費）ペナルティ係数")]
        [SerializeField] private float energyPenaltyWeight = 0.001f;

        [Tooltip("横揺れ（ドリフト）抑制ペナルティ係数")]
        [SerializeField] private float sidewaysPenaltyWeight = 0.1f;

        [Tooltip("転倒と判定する胴体傾斜角の閾値（度）")]
        [SerializeField] private float fallAngleThreshold = 45f;

        [Header("Heuristic Testing")]
        [Tooltip("Heuristic時にキー入力を待たずに自動で歩行モーションを実行するか")]
        [SerializeField] private bool autoWalkInHeuristic = true;

        [Tooltip("歩行サイクルの周波数（歩行スピード）")]
        [SerializeField] private float walkCycleSpeed = 1.5f;

        private Vector3 initialSpawnPosition;
        private Quaternion initialSpawnRotation;
        private EnvironmentParameters envParameters;
        private DecisionRequester decisionRequester;
        private float episodeStartTime;

        protected override void Awake()
        {
            base.Awake();

            if (bodyRb == null)
            {
                bodyRb = GetComponent<Rigidbody>();
            }

            decisionRequester = GetComponent<DecisionRequester>();
            if (decisionRequester == null)
            {
                decisionRequester = gameObject.AddComponent<DecisionRequester>();
                decisionRequester.DecisionPeriod = 5;
                decisionRequester.TakeActionsBetweenDecisions = true;
            }

            initialSpawnPosition = transform.position;
            initialSpawnRotation = transform.rotation;
        }

        public override void Initialize()
        {
            if (bodyRb == null)
            {
                bodyRb = GetComponent<Rigidbody>();
            }

            if (decisionRequester == null)
            {
                decisionRequester = GetComponent<DecisionRequester>();
            }

            initialSpawnPosition = transform.position;
            if (initialSpawnPosition.y > 0.75f)
            {
                initialSpawnPosition.y = 0.58f;
            }
            initialSpawnRotation = transform.rotation;

            foreach (var j in joints)
            {
                if (j != null) j.Initialize();
            }

            envParameters = Academy.Instance.EnvironmentParameters;
        }

        public override void OnEpisodeBegin()
        {
            episodeStartTime = Time.time;

            float currentRoughness = 0.0f;
            if (envParameters != null)
            {
                currentRoughness = envParameters.GetWithDefault("roughness", 0.0f);
            }
            if (terrainGenerator != null)
            {
                terrainGenerator.RebuildTerrain(currentRoughness);
            }

            transform.position = initialSpawnPosition + Vector3.up * spawnHeightOffset;
            transform.rotation = initialSpawnRotation;

            if (bodyRb != null)
            {
                bodyRb.linearVelocity = Vector3.zero;
                bodyRb.angularVelocity = Vector3.zero;
            }

            if (bodyContact != null)
            {
                bodyContact.ResetContact();
            }

            foreach (var j in joints)
            {
                if (j != null) j.ResetJoint();
            }

            foreach (var foot in feetContacts)
            {
                if (foot != null) foot.ResetContact();
            }
        }

        /// <summary>
        /// 観測値の収集 (合計 52次元)
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            if (bodyRb == null)
            {
                bodyRb = GetComponent<Rigidbody>();
            }

            Transform bodyTransform = bodyRb != null ? bodyRb.transform : transform;

            // 1. 胴体の傾き・向き (6次元)
            sensor.AddObservation(bodyTransform.up);
            sensor.AddObservation(bodyTransform.forward);

            // 2. 胴体の速度および角速度（ローカル座標系） (6次元)
            if (bodyRb != null)
            {
                Vector3 localVel = bodyTransform.InverseTransformDirection(bodyRb.linearVelocity);
                Vector3 localAngVel = bodyTransform.InverseTransformDirection(bodyRb.angularVelocity);
                sensor.AddObservation(localVel);
                sensor.AddObservation(localAngVel);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Vector3.zero);
            }

            // 3. 各関節（8個）の現在角度と回転速度 (16次元)
            for (int i = 0; i < 8; i++)
            {
                if (i < joints.Count && joints[i] != null && joints[i].jointRb != null)
                {
                    float normAngle = Mathf.Clamp(joints[i].GetCurrentAngle() / 60f, -1f, 1f);
                    sensor.AddObservation(normAngle);

                    Vector3 relAngVel = bodyTransform.InverseTransformDirection(joints[i].jointRb.angularVelocity);
                    sensor.AddObservation(Mathf.Clamp(relAngVel.x * 0.1f, -1f, 1f));
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }

            // 4. 4つの足先の接地フラグ (4次元)
            for (int i = 0; i < 4; i++)
            {
                if (i < feetContacts.Count && feetContacts[i] != null)
                {
                    sensor.AddObservation(feetContacts[i].IsGrounded ? 1.0f : 0.0f);
                }
                else
                {
                    sensor.AddObservation(0.0f);
                }
            }

            // 5. 胴体から足元・前方の地形を検知する Raycast (5本 × 4値 = 20次元)
            CollectTerrainRaycasts(sensor, bodyTransform);
        }

        private void CollectTerrainRaycasts(VectorSensor sensor, Transform bodyTransform)
        {
            float maxRayDist = 3.5f;

            Vector3[] rayDirs = new Vector3[]
            {
                Vector3.down,
                (Vector3.forward * 0.8f + Vector3.down).normalized,
                (Vector3.forward * 1.5f + Vector3.down).normalized,
                (Vector3.forward * 0.7f + Vector3.left * 0.5f + Vector3.down).normalized,
                (Vector3.forward * 0.7f + Vector3.right * 0.5f + Vector3.down).normalized
            };

            Vector3 rayOrigin = bodyTransform.position;

            foreach (var localDir in rayDirs)
            {
                Vector3 worldDir = bodyTransform.TransformDirection(localDir);

                if (Physics.Raycast(rayOrigin, worldDir, out RaycastHit hit, maxRayDist, groundLayer))
                {
                    float normalizedDist = hit.distance / maxRayDist;
                    sensor.AddObservation(normalizedDist);
                    sensor.AddObservation(hit.normal);
                }
                else
                {
                    sensor.AddObservation(1.0f);
                    sensor.AddObservation(Vector3.up);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuousActions = actions.ContinuousActions;

            // 1. 各関節への目標角度の適用
            float actionSquaredSum = 0f;
            for (int i = 0; i < joints.Count; i++)
            {
                if (i < continuousActions.Length && joints[i] != null)
                {
                    float actionVal = Mathf.Clamp(continuousActions[i], -1f, 1f);
                    joints[i].SetTargetAngle(actionVal);
                    actionSquaredSum += actionVal * actionVal;
                }
            }

            // 2. 転倒判定
            Transform bodyTransform = bodyRb != null ? bodyRb.transform : transform;
            float uprightDot = Vector3.Dot(bodyTransform.up, Vector3.up);
            float minUprightDot = Mathf.Cos(fallAngleThreshold * Mathf.Deg2Rad); // 45度 ≈ 0.707

            bool isGracePeriod = (Time.time - episodeStartTime) < 0.6f;
            bool isFallen = !isGracePeriod && ((bodyContact != null && bodyContact.HasTouchedGround) || (uprightDot < minUprightDot));

            if (isFallen)
            {
                SetReward(-1.0f);
                EndEpisode();
                return;
            }

            // 3. 報酬計算
            if (bodyRb != null)
            {
                Vector3 forward = transform.forward;
                float forwardVelocity = Vector3.Dot(bodyRb.linearVelocity, forward);
                float forwardReward = Mathf.Clamp(forwardVelocity / targetSpeed, -0.5f, 1.2f) * forwardSpeedRewardWeight;

                float uprightReward = Mathf.Max(0f, uprightDot) * uprightRewardWeight;

                Vector3 right = transform.right;
                float sidewaysVelocity = Mathf.Abs(Vector3.Dot(bodyRb.linearVelocity, right));
                float sidewaysPenalty = sidewaysVelocity * sidewaysPenaltyWeight;

                float energyPenalty = actionSquaredSum * energyPenaltyWeight;

                float stepReward = (forwardReward + uprightReward) - (sidewaysPenalty + energyPenalty);
                AddReward(stepReward);
            }
        }

        /// <summary>
        /// IHeuristicProvider のインターフェース実装（直接ディスパッチ保証）
        /// </summary>
        void IHeuristicProvider.Heuristic(in ActionBuffers actionBuffersOut)
        {
            Heuristic(actionBuffersOut);
        }

        /// <summary>
        /// Heuristic（手動/自動デバッグ）歩行ロジック
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            bool inputActive = false;
            try
            {
                inputActive = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            }
            catch { }

            bool shouldWalk = autoWalkInHeuristic || inputActive;

            if (!shouldWalk)
            {
                for (int i = 0; i < continuousActions.Length; i++)
                {
                    continuousActions[i] = 0f;
                }
                return;
            }

            // 対角歩行 (Trot Gait) の生成
            float t = Time.time * walkCycleSpeed * Mathf.PI * 2.0f;

            float[] legPhase = new float[]
            {
                0f,          // FL
                Mathf.PI,    // FR
                Mathf.PI,    // BL
                0f           // BR
            };

            for (int legIndex = 0; legIndex < 4; legIndex++)
            {
                int upperIdx = legIndex * 2;
                int lowerIdx = legIndex * 2 + 1;

                float phase = legPhase[legIndex];

                // swing: 正で後方キック(推進)、負で前方スイング(足送り)
                float swing = Mathf.Sin(t + phase);

                // knee: 
                // 前方スイング中(空中)は膝をしっかり曲げて(knee < 0)足を高く持ち上げ地面を擦らないようにする
                // 後方キック中(接地)は膝を伸ばして(knee > 0)地面を力強く押し出し前進力を生む
                float knee = Mathf.Cos(t + phase) * 0.85f - 0.05f;

                if (upperIdx < continuousActions.Length)
                {
                    continuousActions[upperIdx] = Mathf.Clamp(swing, -1f, 1f);
                }
                if (lowerIdx < continuousActions.Length)
                {
                    continuousActions[lowerIdx] = Mathf.Clamp(knee, -1f, 1f);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bodyRb == null) return;
            Gizmos.color = Color.green;
            Transform t = bodyRb.transform;

            Vector3[] rayDirs = new Vector3[]
            {
                Vector3.down,
                (Vector3.forward * 0.8f + Vector3.down).normalized,
                (Vector3.forward * 1.5f + Vector3.down).normalized,
                (Vector3.forward * 0.7f + Vector3.left * 0.5f + Vector3.down).normalized,
                (Vector3.forward * 0.7f + Vector3.right * 0.5f + Vector3.down).normalized
            };

            foreach (var localDir in rayDirs)
            {
                Vector3 worldDir = t.TransformDirection(localDir);
                Gizmos.DrawRay(t.position, worldDir * 3.5f);
            }
        }
    }

    /// <summary>
    /// 各関節の制御パラメータと初期姿勢を管理するデータ構造
    /// </summary>
    [System.Serializable]
    public class LegJoint
    {
        public string jointName;
        public ConfigurableJoint joint;
        public Rigidbody jointRb;

        [Header("Motion Limits")]
        [Tooltip("可動軸 (ローカル座標系)")]
        public Vector3 motionAxis = Vector3.right;
        [Tooltip("最小目標角度 (度)")]
        public float minAngle = -50f;
        [Tooltip("最大目標角度 (度)")]
        public float maxAngle = 50f;

        [Tooltip("駆動スプリング力")]
        public float spring = 4000f;
        [Tooltip("駆動ダンパー力")]
        public float damper = 200f;
        [Tooltip("最大トルク")]
        public float maxForce = 10000f;

        [HideInInspector] public Quaternion initialLocalRotation;

        public void Initialize()
        {
            if (joint != null)
            {
                initialLocalRotation = joint.transform.localRotation;
                if (initialLocalRotation.w == 0 && initialLocalRotation.x == 0 && initialLocalRotation.y == 0 && initialLocalRotation.z == 0)
                {
                    initialLocalRotation = Quaternion.identity;
                }

                joint.rotationDriveMode = RotationDriveMode.Slerp;

                JointDrive drive = new JointDrive
                {
                    positionSpring = spring,
                    positionDamper = damper,
                    maximumForce = maxForce
                };
                joint.slerpDrive = drive;
            }
        }

        public void ResetJoint()
        {
            if (joint != null)
            {
                if (initialLocalRotation.w == 0 && initialLocalRotation.x == 0 && initialLocalRotation.y == 0 && initialLocalRotation.z == 0)
                {
                    initialLocalRotation = Quaternion.identity;
                }

                joint.transform.localRotation = initialLocalRotation;
                joint.targetRotation = Quaternion.identity;
                if (jointRb != null)
                {
                    jointRb.linearVelocity = Vector3.zero;
                    jointRb.angularVelocity = Vector3.zero;
                }
            }
        }

        public void SetTargetAngle(float normalizedAction)
        {
            if (joint == null) return;

            float targetAngle = Mathf.Lerp(minAngle, maxAngle, (normalizedAction + 1f) * 0.5f);
            Quaternion deltaRotation = Quaternion.AngleAxis(targetAngle, motionAxis);
            joint.targetRotation = Quaternion.Inverse(deltaRotation);
        }

        public float GetCurrentAngle()
        {
            if (joint == null) return 0f;

            Quaternion baseRot = initialLocalRotation;
            if (baseRot.w == 0 && baseRot.x == 0 && baseRot.y == 0 && baseRot.z == 0)
            {
                baseRot = Quaternion.identity;
            }

            Quaternion currentRelative = Quaternion.Inverse(baseRot) * joint.transform.localRotation;
            currentRelative.ToAngleAxis(out float angle, out Vector3 axis);
            if (Vector3.Dot(axis, motionAxis) < 0) angle = -angle;
            return Mathf.DeltaAngle(0f, angle);
        }
    }
}
