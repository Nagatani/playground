// =====================================================================================
// TrafficLightAgent.cs
// ML-Agents の Agent を継承した信号制御エージェント。
// 「維持 / 切り替え要求」の 2 択を出し、黄・全赤の安全制御は TrafficLightController に任せる。
//
// ---- 観察 (Vector Observation, Space Size = 9) --------------------------------------
//   [0] 東西 待機台数        / queueNormalization      (0..1 にクランプ)
//   [1] 東西 平均待機時間    / waitTimeNormalization
//   [2] 南北 待機台数        / queueNormalization
//   [3] 南北 平均待機時間    / waitTimeNormalization
//   [4..6] フェーズ one-hot  : 東西青 / 南北青 / 遷移中（黄・全赤）
//   [7] 現ステージ経過時間   / greenTimeNormalization
//   [8] 最小青時間の残り     / MinGreenTicks（0 なら切り替え可能）
//
// ---- 行動 (Discrete, 1 branch, size 2) ----------------------------------------------
//   0: フェーズ維持 / 1: フェーズ切り替え要求
//   切り替え不能なとき（最小青時間内・遷移中）は行動マスクで 1 を無効化する。
//
// ---- 報酬 ----------------------------------------------------------------------------
//   + passReward × 通過台数
//   - waitPenaltyPerVehicleSecond × 累積待機時間の増分 [台・秒]
//   - switchPenalty（実際に切り替えたとき）
//   - collisionPenalty / deadlockPenalty して EndEpisode()
//   ※ ステップ報酬の平均が負だと「わざとデッドロックさせて早く終わらせる」方が得になり得る。
//     deadlockPenalty は「残りエピソードで受けるはずの待機ペナルティ」より十分大きくすること。
//
// ---- Unity 6 エディタでの設定（SignalAgent GameObject）--------------------------------
//  本コンポーネントを追加すると Behavior Parameters と Decision Requester も自動で付く。
//
//  [Behavior Parameters]
//    Behavior Name              : TrafficLight   ← config/trainer_config.yaml の behaviors キーと一致させる
//    Vector Observation
//      Space Size               : 9
//      Stacked Vectors          : 1（時間的文脈が欲しければ 3〜4）
//    Actions
//      Continuous Actions       : 0
//      Discrete Branches        : 1
//        Branch 0 Size          : 2
//    Model                      : 学習時は None。学習後に生成された .onnx を割り当てる
//    Inference Device           : Default
//    Behavior Type              : Default（学習/推論）、手動操作・ベースライン確認は Heuristic Only
//    Use Child Sensors          : OFF
//
//  [Decision Requester]
//    Decision Period            : 20（Fixed Timestep 0.05 なら 1 秒ごとに判断）
//    Take Actions Between Decisions : ON
//      → OnActionReceived が毎ティック呼ばれ、報酬がティック単位で正しい行動に帰属する。
//        切り替え要求が繰り返されても、遷移開始後は CanSwitch=false なので二重切り替えは起きない。
//
//  [Traffic Light Agent（本コンポーネント）]
//    Max Step                   : 12000（Fixed Timestep 0.05 で 600 秒 = 10 分/エピソード）
//    Environment / Traffic Light: 未設定なら親階層から自動取得
// =====================================================================================
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AdaptiveTraffic
{
    [RequireComponent(typeof(DecisionRequester))]
    public class TrafficLightAgent : Agent
    {
        public const int ObservationSize = 9;
        const int PhaseBranch = 0;
        const int ActionKeep = 0;
        const int ActionSwitch = 1;

        /// <summary>trainer_config.yaml の environment_parameters で上書きできる需要倍率のキー。</summary>
        const string DemandScaleParameter = "demand_scale";

        public enum HeuristicMode
        {
            /// <summary>Space キーを押している間「切り替え」。</summary>
            Keyboard,

            /// <summary>固定サイクル（各方向 fixedCycleGreenSeconds ずつ青）。比較用ベースライン。</summary>
            FixedCycle,

            /// <summary>赤側の待機台数が青側より閾値以上多ければ切り替え。比較用ベースライン。</summary>
            LongestQueue,
        }

        [Header("参照（未設定なら親階層から自動取得）")]
        [SerializeField] TrafficEnvironmentManager environment;
        [SerializeField] TrafficLightController trafficLight;

        [Header("観察の正規化")]
        [SerializeField, Min(1f)] float queueNormalization = 20f;
        [SerializeField, Min(1f)] float waitTimeNormalization = 60f;
        [SerializeField, Min(1f)] float greenTimeNormalization = 60f;

        [Header("報酬")]
        [Tooltip("交差点を通過した車両 1 台あたりのボーナス")]
        [SerializeField] float passReward = 0.1f;

        [Tooltip("累積待機時間 1 台・秒あたりのペナルティ")]
        [SerializeField] float waitPenaltyPerVehicleSecond = 0.005f;

        [Tooltip("フェーズを切り替えるたびのペナルティ")]
        [SerializeField] float switchPenalty = 0.05f;

        [Tooltip("衝突時のペナルティ（エピソード終了）")]
        [SerializeField] float collisionPenalty = 10f;

        [Tooltip("デッドロック（極端な待ち・スピルバック）時のペナルティ（エピソード終了）")]
        [SerializeField] float deadlockPenalty = 10f;

        [Header("Heuristic（Behavior Type = Heuristic Only のとき）")]
        [SerializeField] HeuristicMode heuristicMode = HeuristicMode.Keyboard;
        [SerializeField, Min(0f)] float fixedCycleGreenSeconds = 20f;
        [SerializeField, Min(0)] int queueDifferenceThreshold = 3;

        public override void Initialize()
        {
            if (environment == null)
            {
                environment = GetComponentInParent<TrafficEnvironmentManager>();
            }

            if (trafficLight == null && environment != null)
            {
                trafficLight = environment.TrafficLight;
            }

            if (environment == null || trafficLight == null)
            {
                Debug.LogError($"[{nameof(TrafficLightAgent)}] Environment / Traffic Light が見つかりません。", this);
            }

            ValidateBehaviorParameters();
        }

        public override void OnEpisodeBegin()
        {
            RecordEpisodeStats();

            float demandScale = Academy.Instance.EnvironmentParameters.GetWithDefault(DemandScaleParameter, 1f);
            environment.ResetEnvironment(demandScale);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var eastWest = environment.GetAxisStats(TrafficAxis.EastWest);
            var northSouth = environment.GetAxisStats(TrafficAxis.NorthSouth);

            sensor.AddObservation(Mathf.Clamp01(eastWest.WaitingCount / queueNormalization));
            sensor.AddObservation(Mathf.Clamp01(eastWest.AverageWaitTime / waitTimeNormalization));
            sensor.AddObservation(Mathf.Clamp01(northSouth.WaitingCount / queueNormalization));
            sensor.AddObservation(Mathf.Clamp01(northSouth.AverageWaitTime / waitTimeNormalization));

            int phaseIndex = trafficLight.IsInTransition ? 2 : (int)trafficLight.GreenAxis;
            sensor.AddOneHotObservation(phaseIndex, 3);

            sensor.AddObservation(Mathf.Clamp01(trafficLight.StageElapsedSeconds / greenTimeNormalization));
            sensor.AddObservation(trafficLight.MinGreenTicks > 0
                ? trafficLight.MinGreenRemainingTicks / (float)trafficLight.MinGreenTicks
                : 0f);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            // 最小青時間内・黄/全赤中は「切り替え」を選ばせない
            actionMask.SetActionEnabled(PhaseBranch, ActionSwitch, trafficLight.CanSwitch);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (actions.DiscreteActions[PhaseBranch] == ActionSwitch && trafficLight.RequestSwitch())
            {
                AddReward(-switchPenalty);
            }

            var report = environment.ConsumeStepReport();
            AddReward(report.PassedCount * passReward);
            AddReward(-report.WaitingVehicleSeconds * waitPenaltyPerVehicleSecond);

            if (environment.CollisionDetected)
            {
                AddReward(-collisionPenalty);
                EndEpisode();
            }
            else if (environment.DeadlockDetected)
            {
                AddReward(-deadlockPenalty);
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[PhaseBranch] = heuristicMode switch
            {
                HeuristicMode.FixedCycle => FixedCycleAction(),
                HeuristicMode.LongestQueue => LongestQueueAction(),
                _ => KeyboardAction(),
            };
        }

        static int KeyboardAction()
        {
#if ENABLE_INPUT_SYSTEM
            // Unity 6 の新規プロジェクト既定（Input System Package）
            bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool pressed = Input.GetKey(KeyCode.Space);
#else
            bool pressed = false;
#endif
            return pressed ? ActionSwitch : ActionKeep;
        }

        int FixedCycleAction() =>
            trafficLight.CanSwitch && trafficLight.StageElapsedSeconds >= fixedCycleGreenSeconds
                ? ActionSwitch
                : ActionKeep;

        int LongestQueueAction()
        {
            if (!trafficLight.CanSwitch)
            {
                return ActionKeep;
            }

            var green = environment.GetAxisStats(trafficLight.GreenAxis);
            var red = environment.GetAxisStats(TrafficGeometry.Opposite(trafficLight.GreenAxis));
            return red.WaitingCount >= green.WaitingCount + queueDifferenceThreshold ? ActionSwitch : ActionKeep;
        }

        /// <summary>直前のエピソードの交通指標を TensorBoard に出力する。</summary>
        void RecordEpisodeStats()
        {
            if (environment == null || environment.EpisodeElapsedSeconds <= 0f)
            {
                return;
            }

            var stats = Academy.Instance.StatsRecorder;
            float minutes = environment.EpisodeElapsedSeconds / 60f;
            stats.Add("Traffic/PassedPerMinute", environment.EpisodePassedCount / minutes);
            stats.Add("Traffic/MeanWaitPerPassedVehicle", environment.EpisodeMeanWaitPerPassedVehicle);
            stats.Add("Traffic/WaitingVehicleSeconds", environment.EpisodeWaitingVehicleSeconds);
            stats.Add("Traffic/SwitchesPerMinute", trafficLight.SwitchCount / minutes);
            stats.Add("Traffic/CollisionRate", environment.CollisionDetected ? 1f : 0f);
            stats.Add("Traffic/DeadlockRate", environment.DeadlockDetected ? 1f : 0f);
        }

        void ValidateBehaviorParameters()
        {
            var behavior = GetComponent<BehaviorParameters>();
            if (behavior == null)
            {
                return;
            }

            if (behavior.BrainParameters.VectorObservationSize != ObservationSize)
            {
                Debug.LogWarning(
                    $"[{nameof(TrafficLightAgent)}] Behavior Parameters の Vector Observation Space Size を {ObservationSize} にしてください" +
                    $"（現在 {behavior.BrainParameters.VectorObservationSize}）。", this);
            }

            var branches = behavior.BrainParameters.ActionSpec.BranchSizes;
            if (behavior.BrainParameters.ActionSpec.NumContinuousActions != 0 ||
                branches == null || branches.Length != 1 || branches[0] != 2)
            {
                Debug.LogWarning(
                    $"[{nameof(TrafficLightAgent)}] Actions は Continuous 0 / Discrete Branches 1 / Branch 0 Size 2 にしてください。", this);
            }
        }
    }
}
