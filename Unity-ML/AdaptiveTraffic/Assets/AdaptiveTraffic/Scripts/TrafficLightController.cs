// =====================================================================================
// TrafficLightController.cs
// 信号機の点灯状態（青・黄・赤）とフェーズ遷移ステートマシンを管理する。
//
// ステートマシン（GreenAxis = 現在通行権を持つ軸）:
//
//   [Green(GreenAxis)] --RequestSwitch()※--> [Yellow(GreenAxis)] --yellowTicks--> [AllRed]
//          ^                                                                        |
//          +------------------- allRedTicks 経過で GreenAxis を反転 ------------------+
//
//   ※ RequestSwitch() は Green 中かつ最小青時間 (minGreen) 経過後のみ受理される。
//     エージェントは「切り替え要求」しか出せず、黄・全赤（クリアランス）は必ずこのクラスが挟む。
//
// 時間はすべて FixedUpdate のティック数で管理する（秒指定 → Time.fixedDeltaTime で換算）。
// Tick() は TrafficEnvironmentManager.FixedUpdate から毎ティック呼ばれる（自前の FixedUpdate は持たない）。
//
// ---- Unity 6 エディタでの設定 -------------------------------------------------------
//  1. TrafficArea（TrafficEnvironmentManager を持つ GameObject）の子に空の GameObject
//     "TrafficLight" を作成し、本コンポーネントをアタッチ。
//  2. 各アプローチの停止線付近に Sphere 等を置き、Collider は削除（不要）。
//     東西アプローチ用の灯器を East West Lamps、南北用を North South Lamps に登録（任意）。
//     マテリアルは URP/Lit（_BaseColor）でも Built-in Standard（_Color）でも可。
//  3. Min Green Seconds / Yellow Seconds / All Red Seconds を設定。
//     yellow + allRed は「黄で進入した車両が交差点を抜け切る時間」以上にすること
//     （既定: 制限速度 50km/h・交差点幅 14m で 3s + 2s）。
// =====================================================================================
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AdaptiveTraffic
{
    [DisallowMultipleComponent]
    public class TrafficLightController : MonoBehaviour
    {
        public enum Stage
        {
            Green = 0,
            Yellow = 1,
            AllRed = 2,
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP
        static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

        [Header("フェーズ時間 [秒]（Fixed Timestep でティック数に換算）")]
        [Tooltip("青になってから切り替え要求を受け付けるまでの最小時間")]
        [SerializeField, Min(0f)] float minGreenSeconds = 10f;

        [Tooltip("黄信号の時間（最低 1 ティック）")]
        [SerializeField, Min(0f)] float yellowSeconds = 3f;

        [Tooltip("全方向赤（クリアランス）の時間（最低 1 ティック）")]
        [SerializeField, Min(0f)] float allRedSeconds = 2f;

        [Header("初期状態")]
        [SerializeField] TrafficAxis initialGreenAxis = TrafficAxis.EastWest;

        [Tooltip("エピソード開始時に初期青軸をランダムにする（学習時推奨）")]
        [SerializeField] bool randomizeInitialAxis = true;

        [Header("表示（任意）")]
        [SerializeField] Renderer[] eastWestLamps = Array.Empty<Renderer>();
        [SerializeField] Renderer[] northSouthLamps = Array.Empty<Renderer>();
        [SerializeField] Color greenColor = new Color(0.1f, 0.85f, 0.25f);
        [SerializeField] Color yellowColor = new Color(1f, 0.8f, 0.1f);
        [SerializeField] Color redColor = new Color(0.9f, 0.1f, 0.1f);

        MaterialPropertyBlock propertyBlock;

        /// <summary>現在（または黄・全赤中は直前まで）通行権を持っていた軸。</summary>
        public TrafficAxis GreenAxis { get; private set; }

        public Stage CurrentStage { get; private set; }

        /// <summary>現在のステージ（青/黄/全赤）に入ってからの経過ティック数。</summary>
        public int StageElapsedTicks { get; private set; }

        public int MinGreenTicks { get; private set; }
        public int YellowTicks { get; private set; }
        public int AllRedTicks { get; private set; }

        /// <summary>現エピソードで実際に切り替えが行われた回数。</summary>
        public int SwitchCount { get; private set; }

        public float StageElapsedSeconds => StageElapsedTicks * Time.fixedDeltaTime;
        public bool IsInTransition => CurrentStage != Stage.Green;

        /// <summary>今 RequestSwitch() を呼べば受理されるか（行動マスクに使用）。</summary>
        public bool CanSwitch => CurrentStage == Stage.Green && StageElapsedTicks >= MinGreenTicks;

        /// <summary>最小青時間の残りティック数。黄・全赤中は次の青の最小青時間全体を返す。</summary>
        public int MinGreenRemainingTicks =>
            CurrentStage == Stage.Green ? Mathf.Max(0, MinGreenTicks - StageElapsedTicks) : MinGreenTicks;

        /// <summary>ステージが変化したときに通知される。</summary>
        public event Action<TrafficLightController> StageChanged;

        void Awake()
        {
            RecalculateTicks();
            GreenAxis = initialGreenAxis;
            CurrentStage = Stage.Green;
            ApplyVisuals();
        }

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                RecalculateTicks();
            }
        }

        /// <summary>エピソード開始時に呼ぶ。青から開始し、統計をクリアする。</summary>
        public void ResetController()
        {
            RecalculateTicks();
            GreenAxis = randomizeInitialAxis
                ? (Random.value < 0.5f ? TrafficAxis.EastWest : TrafficAxis.NorthSouth)
                : initialGreenAxis;
            SwitchCount = 0;
            EnterStage(Stage.Green);
        }

        /// <summary>
        /// フェーズ切り替えを要求する。受理されれば黄信号へ遷移して true を返す。
        /// 最小青時間内・黄/全赤中の要求は無視される（false）。
        /// </summary>
        public bool RequestSwitch()
        {
            if (!CanSwitch)
            {
                return false;
            }

            SwitchCount++;
            EnterStage(Stage.Yellow);
            return true;
        }

        /// <summary>1 ティック進める。TrafficEnvironmentManager から毎 FixedUpdate 呼ばれる。</summary>
        public void Tick()
        {
            StageElapsedTicks++;

            if (CurrentStage == Stage.Yellow && StageElapsedTicks >= YellowTicks)
            {
                EnterStage(Stage.AllRed);
            }
            else if (CurrentStage == Stage.AllRed && StageElapsedTicks >= AllRedTicks)
            {
                GreenAxis = TrafficGeometry.Opposite(GreenAxis);
                EnterStage(Stage.Green);
            }
        }

        public SignalColor GetSignal(TrafficAxis axis)
        {
            if (axis != GreenAxis)
            {
                return SignalColor.Red;
            }

            return CurrentStage switch
            {
                Stage.Green => SignalColor.Green,
                Stage.Yellow => SignalColor.Yellow,
                _ => SignalColor.Red,
            };
        }

        public SignalColor GetSignal(Approach approach) => GetSignal(TrafficGeometry.AxisOf(approach));

        public Color GetDisplayColor(SignalColor signal) => signal switch
        {
            SignalColor.Green => greenColor,
            SignalColor.Yellow => yellowColor,
            _ => redColor,
        };

        void RecalculateTicks()
        {
            float dt = Mathf.Max(1e-4f, Time.fixedDeltaTime);
            MinGreenTicks = Mathf.CeilToInt(minGreenSeconds / dt);
            YellowTicks = Mathf.Max(1, Mathf.CeilToInt(yellowSeconds / dt));
            AllRedTicks = Mathf.Max(1, Mathf.CeilToInt(allRedSeconds / dt));
        }

        void EnterStage(Stage stage)
        {
            CurrentStage = stage;
            StageElapsedTicks = 0;
            ApplyVisuals();
            StageChanged?.Invoke(this);
        }

        void ApplyVisuals()
        {
            SetLampColor(eastWestLamps, GetSignal(TrafficAxis.EastWest));
            SetLampColor(northSouthLamps, GetSignal(TrafficAxis.NorthSouth));
        }

        void SetLampColor(Renderer[] lamps, SignalColor signal)
        {
            if (lamps == null || lamps.Length == 0)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            Color color = GetDisplayColor(signal);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);

            foreach (var lamp in lamps)
            {
                if (lamp != null)
                {
                    lamp.SetPropertyBlock(propertyBlock);
                }
            }
        }
    }
}
