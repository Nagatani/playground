// =====================================================================================
// AdaptiveTrafficSceneBuilder.cs (Editor only)
// メニュー:
//   Tools > Adaptive Traffic > Build Training Scene (1 Area)   … 動作確認・推論用
//   Tools > Adaptive Traffic > Build Training Scene (4 Areas)  … 並列学習用
//
// 以下を自動生成する（既存のものは上書き）:
//   Assets/AdaptiveTraffic/Materials/*.mat        道路・区画線・車両・信号灯の単色マテリアル
//   Assets/AdaptiveTraffic/Prefabs/Vehicle.prefab Cube + SimpleVehicle（Collider なし）
//   Assets/AdaptiveTraffic/Prefabs/TrafficArea.prefab
//       TrafficArea (TrafficEnvironmentManager)
//        ├─ Road        見た目のみ（道路・中央線・車線境界線・停止線）
//        ├─ TrafficLight (TrafficLightController) + 4 方向の灯器
//        └─ SignalAgent  (TrafficLightAgent + Behavior Parameters + Decision Requester)
//   Assets/AdaptiveTraffic/Scenes/TrafficTraining.unity（Build Settings にも登録）
//
// 道路の見た目は TrafficEnvironmentManager の既定値（車線幅・停止線位置・通行区分）から計算する。
// マネージャーのジオメトリ設定を変えた場合は、このメニューで再生成すること。
// =====================================================================================
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveTraffic.EditorTools
{
    public static class AdaptiveTrafficSceneBuilder
    {
        const string Root = "Assets/AdaptiveTraffic";
        const string MaterialsFolder = Root + "/Materials";
        const string PrefabsFolder = Root + "/Prefabs";
        const string ScenesFolder = Root + "/Scenes";
        const string VehiclePrefabPath = PrefabsFolder + "/Vehicle.prefab";
        const string AreaPrefabPath = PrefabsFolder + "/TrafficArea.prefab";
        const string ScenePath = ScenesFolder + "/TrafficTraining.unity";

        // Behavior Parameters / Decision Requester / Agent の設定値
        const string BehaviorName = "TrafficLight";
        const int DecisionPeriod = 20; // Fixed Timestep 0.05 → 1 秒
        const int MaxStep = 12000;     // Fixed Timestep 0.05 → 600 秒

        /// <summary>並列学習時のエリア間隔 [m]（スポーン距離 80m の倍以上）。</summary>
        const float AreaSpacing = 250f;

        /// <summary>道路を描く長さ（交差点中心から）[m]。スポーン地点より少し先まで。</summary>
        const float RoadMargin = 5f;

        [MenuItem("Tools/Adaptive Traffic/Build Training Scene (1 Area)")]
        static void BuildSingleArea() => BuildScene(1);

        [MenuItem("Tools/Adaptive Traffic/Build Training Scene (4 Areas)")]
        static void BuildFourAreas() => BuildScene(4);

        [MenuItem("Tools/Adaptive Traffic/Build Training Scene (1 Area)", true)]
        [MenuItem("Tools/Adaptive Traffic/Build Training Scene (4 Areas)", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        public static void BuildScene(int areaCount)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Adaptive Traffic",
                    $"{ScenePath} と TrafficArea / Vehicle プレハブを作り直します。よろしいですか？", "作り直す", "キャンセル"))
            {
                return;
            }

            if (!Mathf.Approximately(Time.fixedDeltaTime, AdaptiveTrafficProjectSetup.FixedTimestep))
            {
                Debug.LogWarning($"[AdaptiveTraffic] Fixed Timestep が {Time.fixedDeltaTime} です。" +
                    "Tools > Adaptive Traffic > Apply Project Settings で 0.05 にしてください（Decision Period / Max Step は 0.05 前提）。");
            }

            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Scenes");

            var materials = new MaterialSet();
            var vehiclePrefab = BuildVehiclePrefab(materials);
            var areaPrefab = BuildAreaPrefab(vehiclePrefab, materials);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            int columns = Mathf.CeilToInt(Mathf.Sqrt(areaCount));
            for (int i = 0; i < areaCount; i++)
            {
                var area = (GameObject)PrefabUtility.InstantiatePrefab(areaPrefab, scene);
                area.name = areaCount == 1 ? "TrafficArea" : $"TrafficArea_{i}";
                area.transform.position = new Vector3(i % columns * AreaSpacing, 0f, i / columns * AreaSpacing);
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 75f, -55f);
                camera.transform.LookAt(Vector3.zero);
                camera.farClipPlane = 2000f;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AdaptiveTraffic] {ScenePath} を生成しました（エリア数 {areaCount}）。" +
                "動作確認: SignalAgent の Behavior Type を Heuristic Only にして Play。");
        }

        // ------------------------------------------------------------------
        // Vehicle プレハブ
        // ------------------------------------------------------------------

        static GameObject BuildVehiclePrefab(MaterialSet materials)
        {
            var vehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vehicle.name = "Vehicle";
            Object.DestroyImmediate(vehicle.GetComponent<Collider>());
            vehicle.transform.localScale = new Vector3(1.8f, 1.4f, 4.5f);

            var renderer = vehicle.GetComponent<Renderer>();
            renderer.sharedMaterial = materials.Vehicle;

            var simpleVehicle = vehicle.AddComponent<SimpleVehicle>();
            var so = new SerializedObject(simpleVehicle);
            so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
            so.FindProperty("length").floatValue = vehicle.transform.localScale.z;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(vehicle, VehiclePrefabPath);
            Object.DestroyImmediate(vehicle);
            return prefab;
        }

        // ------------------------------------------------------------------
        // TrafficArea プレハブ
        // ------------------------------------------------------------------

        static GameObject BuildAreaPrefab(GameObject vehiclePrefab, MaterialSet materials)
        {
            var area = new GameObject("TrafficArea");
            var manager = area.AddComponent<TrafficEnvironmentManager>();

            var trafficLightGo = new GameObject("TrafficLight");
            trafficLightGo.transform.SetParent(area.transform, false);
            var trafficLight = trafficLightGo.AddComponent<TrafficLightController>();

            // マネージャー設定（参照）と、見た目の計算に使うジオメトリ値の取得
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("trafficLight").objectReferenceValue = trafficLight;
            managerSo.FindProperty("vehiclePrefab").objectReferenceValue = vehiclePrefab.GetComponent<SimpleVehicle>();
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            var geometry = new Geometry
            {
                LaneWidth = managerSo.FindProperty("laneWidth").floatValue,
                StopLineSetback = managerSo.FindProperty("stopLineSetback").floatValue,
                RoadLength = managerSo.FindProperty("spawnDistance").floatValue + RoadMargin,
                SideSign = TrafficGeometry.SideSign((DrivingSide)managerSo.FindProperty("drivingSide").enumValueIndex),
            };

            BuildRoad(area.transform, geometry, materials);
            BuildLamps(trafficLight, geometry, materials);
            BuildAgent(area.transform, manager, trafficLight);

            var prefab = PrefabUtility.SaveAsPrefabAsset(area, AreaPrefabPath);
            Object.DestroyImmediate(area);
            return prefab;
        }

        static void BuildAgent(Transform parent, TrafficEnvironmentManager manager, TrafficLightController trafficLight)
        {
            var agentGo = new GameObject("SignalAgent");
            agentGo.transform.SetParent(parent, false);

            // RequireComponent により Behavior Parameters / Decision Requester も追加される
            var agent = agentGo.AddComponent<TrafficLightAgent>();
            agent.MaxStep = MaxStep;

            var agentSo = new SerializedObject(agent);
            agentSo.FindProperty("environment").objectReferenceValue = manager;
            agentSo.FindProperty("trafficLight").objectReferenceValue = trafficLight;
            agentSo.ApplyModifiedPropertiesWithoutUndo();

            var behavior = agentGo.GetComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.BehaviorType = BehaviorType.Default;
            behavior.UseChildSensors = false;
            behavior.BrainParameters.VectorObservationSize = TrafficLightAgent.ObservationSize;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(2);

            var requester = agentGo.GetComponent<DecisionRequester>();
            requester.DecisionPeriod = DecisionPeriod;
            requester.DecisionStep = 0;
            requester.TakeActionsBetweenDecisions = true;
        }

        // ------------------------------------------------------------------
        // 見た目（道路・区画線・灯器）
        // ------------------------------------------------------------------

        struct Geometry
        {
            public float LaneWidth;
            public float StopLineSetback;
            public float RoadLength;
            public float SideSign;

            public float HalfSize => LaneWidth * TrafficGeometry.LanesPerApproach;
        }

        static void BuildRoad(Transform parent, Geometry g, MaterialSet materials)
        {
            var road = new GameObject("Road").transform;
            road.SetParent(parent, false);

            float roadWidth = g.HalfSize * 2f;
            float fullLength = g.RoadLength * 2f;
            CreateBox("Road_EW", road, new Vector3(0f, -0.05f, 0f), new Vector3(fullLength, 0.1f, roadWidth), materials.Road);
            CreateBox("Road_NS", road, new Vector3(0f, -0.051f, 0f), new Vector3(roadWidth, 0.1f, fullLength), materials.Road);

            const float markingY = 0.005f;
            const float markingThickness = 0.01f;
            float armLength = g.RoadLength - g.HalfSize;

            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                var approach = (Approach)a;
                Vector3 d = TrafficGeometry.TravelDirection(approach);  // 交差点へ向かう方向
                Vector3 right = TrafficGeometry.RightOf(d);
                Vector3 inboundSide = right * g.SideSign;                // 進入車線側
                bool alongZ = Mathf.Abs(d.z) > 0.5f;

                // このアームの中心（交差点の外側）
                Vector3 armCenter = -d * (g.HalfSize + armLength * 0.5f);

                // 中央線 1 本 + 進入側・退出側の車線境界線 各 1 本
                CreateBox($"CenterLine_{approach}", road, armCenter + Vector3.up * markingY,
                    StripScale(alongZ, armLength, 0.2f, markingThickness), materials.CenterLine);
                CreateBox($"LaneLine_{approach}_In", road, armCenter + inboundSide * g.LaneWidth + Vector3.up * markingY,
                    StripScale(alongZ, armLength, 0.12f, markingThickness), materials.Marking);
                CreateBox($"LaneLine_{approach}_Out", road, armCenter - inboundSide * g.LaneWidth + Vector3.up * markingY,
                    StripScale(alongZ, armLength, 0.12f, markingThickness), materials.Marking);

                // 停止線: 進入側 2 車線ぶん
                Vector3 stopLineCenter = -d * (g.HalfSize + g.StopLineSetback) + inboundSide * g.LaneWidth;
                CreateBox($"StopLine_{approach}", road, stopLineCenter + Vector3.up * (markingY * 2f),
                    StripScale(!alongZ, g.LaneWidth * 2f, 0.4f, markingThickness), materials.Marking);
            }
        }

        static void BuildLamps(TrafficLightController trafficLight, Geometry g, MaterialSet materials)
        {
            var eastWest = new List<Renderer>();
            var northSouth = new List<Renderer>();

            for (int a = 0; a < TrafficGeometry.ApproachCount; a++)
            {
                var approach = (Approach)a;
                Vector3 d = TrafficGeometry.TravelDirection(approach);
                Vector3 curbSide = TrafficGeometry.RightOf(d) * g.SideSign;
                Vector3 basePosition = -d * (g.HalfSize + g.StopLineSetback + 0.5f) + curbSide * (g.HalfSize + 1f);

                var holder = new GameObject($"Lamp_{approach}").transform;
                holder.SetParent(trafficLight.transform, false);
                holder.localPosition = basePosition;

                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.transform.SetParent(holder, false);
                pole.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                pole.transform.localScale = new Vector3(0.2f, 1.6f, 0.2f);
                pole.GetComponent<Renderer>().sharedMaterial = materials.Pole;

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                Object.DestroyImmediate(head.GetComponent<Collider>());
                head.transform.SetParent(holder, false);
                head.transform.localPosition = new Vector3(0f, 3.6f, 0f);
                head.transform.localScale = Vector3.one * 0.9f;
                var headRenderer = head.GetComponent<Renderer>();
                headRenderer.sharedMaterial = materials.Lamp;

                (TrafficGeometry.AxisOf(approach) == TrafficAxis.EastWest ? eastWest : northSouth).Add(headRenderer);
            }

            var so = new SerializedObject(trafficLight);
            AssignRenderers(so.FindProperty("eastWestLamps"), eastWest);
            AssignRenderers(so.FindProperty("northSouthLamps"), northSouth);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignRenderers(SerializedProperty array, List<Renderer> renderers)
        {
            array.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }
        }

        static Vector3 StripScale(bool alongZ, float length, float width, float thickness) =>
            alongZ ? new Vector3(width, thickness, length) : new Vector3(length, thickness, width);

        static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        // ------------------------------------------------------------------
        // マテリアル・アセット補助
        // ------------------------------------------------------------------

        sealed class MaterialSet
        {
            public readonly Material Road = GetOrCreateMaterial("Road", new Color(0.22f, 0.22f, 0.24f));
            public readonly Material Marking = GetOrCreateMaterial("Marking", new Color(0.95f, 0.95f, 0.95f));
            public readonly Material CenterLine = GetOrCreateMaterial("CenterLine", new Color(1f, 0.75f, 0.1f));
            public readonly Material Vehicle = GetOrCreateMaterial("Vehicle", new Color(0.25f, 0.5f, 1f));
            public readonly Material Lamp = GetOrCreateMaterial("Lamp", new Color(0.3f, 0.3f, 0.3f));
            public readonly Material Pole = GetOrCreateMaterial("Pole", new Color(0.45f, 0.45f, 0.45f));
        }

        static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(DefaultShader());
                AssetDatabase.CreateAsset(material, path);
            }

            // material.color は [MainColor]（URP: _BaseColor / Built-in: _Color）に書き込まれる
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader DefaultShader()
        {
            // URP/HDRP 導入済みならそのパイプラインの既定シェーダー、未導入なら Built-in Standard
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null)
            {
                return pipeline.defaultMaterial.shader;
            }

            return Shader.Find("Standard");
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
            {
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
