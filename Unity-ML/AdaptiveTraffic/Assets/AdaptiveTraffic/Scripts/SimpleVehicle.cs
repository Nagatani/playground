// =====================================================================================
// SimpleVehicle.cs
// 経路（VehiclePath）に沿って走る運動学ベースの車両。
// 物理演算（Rigidbody / Collider）は使わず、道のり s を積分して位置を決める。
// こうすると学習中の高速実行（time_scale 20）でも挙動が決定的で安定する。
//
// 挙動:
//   - 先行車両との車間 (gap - minGap) と、停止すべき場合の停止線までの距離のうち
//     小さい方を「進める余地」とし、そこで止まれる速度 v = sqrt(2 * decel * 余地) を上限にする。
//   - 黄信号: その時点の制動距離 v^2 / (2 * decel) より停止線が近ければ「止まれない」と判断して進行
//     （ジレンマゾーン処理）。判断は黄の間 1 回だけ行い、以後は固定する。
//   - 停止線を越えた車両はもう信号を見ない（交差点内で止まらない）。
//   - 停止線手前で速度が waitSpeedThreshold 未満の間、WaitTime を累積する。
//
// Tick() は TrafficEnvironmentManager.FixedUpdate からスポーン順に呼ばれる（自前の Update は持たない）。
// 通過判定・破棄・統計はマネージャー側で行う。
//
// ---- Unity 6 エディタでの設定（車両プレハブ）----------------------------------------
//  1. Hierarchy で 3D Object > Cube を作成し "Vehicle" にリネーム。
//     Scale = (1.8, 1.4, 4.5)（幅・高さ・全長。全長は下の Length と合わせる）。
//  2. BoxCollider を削除（衝突判定はマネージャーが距離ベースで行うため不要）。
//  3. 本コンポーネントをアタッチし、Body Renderer に Cube の MeshRenderer を設定（任意）。
//     待機時間に応じて車体色が Moving Color → Waiting Color へ変化する。
//  4. Assets/AdaptiveTraffic/Prefabs/Vehicle.prefab として保存し、シーン上の実体は削除。
//  5. TrafficEnvironmentManager の Vehicle Prefab に割り当てる。
// =====================================================================================
using UnityEngine;

namespace AdaptiveTraffic
{
    [DisallowMultipleComponent]
    public class SimpleVehicle : MonoBehaviour
    {
        /// <summary>停止時に停止線を踏まないための余白 [m]。</summary>
        const float StopLineEpsilon = 0.05f;

        [Header("走行特性")]
        [Tooltip("最高速度 [m/s]（13.9 m/s ≒ 50 km/h）")]
        [SerializeField, Min(0.1f)] float maxSpeed = 13.9f;

        [Tooltip("加速度 [m/s^2]")]
        [SerializeField, Min(0.1f)] float acceleration = 2.5f;

        [Tooltip("減速度 [m/s^2]。停止計画と黄信号のジレンマ判定に使用")]
        [SerializeField, Min(0.1f)] float deceleration = 4.0f;

        [Tooltip("車両全長 [m]。プレハブの Z スケールと合わせる")]
        [SerializeField, Min(0.5f)] float length = 4.5f;

        [Tooltip("停止時に先行車両との間に空ける安全マージン [m]")]
        [SerializeField, Min(0f)] float minGap = 2.0f;

        [Tooltip("この速度未満で停止線手前にいる間を「待機中」とみなす [m/s]")]
        [SerializeField, Min(0f)] float waitSpeedThreshold = 0.5f;

        [Header("表示（任意）")]
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Color movingColor = new Color(0.25f, 0.5f, 1f);
        [SerializeField] Color waitingColor = new Color(1f, 0.15f, 0.1f);

        [Tooltip("この待機時間 [s] で車体色が Waiting Color に達する")]
        [SerializeField, Min(1f)] float waitColorSaturationSeconds = 60f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        TrafficLightController trafficLight;
        SimpleVehicle leader;
        bool yellowDecisionMade;
        bool proceedOnYellow;
        MaterialPropertyBlock propertyBlock;

        public VehiclePath Path { get; private set; }
        public Approach Approach => Path.Approach;
        public TrafficAxis Axis => TrafficGeometry.AxisOf(Path.Approach);

        /// <summary>現在速度 [m/s]。</summary>
        public float Speed { get; private set; }

        /// <summary>経路始点からの前端の道のり [m]。</summary>
        public float DistanceTravelled { get; private set; }

        /// <summary>停止線手前で停止（低速）していた累積時間 [s]。</summary>
        public float WaitTime { get; private set; }

        public float Length => length;
        public float MinGap => minGap;
        public float RearDistance => DistanceTravelled - length;

        /// <summary>前端が停止線を越えたか（越えた後は信号に従わない）。</summary>
        public bool HasPassedStopLine => DistanceTravelled >= Path.StopLineDistance;

        /// <summary>停止線手前で待機中か。</summary>
        public bool IsWaiting => !HasPassedStopLine && Speed < waitSpeedThreshold;

        /// <summary>車体の一部でも交差点ボックス内にあるか（衝突判定対象）。</summary>
        public bool IsInsideIntersection =>
            DistanceTravelled > Path.IntersectionEntryDistance && RearDistance < Path.IntersectionExitDistance;

        /// <summary>後端が交差点ボックスを抜けたか（＝通過完了）。</summary>
        public bool HasClearedIntersection => RearDistance >= Path.IntersectionExitDistance;

        /// <summary>経路終端に達したか（破棄対象）。</summary>
        public bool HasReachedEnd => RearDistance >= Path.Length;

        /// <summary>通過台数に計上済みか（マネージャーが管理）。</summary>
        public bool PassCounted { get; internal set; }

        /// <summary>
        /// スポーン直後にマネージャーから呼ばれる。
        /// </summary>
        /// <param name="light">従う信号機</param>
        /// <param name="path">走行経路</param>
        /// <param name="leaderVehicle">同一レーンの直前の車両（いなければ null）</param>
        public void Initialize(TrafficLightController light, VehiclePath path, SimpleVehicle leaderVehicle)
        {
            trafficLight = light;
            Path = path;
            leader = leaderVehicle;
            DistanceTravelled = 0f;
            Speed = maxSpeed; // 先行車両が近い場合は最初の Tick で安全速度まで落ちる
            WaitTime = 0f;
            PassCounted = false;
            yellowDecisionMade = false;
            proceedOnYellow = false;

            UpdateTransform();
            UpdateVisual();
        }

        public void Tick(float dt)
        {
            // 1) 進める余地（この距離以内で必ず止まれるようにする）
            float freeDistance = float.PositiveInfinity;

            if (TryGetLeaderGap(out float gap))
            {
                freeDistance = gap - minGap;
            }

            if (!HasPassedStopLine)
            {
                float distanceToLine = Path.StopLineDistance - DistanceTravelled - StopLineEpsilon;
                if (MustStopAtLine(distanceToLine))
                {
                    freeDistance = Mathf.Min(freeDistance, distanceToLine);
                }
            }

            // 2) 速度更新
            float safeSpeed = float.IsPositiveInfinity(freeDistance)
                ? maxSpeed
                : Mathf.Sqrt(2f * deceleration * Mathf.Max(0f, freeDistance));
            float targetSpeed = Mathf.Min(maxSpeed, safeSpeed);
            Speed = Speed < targetSpeed ? Mathf.Min(targetSpeed, Speed + acceleration * dt) : targetSpeed;

            // 3) 位置更新（離散化誤差で余地を超えないようクランプ）
            float step = Mathf.Min(Speed * dt, Mathf.Max(0f, freeDistance));
            Speed = dt > 0f ? step / dt : 0f;
            DistanceTravelled += step;

            // 4) 待機時間
            if (IsWaiting)
            {
                WaitTime += dt;
            }

            UpdateTransform();
            UpdateVisual();
        }

        bool MustStopAtLine(float distanceToLine)
        {
            switch (trafficLight.GetSignal(Approach))
            {
                case SignalColor.Green:
                    yellowDecisionMade = false;
                    proceedOnYellow = false;
                    return false;

                case SignalColor.Yellow:
                    if (!yellowDecisionMade)
                    {
                        float stoppingDistance = Speed * Speed / (2f * deceleration);
                        proceedOnYellow = distanceToLine < stoppingDistance;
                        yellowDecisionMade = true;
                    }

                    return !proceedOnYellow;

                default:
                    // 黄で進行を決めた車両はそのまま抜ける（全赤時間がこれを吸収する）
                    return !proceedOnYellow;
            }
        }

        bool TryGetLeaderGap(out float gap)
        {
            gap = 0f;

            // Unity の == 演算子で破棄済みオブジェクトも null 扱いになる
            if (leader == null)
            {
                leader = null;
                return false;
            }

            // 先行車両が別ルート（右左折）で、車体が完全に交差点へ入った後は自分の進路上にいない
            if (leader.Path != Path && leader.RearDistance > Path.IntersectionEntryDistance)
            {
                leader = null;
                return false;
            }

            // 停止線までは全ルートで経路が共通なので道のりの差がそのまま車間になる
            gap = leader.RearDistance - DistanceTravelled;
            return true;
        }

        void UpdateTransform()
        {
            // 道のりは前端基準なので、車体中心は length/2 手前
            Path.Evaluate(DistanceTravelled - length * 0.5f, out Vector3 position, out Vector3 forward);
            transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        void UpdateVisual()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            Color color = Color.Lerp(movingColor, waitingColor, WaitTime / waitColorSaturationSeconds);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
