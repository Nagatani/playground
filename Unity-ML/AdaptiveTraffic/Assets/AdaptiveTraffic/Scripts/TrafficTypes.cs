// =====================================================================================
// TrafficTypes.cs
// 交差点シミュレーション全体で共有する列挙型・幾何ヘルパー・車両経路クラス。
// MonoBehaviour ではないため、どの GameObject にもアタッチ不要。
//
// 座標系（TrafficEnvironmentManager のローカル座標）:
//   - 交差点中心 = 原点、+Z = 北、+X = 東、Y = 上
//   - Approach は「車両がどの方角から交差点へ進入してくるか」を表す
//     (Approach.North の車両は南向き = -Z 方向へ走行)
// =====================================================================================
using UnityEngine;

namespace AdaptiveTraffic
{
    /// <summary>車両の進入方向（どの方角から交差点へ向かってくるか）。</summary>
    public enum Approach
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3,
    }

    /// <summary>信号制御上の方向軸。1 つの軸に属する 2 アプローチは同時に青になる。</summary>
    public enum TrafficAxis
    {
        EastWest = 0,
        NorthSouth = 1,
    }

    public enum SignalColor
    {
        Green,
        Yellow,
        Red,
    }

    /// <summary>通行区分。日本は Left（左側通行）。</summary>
    public enum DrivingSide
    {
        Left,
        Right,
    }

    /// <summary>
    /// 車両の進路。
    /// Step 1 では対向車線を横切らない「歩道側の右左折」（左側通行なら左折、右側通行なら右折）のみ対応。
    /// 対向直進と交差する右折（左側通行時）は、右折待ち/右折専用フェーズが必要になるため後続ステップで扱う。
    /// </summary>
    public enum RouteType
    {
        Straight = 0,
        CurbSideTurn = 1,
    }

    public static class TrafficGeometry
    {
        public const int ApproachCount = 4;
        public const int AxisCount = 2;
        public const int LanesPerApproach = 2;
        public const int RouteTypeCount = 2;

        /// <summary>中央線側の車線（直進専用）。</summary>
        public const int InnerLane = 0;

        /// <summary>歩道側の車線（直進 + 歩道側の右左折）。</summary>
        public const int OuterLane = 1;

        public static TrafficAxis AxisOf(Approach approach) =>
            approach is Approach.East or Approach.West ? TrafficAxis.EastWest : TrafficAxis.NorthSouth;

        public static TrafficAxis Opposite(TrafficAxis axis) =>
            axis == TrafficAxis.EastWest ? TrafficAxis.NorthSouth : TrafficAxis.EastWest;

        /// <summary>アプローチから交差点へ向かう走行方向（ローカル座標の単位ベクトル）。</summary>
        public static Vector3 TravelDirection(Approach approach) => approach switch
        {
            Approach.North => Vector3.back,
            Approach.South => Vector3.forward,
            Approach.East => Vector3.left,
            _ => Vector3.right,
        };

        /// <summary>水平面上で進行方向 d に対する右方向。</summary>
        public static Vector3 RightOf(Vector3 d) => new Vector3(d.z, 0f, -d.x);

        /// <summary>右側通行なら +1、左側通行なら -1（車線の横方向オフセットに掛ける）。</summary>
        public static float SideSign(DrivingSide side) => side == DrivingSide.Right ? 1f : -1f;
    }

    /// <summary>
    /// 車両が辿る折れ線経路。距離 s（始点からの道のり）で位置・向きを評価する。
    /// 同じ (Approach, Lane, RouteType) の車両は同一インスタンスを共有する。
    /// 停止線までの区間は全ルートで共通なので、同一レーン内の車間距離は s の差で計算できる。
    /// </summary>
    public sealed class VehiclePath
    {
        readonly Vector3[] points;
        readonly float[] cumulative;

        public Approach Approach { get; }
        public int Lane { get; }
        public RouteType Route { get; }

        /// <summary>経路全長 [m]。</summary>
        public float Length { get; }

        /// <summary>始点から停止線までの距離 [m]。</summary>
        public float StopLineDistance { get; }

        /// <summary>始点から交差点ボックス進入点までの距離 [m]。</summary>
        public float IntersectionEntryDistance { get; }

        /// <summary>始点から交差点ボックス退出点までの距離 [m]。</summary>
        public float IntersectionExitDistance { get; }

        public int PointCount => points.Length;

        public VehiclePath(Approach approach, int lane, RouteType route, Vector3[] points,
            float stopLineDistance, int entryPointIndex, int exitPointIndex)
        {
            Approach = approach;
            Lane = lane;
            Route = route;
            this.points = points;

            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }

            Length = cumulative[points.Length - 1];
            StopLineDistance = stopLineDistance;
            IntersectionEntryDistance = cumulative[entryPointIndex];
            IntersectionExitDistance = cumulative[exitPointIndex];
        }

        public Vector3 GetPoint(int index) => points[index];

        /// <summary>
        /// 道のり distance における位置と進行方向を返す。範囲外は始点/終点の延長線上で外挿する。
        /// </summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
        {
            int last = points.Length - 1;
            if (distance <= 0f)
            {
                forward = (points[1] - points[0]).normalized;
                position = points[0] + forward * distance;
                return;
            }

            if (distance >= Length)
            {
                forward = (points[last] - points[last - 1]).normalized;
                position = points[last] + forward * (distance - Length);
                return;
            }

            // 点数は十数個なので線形探索で十分
            int i = 1;
            while (cumulative[i] < distance)
            {
                i++;
            }

            float segment = cumulative[i] - cumulative[i - 1];
            float t = segment > 1e-5f ? (distance - cumulative[i - 1]) / segment : 1f;
            position = Vector3.LerpUnclamped(points[i - 1], points[i], t);
            forward = (points[i] - points[i - 1]).normalized;
        }
    }
}
