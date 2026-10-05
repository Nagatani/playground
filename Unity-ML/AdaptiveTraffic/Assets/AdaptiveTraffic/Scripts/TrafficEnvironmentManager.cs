// =====================================================================================
// TrafficEnvironmentManager.cs
// 1 交差点ぶんの環境（トレーニングエリア）を管理する。
//   - 経路ジオメトリの生成（4 アプローチ × 2 車線、直進 / 歩道側の右左折）
//   - 車両のスポーン（固定間隔 or ポアソン到着）とエピソード毎の需要ランダム化
//   - シミュレーションティックの駆動: 信号 → スポーン → 車両移動 → 通過/破棄 → 統計 → 衝突判定
//   - 統計集計（軸別の待機台数・平均待機時間、通過台数、累積待機時間）
//   - 衝突・デッドロック（極端な待ち / スピルバック）の検知
//
// 全座標は本 GameObject のローカル座標。エリアを複製して位置をずらせば並列学習できる。
//
// ---- Unity 6 エディタでの設定 -------------------------------------------------------
//  推奨 Hierarchy:
//    TrafficArea              ← 本コンポーネント（Transform は自由。交差点中心になる）
//     ├─ TrafficLight         ← TrafficLightController
//     ├─ SignalAgent          ← TrafficLightAgent + Behavior Parameters + Decision Requester
//     └─ Road (任意)          ← 見た目用の Plane 等（Collider 不要）
//    ※ 実行時に子 "Vehicles" が自動生成され、車両はその下にスポーンされる。
//
//  インスペクター:
//    - Traffic Light   : 子の TrafficLightController（未設定なら子から自動取得）
//    - Vehicle Prefab  : SimpleVehicle 付きプレハブ
//    - Driving Side    : 日本なら Left
//    - Lane Width 3.5m の場合、交差点ボックスは 14m × 14m（片側 2 車線 × 2 方向）
//    - Scene ビューで Gizmos を ON にすると車線・停止線・交差点ボックスが表示される
//
//  並列学習: TrafficArea をプレハブ化し、互いに 200m 以上離して複数配置する
//  （車両はエリア外を参照しないので重なっていなければ距離は任意）。
//
//  Project Settings > Time > Fixed Timestep は 0.05（20Hz）を推奨。
//  信号・車両の時間はすべてこの値から換算される。
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveTraffic
{
    public enum SpawnMode
    {
        /// <summary>平均間隔ちょうどで発生。</summary>
        Fixed,

        /// <summary>指数分布の間隔（ポアソン到着）。</summary>
        Poisson,
    }

    [DisallowMultipleComponent]
    public class TrafficEnvironmentManager : MonoBehaviour
    {
        /// <summary>軸（東西/南北）ごとの進入レーン統計。停止線手前の車両のみ対象。</summary>
        public struct AxisStats
        {
            /// <summary>停止線手前にいる車両数（走行中含む）。</summary>
            public int ApproachingCount;

            /// <summary>停止線手前で待機中の車両数。</summary>
            public int WaitingCount;

            /// <summary>待機中車両の WaitTime 平均 [s]。</summary>
            public float AverageWaitTime;

            /// <summary>待機中車両の WaitTime 最大 [s]。</summary>
            public float MaxWaitTime;
        }

        /// <summary>前回 ConsumeStepReport() 以降に発生した事象（エージェントの報酬計算用）。</summary>
        public struct StepReport
        {
            /// <summary>交差点を通過し終えた車両数。</summary>
            public int PassedCount;

            /// <summary>待機台数 × 経過時間の積算 [台・秒]（＝全車両の累積待機時間の増分）。</summary>
            public float WaitingVehicleSeconds;
        }

        [Header("参照")]
        [SerializeField] TrafficLightController trafficLight;
        [SerializeField] SimpleVehicle vehiclePrefab;

        [Header("ジオメトリ（ローカル座標, 交差点中心 = 原点）")]
        [SerializeField] DrivingSide drivingSide = DrivingSide.Left;
        [SerializeField, Min(2f)] float laneWidth = 3.5f;

        [Tooltip("交差点ボックス端から停止線までの後退距離 [m]")]
        [SerializeField, Min(0f)] float stopLineSetback = 1.5f;

        [Tooltip("交差点中心からスポーン地点までの距離 [m]")]
        [SerializeField, Min(20f)] float spawnDistance = 80f;

        [Tooltip("交差点中心から破棄地点までの距離 [m]")]
        [SerializeField, Min(10f)] float despawnDistance = 50f;

        [Tooltip("右左折カーブの分割数")]
        [SerializeField, Range(2, 32)] int curveSegments = 8;

        [Header("交通需要")]
        [SerializeField] SpawnMode spawnMode = SpawnMode.Poisson;

        [Tooltip("東西の各アプローチの平均発生間隔 [s] の範囲。エピソード毎にこの範囲から一様抽選する")]
        [SerializeField] Vector2 eastWestIntervalRange = new Vector2(3f, 8f);

        [Tooltip("南北の各アプローチの平均発生間隔 [s] の範囲。エピソード毎にこの範囲から一様抽選する")]
        [SerializeField] Vector2 northSouthIntervalRange = new Vector2(3f, 8f);

        [Tooltip("ポアソン到着時の最小発生間隔 [s]")]
        [SerializeField, Min(0.1f)] float minSpawnInterval = 1.0f;

        [Tooltip("歩道側車線の車両が右左折する確率（中央側車線は常に直進）")]
        [SerializeField, Range(0f, 1f)] float curbSideTurnRatio = 0.3f;

        [SerializeField, Min(1)] int maxVehicles = 200;

        [Header("安全・終了判定")]
        [Tooltip("交差点内で異なるアプローチの車両中心がこの距離未満なら衝突とみなす [m]")]
        [SerializeField, Min(0.1f)] float collisionDistance = 2.0f;

        [Tooltip("いずれかの車両の待機時間がこれを超えたらデッドロック（飢餓）とみなす [s]")]
        [SerializeField, Min(1f)] float deadlockWaitSeconds = 120f;

        [Tooltip("待ち行列がスポーン地点まで伸びてスポーン不能な状態がこれだけ続いたらデッドロックとみなす [s]")]
        [SerializeField, Min(1f)] float spillbackSeconds = 30f;

        [Header("デバッグ")]
        [SerializeField] bool drawGizmos = true;

        readonly List<SimpleVehicle> vehicles = new List<SimpleVehicle>();
        readonly List<SimpleVehicle> insideIntersection = new List<SimpleVehicle>();
        readonly VehiclePath[,,] paths =
            new VehiclePath[TrafficGeometry.ApproachCount, TrafficGeometry.LanesPerApproach, TrafficGeometry.RouteTypeCount];
        readonly SimpleVehicle[,] lastInLane =
            new SimpleVehicle[TrafficGeometry.ApproachCount, TrafficGeometry.LanesPerApproach];
        readonly float[] meanSpawnInterval = new float[TrafficGeometry.ApproachCount];
        readonly float[] spawnTimers = new float[TrafficGeometry.ApproachCount];
        readonly float[] spawnBlockedSeconds = new float[TrafficGeometry.ApproachCount];
        readonly AxisStats[] axisStats = new AxisStats[TrafficGeometry.AxisCount];

        Transform vehicleRoot;
        bool initialized;
        bool hasBeenReset;
        int spawnSerial;

        int pendingPassedCount;
        float pendingWaitingVehicleSeconds;

        public TrafficLightController TrafficLight => trafficLight;
        public IReadOnlyList<SimpleVehicle> Vehicles => vehicles;

        /// <summary>交差点ボックスの半辺長 [m]（片側 2 車線 × 車線幅）。</summary>
        public float IntersectionHalfSize => laneWidth * TrafficGeometry.LanesPerApproach;

        public bool CollisionDetected { get; private set; }
        public bool DeadlockDetected { get; private set; }

        // ---- エピソード統計（TensorBoard 出力・評価用）----
        public float EpisodeElapsedSeconds { get; private set; }
        public int EpisodeSpawnedCount { get; private set; }
        public int EpisodePassedCount { get; private set; }

        /// <summary>エピソード中の全車両の累積待機時間 [台・秒]。</summary>
        public float EpisodeWaitingVehicleSeconds { get; private set; }

        /// <summary>通過した車両の WaitTime の合計 [s]。</summary>
        public float EpisodeWaitTimeOfPassed { get; private set; }

        public float EpisodeMeanWaitPerPassedVehicle =>
            EpisodePassedCount > 0 ? EpisodeWaitTimeOfPassed / EpisodePassedCount : 0f;

        void Awake()
        {
            EnsureInitialized();
        }

        void Start()
        {
            // エージェントが無い場合（手動テスト）でも動くように自前でもリセットする
            if (!hasBeenReset)
            {
                ResetEnvironment();
            }
        }

        void OnValidate()
        {
            if (initialized)
            {
                RebuildPaths();
            }
        }

        void FixedUpdate()
        {
            if (!initialized || !hasBeenReset)
            {
                return;
            }

            Simulate(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 環境を初期状態に戻す。TrafficLightAgent.OnEpisodeBegin から呼ばれる。
        /// </summary>
        /// <param name="demandScale">需要倍率（2 なら発生間隔が半分 = 交通量 2 倍）。カリキュラム学習用。</param>
        public void ResetEnvironment(float demandScale = 1f)
        {
            EnsureInitialized();
            if (!initialized)
            {
                return;
            }

            foreach (var vehicle in vehicles)
            {
                if (vehicle != null)
                {
                    // Destroy はフレーム末まで遅延するので即座に非表示にする
                    vehicle.gameObject.SetActive(false);
                    Destroy(vehicle.gameObject);
                }
            }

            vehicles.Clear();
            insideIntersection.Clear();
            System.Array.Clear(lastInLane, 0, lastInLane.Length);

            demandScale = Mathf.Max(0.01f, demandScale);
            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                Vector2 range = TrafficGeometry.AxisOf((Approach)a) == TrafficAxis.EastWest
                    ? eastWestIntervalRange
                    : northSouthIntervalRange;
                meanSpawnInterval[a] = Random.Range(range.x, range.y) / demandScale;
                spawnTimers[a] = Random.Range(0f, meanSpawnInterval[a]); // アプローチ間で位相をずらす
                spawnBlockedSeconds[a] = 0f;
            }

            CollisionDetected = false;
            DeadlockDetected = false;
            pendingPassedCount = 0;
            pendingWaitingVehicleSeconds = 0f;
            EpisodeElapsedSeconds = 0f;
            EpisodeSpawnedCount = 0;
            EpisodePassedCount = 0;
            EpisodeWaitingVehicleSeconds = 0f;
            EpisodeWaitTimeOfPassed = 0f;

            trafficLight.ResetController();
            UpdateStatistics(0f);
            hasBeenReset = true;
        }

        public AxisStats GetAxisStats(TrafficAxis axis) => axisStats[(int)axis];

        /// <summary>前回呼び出し以降の通過台数・累積待機時間を返し、カウンタをクリアする。</summary>
        public StepReport ConsumeStepReport()
        {
            var report = new StepReport
            {
                PassedCount = pendingPassedCount,
                WaitingVehicleSeconds = pendingWaitingVehicleSeconds,
            };
            pendingPassedCount = 0;
            pendingWaitingVehicleSeconds = 0f;
            return report;
        }

        void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            if (trafficLight == null)
            {
                trafficLight = GetComponentInChildren<TrafficLightController>();
            }

            if (trafficLight == null || vehiclePrefab == null)
            {
                Debug.LogError($"[{nameof(TrafficEnvironmentManager)}] Traffic Light と Vehicle Prefab を設定してください。", this);
                enabled = false;
                return;
            }

            var root = new GameObject("Vehicles").transform;
            root.SetParent(transform, false);
            vehicleRoot = root;

            RebuildPaths();
            initialized = true;
        }

        // ------------------------------------------------------------------
        // シミュレーション
        // ------------------------------------------------------------------

        void Simulate(float dt)
        {
            EpisodeElapsedSeconds += dt;

            trafficLight.Tick();
            UpdateSpawning(dt);

            // スポーン順 = 同一レーン内で先行車が先に更新される順
            for (int i = 0; i < vehicles.Count; i++)
            {
                vehicles[i].Tick(dt);
            }

            ProcessPassAndDespawn();
            UpdateStatistics(dt);
            DetectCollisions();
        }

        void UpdateSpawning(float dt)
        {
            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                spawnTimers[a] -= dt;
                if (spawnTimers[a] > 0f || vehicles.Count >= maxVehicles)
                {
                    continue;
                }

                if (TrySpawn((Approach)a))
                {
                    spawnTimers[a] = NextSpawnInterval(a);
                    spawnBlockedSeconds[a] = 0f;
                }
                else
                {
                    // 両車線とも入口が詰まっている → 次ティックで再試行（スピルバック計測）
                    spawnBlockedSeconds[a] += dt;
                }
            }
        }

        bool TrySpawn(Approach approach)
        {
            int firstLane = Random.Range(0, TrafficGeometry.LanesPerApproach);
            for (int k = 0; k < TrafficGeometry.LanesPerApproach; k++)
            {
                int lane = (firstLane + k) % TrafficGeometry.LanesPerApproach;
                if (IsLaneEntryClear(approach, lane))
                {
                    SpawnVehicle(approach, lane);
                    return true;
                }
            }

            return false;
        }

        bool IsLaneEntryClear(Approach approach, int lane)
        {
            var last = lastInLane[(int)approach, lane];
            return last == null || last.RearDistance >= last.MinGap;
        }

        void SpawnVehicle(Approach approach, int lane)
        {
            RouteType route = lane == TrafficGeometry.OuterLane && Random.value < curbSideTurnRatio
                ? RouteType.CurbSideTurn
                : RouteType.Straight;

            var vehicle = Instantiate(vehiclePrefab, vehicleRoot);
            vehicle.name = $"Vehicle_{approach}_{lane}_{route}_{spawnSerial++}";
            vehicle.Initialize(trafficLight, paths[(int)approach, lane, (int)route], lastInLane[(int)approach, lane]);

            lastInLane[(int)approach, lane] = vehicle;
            vehicles.Add(vehicle);
            EpisodeSpawnedCount++;
        }

        float NextSpawnInterval(int approachIndex)
        {
            float mean = meanSpawnInterval[approachIndex];
            if (spawnMode == SpawnMode.Fixed)
            {
                return mean;
            }

            // 指数分布: -mean * ln(U)
            float u = Mathf.Max(1e-4f, 1f - Random.value);
            return Mathf.Max(minSpawnInterval, -mean * Mathf.Log(u));
        }

        void ProcessPassAndDespawn()
        {
            for (int i = vehicles.Count - 1; i >= 0; i--)
            {
                var vehicle = vehicles[i];

                if (!vehicle.PassCounted && vehicle.HasClearedIntersection)
                {
                    vehicle.PassCounted = true;
                    pendingPassedCount++;
                    EpisodePassedCount++;
                    EpisodeWaitTimeOfPassed += vehicle.WaitTime;
                }

                if (vehicle.HasReachedEnd)
                {
                    vehicles.RemoveAt(i);
                    int a = (int)vehicle.Approach;
                    int lane = vehicle.Path.Lane;
                    if (lastInLane[a, lane] == vehicle)
                    {
                        lastInLane[a, lane] = null;
                    }

                    // TODO(Step 2+): 学習の高速化が必要ならオブジェクトプールに置き換える
                    Destroy(vehicle.gameObject);
                }
            }
        }

        void UpdateStatistics(float dt)
        {
            // 毎ティック呼ばれるので GC を避けて stackalloc を使う
            System.Span<int> approaching = stackalloc int[TrafficGeometry.AxisCount];
            System.Span<int> waiting = stackalloc int[TrafficGeometry.AxisCount];
            System.Span<float> waitSum = stackalloc float[TrafficGeometry.AxisCount];
            System.Span<float> waitMax = stackalloc float[TrafficGeometry.AxisCount];

            foreach (var vehicle in vehicles)
            {
                if (vehicle.HasPassedStopLine)
                {
                    continue;
                }

                int axis = (int)vehicle.Axis;
                approaching[axis]++;
                if (vehicle.IsWaiting)
                {
                    waiting[axis]++;
                    waitSum[axis] += vehicle.WaitTime;
                    waitMax[axis] = Mathf.Max(waitMax[axis], vehicle.WaitTime);
                }
            }

            int totalWaiting = 0;
            float maxWait = 0f;
            for (int axis = 0; axis < TrafficGeometry.AxisCount; axis++)
            {
                axisStats[axis] = new AxisStats
                {
                    ApproachingCount = approaching[axis],
                    WaitingCount = waiting[axis],
                    AverageWaitTime = waiting[axis] > 0 ? waitSum[axis] / waiting[axis] : 0f,
                    MaxWaitTime = waitMax[axis],
                };
                totalWaiting += waiting[axis];
                maxWait = Mathf.Max(maxWait, waitMax[axis]);
            }

            float waitingVehicleSeconds = totalWaiting * dt;
            pendingWaitingVehicleSeconds += waitingVehicleSeconds;
            EpisodeWaitingVehicleSeconds += waitingVehicleSeconds;

            if (maxWait > deadlockWaitSeconds)
            {
                DeadlockDetected = true;
            }

            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                if (spawnBlockedSeconds[a] > spillbackSeconds)
                {
                    DeadlockDetected = true;
                }
            }
        }

        void DetectCollisions()
        {
            insideIntersection.Clear();
            foreach (var vehicle in vehicles)
            {
                if (vehicle.IsInsideIntersection)
                {
                    insideIntersection.Add(vehicle);
                }
            }

            float thresholdSqr = collisionDistance * collisionDistance;
            for (int i = 0; i < insideIntersection.Count; i++)
            {
                for (int j = i + 1; j < insideIntersection.Count; j++)
                {
                    var a = insideIntersection[i];
                    var b = insideIntersection[j];
                    if (a.Approach == b.Approach)
                    {
                        continue; // 同一アプローチは車間制御で分離されている
                    }

                    if ((a.transform.localPosition - b.transform.localPosition).sqrMagnitude < thresholdSqr)
                    {
                        if (!CollisionDetected)
                        {
                            Debug.LogWarning($"[{nameof(TrafficEnvironmentManager)}] 衝突検知: {a.name} × {b.name}", this);
                        }

                        CollisionDetected = true;
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // 経路ジオメトリ
        // ------------------------------------------------------------------

        void RebuildPaths()
        {
            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                for (int lane = 0; lane < TrafficGeometry.LanesPerApproach; lane++)
                {
                    paths[a, lane, (int)RouteType.Straight] = BuildPath((Approach)a, lane, RouteType.Straight);
                    paths[a, lane, (int)RouteType.CurbSideTurn] = lane == TrafficGeometry.OuterLane
                        ? BuildPath((Approach)a, lane, RouteType.CurbSideTurn)
                        : null;
                }
            }
        }

        VehiclePath BuildPath(Approach approach, int lane, RouteType route)
        {
            float sideSign = TrafficGeometry.SideSign(drivingSide);
            float half = IntersectionHalfSize;
            float laneOffset = sideSign * laneWidth * (lane + 0.5f);

            Vector3 direction = TrafficGeometry.TravelDirection(approach);
            Vector3 lateral = TrafficGeometry.RightOf(direction) * laneOffset;

            var points = new List<Vector3>
            {
                -direction * spawnDistance + lateral, // 0: スポーン地点
                -direction * half + lateral,          // 1: 交差点進入点
            };
            const int entryIndex = 1;

            Vector3 exitDirection;
            Vector3 exitLateral;
            if (route == RouteType.Straight)
            {
                exitDirection = direction;
                exitLateral = lateral;
                points.Add(direction * half + lateral);
            }
            else
            {
                // 歩道側への右左折: 右側通行なら右折、左側通行なら左折
                exitDirection = TrafficGeometry.RightOf(direction) * sideSign;
                exitLateral = TrafficGeometry.RightOf(exitDirection) * laneOffset;

                Vector3 entry = points[entryIndex];
                Vector3 exitEdge = exitDirection * half + exitLateral;
                Vector3 corner = entry + direction * Vector3.Dot(exitEdge - entry, direction);

                // 2 次ベジェ曲線で近似
                for (int k = 1; k <= curveSegments; k++)
                {
                    float t = k / (float)curveSegments;
                    float u = 1f - t;
                    points.Add(u * u * entry + 2f * u * t * corner + t * t * exitEdge);
                }
            }

            int exitIndex = points.Count - 1;
            points.Add(exitDirection * despawnDistance + exitLateral); // 破棄地点

            float stopLineDistance = spawnDistance - (half + stopLineSetback);
            return new VehiclePath(approach, lane, route, points.ToArray(), stopLineDistance, entryIndex, exitIndex);
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;

            float half = IntersectionHalfSize;
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(half * 2f, 0.01f, half * 2f));

            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                for (int lane = 0; lane < TrafficGeometry.LanesPerApproach; lane++)
                {
                    for (int r = 0; r < TrafficGeometry.RouteTypeCount; r++)
                    {
                        var route = (RouteType)r;
                        if (route == RouteType.CurbSideTurn && lane != TrafficGeometry.OuterLane)
                        {
                            continue;
                        }

                        var path = initialized ? paths[a, lane, r] : BuildPath((Approach)a, lane, route);
                        DrawPathGizmo(path);
                    }
                }
            }
        }

        void DrawPathGizmo(VehiclePath path)
        {
            if (path == null)
            {
                return;
            }

            Gizmos.color = path.Route == RouteType.Straight
                ? new Color(0.3f, 0.7f, 1f, 0.8f)
                : new Color(1f, 0.6f, 0.2f, 0.8f);
            for (int i = 1; i < path.PointCount; i++)
            {
                Gizmos.DrawLine(path.GetPoint(i - 1), path.GetPoint(i));
            }

            // 停止線（実行中は信号色で表示）
            path.Evaluate(path.StopLineDistance, out Vector3 stopPos, out Vector3 forward);
            Vector3 side = TrafficGeometry.RightOf(forward) * (laneWidth * 0.5f);
            Gizmos.color = Application.isPlaying && trafficLight != null
                ? trafficLight.GetDisplayColor(trafficLight.GetSignal(path.Approach))
                : Color.white;
            Gizmos.DrawLine(stopPos - side, stopPos + side);
        }
    }
}
