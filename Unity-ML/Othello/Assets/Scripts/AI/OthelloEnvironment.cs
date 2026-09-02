using System;
using System.Collections;
using UnityEngine;
using Othello.Core;
using Othello.View;

namespace Othello.AI
{
    /// <summary>
    /// オセロの強化学習環境コンポーネント
    /// 1つのエージェントが交互に手番を担当することで、通信途絶なく高速に自己対戦学習を実行します。
    /// </summary>
    public class OthelloEnvironment : MonoBehaviour
    {
        [Header("Agent")]
        [SerializeField] private OthelloAgent agent;

        [Header("Visualization (Optional)")]
        [SerializeField] private OthelloBoardView boardView;

        [Header("Settings")]
        [SerializeField] private bool autoStepInTraining = true;

        private OthelloBoard _board;
        private PieceType _currentTurn = PieceType.Black;
        private bool _isGameRunning = false;
        private int _stepCount = 0;
        private const int MaxGameSteps = 120; // 最大手数ガード

        public OthelloBoard Board => _board;
        public PieceType CurrentTurn => _currentTurn;
        public bool IsGameRunning => _isGameRunning;

        // イベント
        public event Action<OthelloBoard> OnBoardUpdated;
        public event Action<PieceType> OnTurnChanged;
        public event Action<PieceType> OnPassOccurred;
        public event Action<GameResult, int, int> OnGameOver;

        private void Awake()
        {
            _board = new OthelloBoard();

            if (agent == null)
            {
                agent = GetComponentInChildren<OthelloAgent>();
            }

            if (agent != null)
            {
                agent.InitializeAgent(this, _board);
            }
        }

        private void Start()
        {
            ResetGame();
        }

        /// <summary>
        /// ゲーム初期化
        /// </summary>
        public void ResetGame()
        {
            _board.Reset();
            _currentTurn = PieceType.Black;
            _isGameRunning = true;
            _stepCount = 0;

            if (agent != null)
            {
                agent.AssignedColor = _currentTurn;
            }

            if (boardView != null)
            {
                boardView.UpdateView(_board, _currentTurn);
            }

            OnBoardUpdated?.Invoke(_board);
            OnTurnChanged?.Invoke(_currentTurn);

            // 最初の決定要求
            RequestNextMove();
        }

        /// <summary>
        /// 次の手番の意思決定を要求
        /// </summary>
        public void RequestNextMove()
        {
            if (!_isGameRunning || agent == null) return;

            // パス判定
            if (!_board.HasAnyValidMove(_currentTurn))
            {
                PieceType opp = _currentTurn.Opponent();
                if (!_board.HasAnyValidMove(opp))
                {
                    // 両者パス -> ゲーム終了
                    EndGame();
                    return;
                }

                // パス発生
                OnPassOccurred?.Invoke(_currentTurn);
                _currentTurn = opp;
                OnTurnChanged?.Invoke(_currentTurn);

                if (boardView != null)
                {
                    boardView.UpdateView(_board, _currentTurn);
                }
            }

            agent.AssignedColor = _currentTurn;
            agent.RequestDecision();
        }

        /// <summary>
        /// エージェントからアクションを受け取ったときの処理
        /// </summary>
        public void OnAgentMove(OthelloAgent moveAgent, int squareIndex)
        {
            if (!_isGameRunning) return;

            _stepCount++;
            if (_stepCount > MaxGameSteps)
            {
                EndGame();
                return;
            }

            var flipped = _board.PlacePiece(squareIndex, _currentTurn);
            if (flipped == null)
            {
                // 合法手フォールバック
                var validMoves = _board.GetValidMoves(_currentTurn);
                if (validMoves.Count > 0)
                {
                    _board.PlacePiece(validMoves[0], _currentTurn);
                }
                else
                {
                    _currentTurn = _currentTurn.Opponent();
                    RequestNextMove();
                    return;
                }
            }

            if (boardView != null)
            {
                boardView.UpdateView(_board, _currentTurn);
            }
            OnBoardUpdated?.Invoke(_board);

            // 終局判定
            if (_board.IsGameOver())
            {
                EndGame();
                return;
            }

            // 手番交代
            _currentTurn = _currentTurn.Opponent();
            OnTurnChanged?.Invoke(_currentTurn);

            if (boardView != null)
            {
                boardView.UpdateView(_board, _currentTurn);
            }

            // 次のターンへ
            RequestNextMove();
        }

        private void EndGame()
        {
            if (!_isGameRunning) return;
            _isGameRunning = false;

            var result = _board.GetGameResult();
            var (blackCount, whiteCount, _) = _board.GetPieceCounts();

            PieceType winner = PieceType.Empty;
            if (result == GameResult.BlackWin) winner = PieceType.Black;
            else if (result == GameResult.WhiteWin) winner = PieceType.White;

            if (agent != null)
            {
                agent.NotifyGameEnd(result, blackCount, whiteCount, winner);
            }

            OnGameOver?.Invoke(result, blackCount, whiteCount);

            if (autoStepInTraining)
            {
                StartCoroutine(RestartAfterFrame());
            }
        }

        private IEnumerator RestartAfterFrame()
        {
            yield return null;
            ResetGame();
        }
    }
}
