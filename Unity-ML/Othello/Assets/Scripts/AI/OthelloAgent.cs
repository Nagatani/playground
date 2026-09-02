using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Othello.Core;

namespace Othello.AI
{
    /// <summary>
    /// オセロのML-Agents強化学習エージェント
    /// 手番プレイヤーの視点（自分=+1, 相手=-1, 空=0）で観測・行動し、1つのポリシーで自己対戦学習を完結させます。
    /// </summary>
    public class OthelloAgent : Agent
    {
        [Header("Agent Settings")]
        [SerializeField] private PieceType assignedColor = PieceType.Black;
        [SerializeField] private bool useScoreShapingReward = true;

        private OthelloEnvironment _environment;
        private Game.OthelloGameManager _gameManager;
        private OthelloBoard _board;

        public PieceType AssignedColor
        {
            get => assignedColor;
            set => assignedColor = value;
        }

        public void InitializeAgent(OthelloEnvironment environment, OthelloBoard board)
        {
            _environment = environment;
            _board = board;
        }

        public void InitializeAgent(Game.OthelloGameManager gameManager, OthelloBoard board)
        {
            _gameManager = gameManager;
            _board = board;
        }

        /// <summary>
        /// 観測データの収集 (Observation)
        /// 現在の手番のプレイヤー視点に正規化 (+1: 自分/手番, -1: 相手, 0: 空) して観測。
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            if (_board == null)
            {
                for (int i = 0; i < OthelloBoard.TotalSquares; i++)
                {
                    sensor.AddObservation(0f);
                }
                return;
            }

            // 環境から現在の手番を取得（通常対戦時はassignedColor）
            PieceType currentTurn = (_environment != null) ? _environment.CurrentTurn : assignedColor;
            PieceType oppColor = currentTurn.Opponent();

            for (int i = 0; i < OthelloBoard.TotalSquares; i++)
            {
                PieceType piece = _board.GetPiece(i);
                if (piece == currentTurn)
                {
                    sensor.AddObservation(1.0f);
                }
                else if (piece == oppColor)
                {
                    sensor.AddObservation(-1.0f);
                }
                else
                {
                    sensor.AddObservation(0.0f);
                }
            }
        }

        /// <summary>
        /// アクションマスキング (Action Masking)
        /// 現在の手番の合法手のみを選択可能にし、非合法手はマスクする。
        /// </summary>
        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (_board == null) return;

            PieceType currentTurn = (_environment != null) ? _environment.CurrentTurn : assignedColor;
            var validMoves = _board.GetValidMoves(currentTurn);
            var validMoveSet = new HashSet<int>(validMoves);

            for (int i = 0; i < OthelloBoard.TotalSquares; i++)
            {
                if (!validMoveSet.Contains(i))
                {
                    actionMask.SetActionEnabled(0, i, false);
                }
            }
        }

        /// <summary>
        /// 決定された行動の実行
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            int squareIndex = actions.DiscreteActions[0];

            if (_environment != null)
            {
                _environment.OnAgentMove(this, squareIndex);
            }
            else if (_gameManager != null)
            {
                _gameManager.OnAgentMakeMove(this, squareIndex);
            }
        }

        /// <summary>
        /// 手動テスト・ヒューリスティック動作（四隅優先＋ランダム）
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;
            if (_board == null)
            {
                discreteActions[0] = 0;
                return;
            }

            PieceType currentTurn = (_environment != null) ? _environment.CurrentTurn : assignedColor;
            var validMoves = _board.GetValidMoves(currentTurn);
            if (validMoves.Count == 0)
            {
                discreteActions[0] = 0;
                return;
            }

            // 四隅 (0, 7, 56, 63) を優先
            int[] corners = { 0, 7, 56, 63 };
            foreach (int c in corners)
            {
                if (validMoves.Contains(c))
                {
                    discreteActions[0] = c;
                    return;
                }
            }

            // ランダムに合法手を選択
            int randomIndex = Random.Range(0, validMoves.Count);
            discreteActions[0] = validMoves[randomIndex];
        }

        /// <summary>
        /// ゲーム終了時の報酬設定とエピソード完了
        /// </summary>
        public void NotifyGameEnd(GameResult result, int blackCount, int whiteCount, PieceType winnerColor)
        {
            float reward = 0f;

            if (result == GameResult.Draw)
            {
                reward = 0f;
            }
            else if (winnerColor != PieceType.Empty)
            {
                // 勝者には+1.0、敗者には-1.0
                reward = 1.0f;

                if (useScoreShapingReward)
                {
                    int diff = Mathf.Abs(blackCount - whiteCount);
                    reward += (float)diff / OthelloBoard.TotalSquares * 0.1f;
                }
            }

            AddReward(reward);
            EndEpisode();
        }
    }
}
