using System;
using UnityEngine;
using UnityEngine.UI;
using Othello.Core;
using Othello.Game;

namespace Othello.View
{
    /// <summary>
    /// オセロのスコア・手番・勝敗・コントロールUIを管理（Legacy UI Text対応で確実に表示）
    /// </summary>
    public class OthelloUIManager : MonoBehaviour
    {
        [Header("Score Displays")]
        [SerializeField] private Text blackScoreText;
        [SerializeField] private Text whiteScoreText;

        [Header("Turn / Status Displays")]
        [SerializeField] private Text turnText;
        [SerializeField] private Text statusMessageText;

        [Header("Control Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button humanVsAIButton;
        [SerializeField] private Button humanVsHumanButton;
        [SerializeField] private Button aiVsAIButton;

        [Header("Button Backgrounds for Active State")]
        [SerializeField] private Image humanVsAIImage;
        [SerializeField] private Image humanVsHumanImage;
        [SerializeField] private Image aiVsAIImage;

        private readonly Color activeModeColor = new Color(0.18f, 0.55f, 0.90f); // 選択中: 鮮やかな青
        private readonly Color inactiveModeColor = new Color(0.28f, 0.30f, 0.35f); // 非選択: ダークグレー

        public event Action OnRestartClicked;
        public event Action<GameMode> OnGameModeChanged;

        private void Awake()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            }

            if (humanVsAIButton != null)
            {
                humanVsAIButton.onClick.AddListener(() =>
                {
                    SetModeHighlight(GameMode.HumanVsAI);
                    OnGameModeChanged?.Invoke(GameMode.HumanVsAI);
                });
            }

            if (humanVsHumanButton != null)
            {
                humanVsHumanButton.onClick.AddListener(() =>
                {
                    SetModeHighlight(GameMode.HumanVsHuman);
                    OnGameModeChanged?.Invoke(GameMode.HumanVsHuman);
                });
            }

            if (aiVsAIButton != null)
            {
                aiVsAIButton.onClick.AddListener(() =>
                {
                    SetModeHighlight(GameMode.AIVsAI);
                    OnGameModeChanged?.Invoke(GameMode.AIVsAI);
                });
            }

            SetModeHighlight(GameMode.HumanVsAI);
        }

        public void SetModeHighlight(GameMode mode)
        {
            if (humanVsAIImage != null) humanVsAIImage.color = (mode == GameMode.HumanVsAI) ? activeModeColor : inactiveModeColor;
            if (humanVsHumanImage != null) humanVsHumanImage.color = (mode == GameMode.HumanVsHuman) ? activeModeColor : inactiveModeColor;
            if (aiVsAIImage != null) aiVsAIImage.color = (mode == GameMode.AIVsAI) ? activeModeColor : inactiveModeColor;
        }

        public void UpdateScores(int blackCount, int whiteCount)
        {
            if (blackScoreText != null)
            {
                blackScoreText.text = $"● 黒: {blackCount}";
            }

            if (whiteScoreText != null)
            {
                whiteScoreText.text = $"○ 白: {whiteCount}";
            }
        }

        public void UpdateTurn(PieceType currentTurn, string playerRoleName = "")
        {
            if (turnText != null)
            {
                string colorName = currentTurn == PieceType.Black ? "黒 (先手)" : "白 (後手)";
                string role = string.IsNullOrEmpty(playerRoleName) ? "" : $" [{playerRoleName}]";
                turnText.text = $"手番: {colorName}{role}";
            }
        }

        public void ShowStatusMessage(string message)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
            }
        }

        public void ShowPassMessage(PieceType passedPlayer)
        {
            string colorName = passedPlayer == PieceType.Black ? "黒" : "白";
            ShowStatusMessage($"{colorName}は打てる場所が無いためパスしました。");
        }

        public void ShowGameOver(GameResult result, int blackCount, int whiteCount)
        {
            string msg = result switch
            {
                GameResult.BlackWin => $"【ゲーム終了】 黒の勝利！ ({blackCount} vs {whiteCount})",
                GameResult.WhiteWin => $"【ゲーム終了】 白の勝利！ ({whiteCount} vs {blackCount})",
                GameResult.Draw => $"【ゲーム終了】 引き分け！ ({blackCount} vs {whiteCount})",
                _ => "ゲーム終了"
            };

            ShowStatusMessage(msg);
        }
    }
}
