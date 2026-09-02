using System.Collections;
using UnityEngine;
using Othello.Core;
using Othello.AI;
using Othello.View;

namespace Othello.Game
{
    public enum GameMode
    {
        HumanVsAI = 0,
        HumanVsHuman = 1,
        AIVsAI = 2
    }

    /// <summary>
    /// オセロゲームの進行・プレイヤー入力・AI思考の統合マネージャー
    /// </summary>
    public class OthelloGameManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private OthelloBoardView boardView;
        [SerializeField] private OthelloUIManager uiManager;
        [SerializeField] private OthelloAgent blackAgent;
        [SerializeField] private OthelloAgent whiteAgent;

        [Header("Game Configuration")]
        [SerializeField] private GameMode initialGameMode = GameMode.HumanVsAI;
        [SerializeField] private PieceType humanPlayerColor = PieceType.Black;
        [SerializeField] private float aiMoveDelay = 0.5f; // AI着手前の演出ディレイ（秒）

        private OthelloBoard _board;
        private PieceType _currentTurn = PieceType.Black;
        private GameMode _currentGameMode;
        private bool _isGameActive = false;
        private bool _isAITurnProcessing = false;

        public OthelloBoard Board => _board;
        public PieceType CurrentTurn => _currentTurn;
        public GameMode CurrentGameMode => _currentGameMode;

        private void Awake()
        {
            _board = new OthelloBoard();
            _currentGameMode = initialGameMode;
        }

        private void Start()
        {
            if (boardView != null)
            {
                boardView.OnTileClicked += HandleTileClicked;
            }

            if (uiManager != null)
            {
                uiManager.OnRestartClicked += StartNewGame;
                uiManager.OnGameModeChanged += HandleGameModeChanged;
            }

            // エージェントの初期化
            if (blackAgent != null)
            {
                blackAgent.AssignedColor = PieceType.Black;
                blackAgent.InitializeAgent(this, _board);
            }

            if (whiteAgent != null)
            {
                whiteAgent.AssignedColor = PieceType.White;
                whiteAgent.InitializeAgent(this, _board);
            }

            StartNewGame();
        }

        private void OnDestroy()
        {
            if (boardView != null)
            {
                boardView.OnTileClicked -= HandleTileClicked;
            }

            if (uiManager != null)
            {
                uiManager.OnRestartClicked -= StartNewGame;
                uiManager.OnGameModeChanged -= HandleGameModeChanged;
            }
        }

        public void StartNewGame()
        {
            StopAllCoroutines();
            _isAITurnProcessing = false;
            _board.Reset();
            _currentTurn = PieceType.Black;
            _isGameActive = true;

            UpdateUIAndBoard();

            if (uiManager != null)
            {
                uiManager.ShowStatusMessage("ゲーム開始！");
            }

            CheckTurnAndProceed();
        }

        private void HandleGameModeChanged(GameMode mode)
        {
            _currentGameMode = mode;
            StartNewGame();
        }

        /// <summary>
        /// 現在の手番が人間かAIかを判定
        /// </summary>
        public bool IsCurrentPlayerHuman()
        {
            return _currentGameMode switch
            {
                GameMode.HumanVsHuman => true,
                GameMode.HumanVsAI => _currentTurn == humanPlayerColor,
                GameMode.AIVsAI => false,
                _ => true
            };
        }

        /// <summary>
        /// 人間プレイヤーが盤面をクリックした際の処理
        /// </summary>
        private void HandleTileClicked(int squareIndex)
        {
            if (!_isGameActive || !IsCurrentPlayerHuman() || _isAITurnProcessing) return;

            if (_board.IsValidMove(squareIndex, _currentTurn))
            {
                ExecuteMove(squareIndex);
            }
        }

        /// <summary>
        /// 石を置いて手番を進める
        /// </summary>
        private void ExecuteMove(int squareIndex)
        {
            var flipped = _board.PlacePiece(squareIndex, _currentTurn);
            if (flipped == null) return;

            UpdateUIAndBoard();

            // ゲーム終了チェック
            if (_board.IsGameOver())
            {
                EndGame();
                return;
            }

            // 手番交代
            _currentTurn = _currentTurn.Opponent();

            // パス判定
            if (!_board.HasAnyValidMove(_currentTurn))
            {
                PieceType opp = _currentTurn.Opponent();
                if (!_board.HasAnyValidMove(opp))
                {
                    EndGame();
                    return;
                }

                if (uiManager != null)
                {
                    uiManager.ShowPassMessage(_currentTurn);
                }

                // 手番を戻す
                _currentTurn = opp;
            }

            UpdateUIAndBoard();
            CheckTurnAndProceed();
        }

        private void CheckTurnAndProceed()
        {
            if (!_isGameActive) return;

            if (!IsCurrentPlayerHuman())
            {
                StartCoroutine(ProcessAITurnCoroutine());
            }
        }

        private IEnumerator ProcessAITurnCoroutine()
        {
            _isAITurnProcessing = true;

            if (aiMoveDelay > 0f)
            {
                yield return new WaitForSeconds(aiMoveDelay);
            }

            OthelloAgent activeAgent = _currentTurn == PieceType.Black ? blackAgent : whiteAgent;

            if (activeAgent != null)
            {
                // エージェントに意思決定を要求
                activeAgent.RequestDecision();
            }
            else
            {
                // エージェントがアタッチされていない場合のフォールバック（ランダム手）
                var validMoves = _board.GetValidMoves(_currentTurn);
                if (validMoves.Count > 0)
                {
                    int chosen = validMoves[Random.Range(0, validMoves.Count)];
                    ExecuteMove(chosen);
                }
            }

            _isAITurnProcessing = false;
        }

        /// <summary>
        /// AIエージェントから着手を受け取る（OthelloAgentからのコールバック用）
        /// </summary>
        public void OnAgentMakeMove(OthelloAgent agent, int squareIndex)
        {
            if (!_isGameActive) return;
            if (agent.AssignedColor != _currentTurn) return;

            ExecuteMove(squareIndex);
        }

        private void UpdateUIAndBoard()
        {
            var (blackCount, whiteCount, _) = _board.GetPieceCounts();

            if (uiManager != null)
            {
                uiManager.UpdateScores(blackCount, whiteCount);
                string roleName = IsCurrentPlayerHuman() ? "プレイヤー" : "AI";
                uiManager.UpdateTurn(_currentTurn, roleName);
            }

            if (boardView != null)
            {
                boardView.UpdateView(_board, _currentTurn, IsCurrentPlayerHuman());
            }
        }

        private void EndGame()
        {
            _isGameActive = false;
            var result = _board.GetGameResult();
            var (blackCount, whiteCount, _) = _board.GetPieceCounts();

            if (uiManager != null)
            {
                uiManager.ShowGameOver(result, blackCount, whiteCount);
            }

            if (boardView != null)
            {
                boardView.UpdateView(_board, _currentTurn, false);
            }
        }
    }
}
