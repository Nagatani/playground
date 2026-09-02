using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Unity.MLAgents.Policies;
using Othello.Core;
using Othello.AI;
using Othello.View;
using Othello.Game;

namespace Othello.Editor
{
    public static class OthelloSceneSetup
    {
        private static Font _cachedFont = null;

        /// <summary>
        /// 日本語文字が確実に描画できるシステムフォントまたはビルトインフォントを取得
        /// </summary>
        private static Font GetSystemFont()
        {
            if (_cachedFont != null) return _cachedFont;

            string[] osFonts = { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Arial" };
            _cachedFont = Font.CreateDynamicFontFromOSFont(osFonts, 28);

            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return _cachedFont;
        }

        [MenuItem("Othello/1. Setup Playable Game Scene", false, 10)]
        public static void SetupPlayableGameScene()
        {
            EnsureEventSystem();

            // メインカメラの確認
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                mainCam = camObj.GetComponent<Camera>();
                mainCam.tag = "MainCamera";
                mainCam.orthographic = true;
                mainCam.transform.position = new Vector3(0, 0, -10);
            }
            mainCam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            // 既存のオブジェクトを整理
            var oldCanvas = GameObject.Find("OthelloCanvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);
            var oldGM = GameObject.Find("GameManager");
            if (oldGM != null) Object.DestroyImmediate(oldGM);
            var oldAgents = GameObject.Find("AI_Agents");
            if (oldAgents != null) Object.DestroyImmediate(oldAgents);

            // Canvas作成
            var canvasObj = new GameObject("OthelloCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ルートパネル
            var rootPanel = CreateUIObject("RootPanel", canvasObj.transform);
            var rootRect = rootPanel.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // ==========================================
            // 1. 上部ヘッダー (スコア・手番・状態)
            // ==========================================
            var headerPanel = CreateUIObject("HeaderPanel", rootPanel.transform);
            var headerRect = headerPanel.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.05f, 0.82f);
            headerRect.anchorMax = new Vector2(0.95f, 0.98f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            // タイトル
            var titleText = CreateText("TitleText", headerPanel.transform, "2D OTHELLO (ML-AGENTS)", 38, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f));
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.1f, 0.60f);
            titleRect.anchorMax = new Vector2(0.9f, 1.0f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // スコア表示 (黒 / 白)
            var blackScoreText = CreateText("BlackScore", headerPanel.transform, "● 黒: 2", 30, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.9f, 0.9f, 0.9f));
            blackScoreText.rectTransform.anchorMin = new Vector2(0.20f, 0.22f);
            blackScoreText.rectTransform.anchorMax = new Vector2(0.45f, 0.60f);
            blackScoreText.rectTransform.offsetMin = Vector2.zero;
            blackScoreText.rectTransform.offsetMax = Vector2.zero;

            var whiteScoreText = CreateText("WhiteScore", headerPanel.transform, "○ 白: 2", 30, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.9f, 0.9f, 0.9f));
            whiteScoreText.rectTransform.anchorMin = new Vector2(0.55f, 0.22f);
            whiteScoreText.rectTransform.anchorMax = new Vector2(0.80f, 0.60f);
            whiteScoreText.rectTransform.offsetMin = Vector2.zero;
            whiteScoreText.rectTransform.offsetMax = Vector2.zero;

            // 手番表示
            var turnText = CreateText("TurnText", headerPanel.transform, "手番: 黒 (先手) [プレイヤー]", 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            turnText.rectTransform.anchorMin = new Vector2(0.1f, 0.22f);
            turnText.rectTransform.anchorMax = new Vector2(0.9f, 0.60f);
            turnText.rectTransform.offsetMin = Vector2.zero;
            turnText.rectTransform.offsetMax = Vector2.zero;

            // ステータス・メッセージ
            var statusText = CreateText("StatusText", headerPanel.transform, "ゲーム開始！", 24, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 0.85f));
            statusText.rectTransform.anchorMin = new Vector2(0.1f, 0.0f);
            statusText.rectTransform.anchorMax = new Vector2(0.9f, 0.25f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;

            // ==========================================
            // 2. 中央オセロ盤面 (正方形で中央に完璧フィット)
            // ==========================================
            var boardContainer = CreateUIObject("BoardContainer", rootPanel.transform);
            var boardContainerRect = boardContainer.GetComponent<RectTransform>();
            boardContainerRect.anchorMin = new Vector2(0.15f, 0.16f);
            boardContainerRect.anchorMax = new Vector2(0.85f, 0.80f);
            boardContainerRect.offsetMin = Vector2.zero;
            boardContainerRect.offsetMax = Vector2.zero;

            var boardObj = CreateUIObject("OthelloBoard", boardContainer.transform);
            var boardRect = boardObj.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.sizeDelta = new Vector2(600, 600);

            var aspectFitter = boardObj.AddComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = 1.0f;

            var boardImage = boardObj.AddComponent<Image>();
            boardImage.color = new Color(0.06f, 0.22f, 0.10f); // 濃いグリーン枠
            var boardView = boardObj.AddComponent<OthelloBoardView>();

            // ==========================================
            // 3. 下部フッター操作ボタン群 (4つの明確なボタン)
            // ==========================================
            var controlPanel = CreateUIObject("ControlPanel", rootPanel.transform);
            var controlRect = controlPanel.GetComponent<RectTransform>();
            controlRect.anchorMin = new Vector2(0.12f, 0.03f);
            controlRect.anchorMax = new Vector2(0.88f, 0.13f);
            controlRect.offsetMin = Vector2.zero;
            controlRect.offsetMax = Vector2.zero;

            // 1. [人 vs AI] ボタン
            var (btnHumanVsAI, imgHumanVsAI) = CreateColoredButton("Btn_HumanVsAI", controlPanel.transform, "人 vs AI", 24, new Color(0.18f, 0.55f, 0.90f));
            var r1 = btnHumanVsAI.GetComponent<RectTransform>();
            r1.anchorMin = new Vector2(0.00f, 0.1f);
            r1.anchorMax = new Vector2(0.22f, 0.9f);
            r1.offsetMin = Vector2.zero;
            r1.offsetMax = Vector2.zero;

            // 2. [人 vs 人] ボタン
            var (btnHumanVsHuman, imgHumanVsHuman) = CreateColoredButton("Btn_HumanVsHuman", controlPanel.transform, "人 vs 人", 24, new Color(0.28f, 0.30f, 0.35f));
            var r2 = btnHumanVsHuman.GetComponent<RectTransform>();
            r2.anchorMin = new Vector2(0.25f, 0.1f);
            r2.anchorMax = new Vector2(0.47f, 0.9f);
            r2.offsetMin = Vector2.zero;
            r2.offsetMax = Vector2.zero;

            // 3. [AI vs AI] ボタン
            var (btnAIVsAI, imgAIVsAI) = CreateColoredButton("Btn_AIVsAI", controlPanel.transform, "AI vs AI (観戦)", 24, new Color(0.28f, 0.30f, 0.35f));
            var r3 = btnAIVsAI.GetComponent<RectTransform>();
            r3.anchorMin = new Vector2(0.50f, 0.1f);
            r3.anchorMax = new Vector2(0.72f, 0.9f);
            r3.offsetMin = Vector2.zero;
            r3.offsetMax = Vector2.zero;

            // 4. [🔄 リスタート] ボタン
            var (btnRestart, _) = CreateColoredButton("Btn_Restart", controlPanel.transform, "🔄 リスタート", 26, new Color(0.85f, 0.45f, 0.15f)); // オレンジ色
            var r4 = btnRestart.GetComponent<RectTransform>();
            r4.anchorMin = new Vector2(0.76f, 0.1f);
            r4.anchorMax = new Vector2(1.00f, 0.9f);
            r4.offsetMin = Vector2.zero;
            r4.offsetMax = Vector2.zero;

            // ==========================================
            // 4. UIManager & GameManager & AIの配線
            // ==========================================
            var uiManager = canvasObj.AddComponent<OthelloUIManager>();
            SetSerializedField(uiManager, "blackScoreText", blackScoreText);
            SetSerializedField(uiManager, "whiteScoreText", whiteScoreText);
            SetSerializedField(uiManager, "turnText", turnText);
            SetSerializedField(uiManager, "statusMessageText", statusText);
            SetSerializedField(uiManager, "humanVsAIButton", btnHumanVsAI.GetComponent<Button>());
            SetSerializedField(uiManager, "humanVsHumanButton", btnHumanVsHuman.GetComponent<Button>());
            SetSerializedField(uiManager, "aiVsAIButton", btnAIVsAI.GetComponent<Button>());
            SetSerializedField(uiManager, "restartButton", btnRestart.GetComponent<Button>());
            SetSerializedField(uiManager, "humanVsAIImage", imgHumanVsAI);
            SetSerializedField(uiManager, "humanVsHumanImage", imgHumanVsHuman);
            SetSerializedField(uiManager, "aiVsAIImage", imgAIVsAI);

            // AIエージェント GameObject
            var aiContainer = new GameObject("AI_Agents");
            var blackAgentObj = new GameObject("Agent_Black");
            blackAgentObj.transform.SetParent(aiContainer.transform);
            var blackAgent = blackAgentObj.AddComponent<OthelloAgent>();
            SetupBehaviorParameters(blackAgentObj, "Othello", 0);

            var whiteAgentObj = new GameObject("Agent_White");
            whiteAgentObj.transform.SetParent(aiContainer.transform);
            var whiteAgent = whiteAgentObj.AddComponent<OthelloAgent>();
            SetupBehaviorParameters(whiteAgentObj, "Othello", 1);

            // GameManager
            var gmObj = new GameObject("GameManager");
            var gameManager = gmObj.AddComponent<OthelloGameManager>();
            SetSerializedField(gameManager, "boardView", boardView);
            SetSerializedField(gameManager, "uiManager", uiManager);
            SetSerializedField(gameManager, "blackAgent", blackAgent);
            SetSerializedField(gameManager, "whiteAgent", whiteAgent);

            // 盤面のグリッド初期生成を実行
            boardView.InitializeGridIfNeeded();

            EditorUtility.DisplayDialog("セットアップ完了", "オセロ対戦シーンの構築が完了しました！\n画面下部に「人 vs AI」「人 vs 人」「AI vs AI」「リスタート」の各ボタンがはっきりと表示されます。", "OK");
        }

        [MenuItem("Othello/2. Setup ML-Agents Training Scene (16 Boards)", false, 20)]
        public static void SetupTrainingScene()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                mainCam = camObj.GetComponent<Camera>();
                mainCam.tag = "MainCamera";
                mainCam.orthographic = true;
                mainCam.transform.position = new Vector3(0, 0, -10);
            }
            mainCam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);

            var oldRoot = GameObject.Find("TrainingEnvironments");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);

            var trainingRoot = new GameObject("TrainingEnvironments");

            int rows = 4;
            int cols = 4;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int index = r * cols + c;
                    var envObj = new GameObject($"OthelloEnv_{index}");
                    envObj.transform.SetParent(trainingRoot.transform);

                    var env = envObj.AddComponent<OthelloEnvironment>();

                    // 1つのエージェントが交互に手番を担当（手番視点観測による自己対戦）
                    var agentObj = new GameObject("OthelloAgent");
                    agentObj.transform.SetParent(envObj.transform);
                    var agent = agentObj.AddComponent<OthelloAgent>();
                    SetupBehaviorParameters(agentObj, "Othello", 0);

                    SetSerializedField(env, "agent", agent);
                    SetSerializedField(env, "autoStepInTraining", true);
                }
            }

            EditorUtility.DisplayDialog("セットアップ完了", "16面の並列強化学習用環境を構築しました！", "OK");
        }

        private static void SetupBehaviorParameters(GameObject obj, string behaviorName, int teamId)
        {
            var bp = obj.GetComponent<BehaviorParameters>();
            if (bp == null) bp = obj.AddComponent<BehaviorParameters>();

            bp.BehaviorName = behaviorName;
            bp.TeamId = teamId;
            bp.BrainParameters.VectorObservationSize = 64;
            bp.BrainParameters.NumStackedVectorObservations = 1;
            bp.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(64);
            bp.BehaviorType = BehaviorType.Default;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            var t = obj.GetComponent<Text>();
            t.font = GetSystemFont();
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static (GameObject buttonObj, Image buttonImage) CreateColoredButton(string name, Transform parent, string labelText, int fontSize, Color bgColor)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            var img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            var label = CreateText("Label", btnObj.transform, labelText, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            var lRect = label.rectTransform;
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero;

            return (btnObj, img);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
                if (System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") is System.Type inputModuleType)
                {
                    eventSystem.AddComponent(inputModuleType);
                }
                else
                {
                    eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
#else
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }
        }

        private static void SetSerializedField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                if (value is Object objValue)
                {
                    prop.objectReferenceValue = objValue;
                }
                so.ApplyModifiedProperties();
            }
        }
    }
}
