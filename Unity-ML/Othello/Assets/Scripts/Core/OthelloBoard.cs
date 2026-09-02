using System;
using System.Collections.Generic;

namespace Othello.Core
{
    /// <summary>
    /// オセロのゲーム結果
    /// </summary>
    public enum GameResult
    {
        InProgress,
        BlackWin,
        WhiteWin,
        Draw
    }

    /// <summary>
    /// オセロ（8x8）の盤面ロジック
    /// </summary>
    public class OthelloBoard
    {
        public const int BoardSize = 8;
        public const int TotalSquares = BoardSize * BoardSize;

        private readonly PieceType[] _squares = new PieceType[TotalSquares];

        // 8方向のベクトル (dx, dy)
        private static readonly (int dx, int dy)[] Directions = new (int, int)[]
        {
            (-1, -1), (0, -1), (1, -1),
            (-1,  0),          (1,  0),
            (-1,  1), (0,  1), (1,  1)
        };

        public OthelloBoard()
        {
            Reset();
        }

        /// <summary>
        /// 盤面を初期状態にリセット
        /// </summary>
        public void Reset()
        {
            Array.Clear(_squares, 0, TotalSquares);

            // 中央4マスに初期配置
            // (3, 3)=White, (4, 4)=White, (3, 4)=Black, (4, 3)=Black
            SetPiece(3, 3, PieceType.White);
            SetPiece(4, 4, PieceType.White);
            SetPiece(3, 4, PieceType.Black);
            SetPiece(4, 3, PieceType.Black);
        }

        public static int ToIndex(int x, int y) => y * BoardSize + x;
        public static (int x, int y) ToCoord(int index) => (index % BoardSize, index / BoardSize);

        public static bool IsInside(int x, int y) => x >= 0 && x < BoardSize && y >= 0 && y < BoardSize;

        public PieceType GetPiece(int x, int y)
        {
            if (!IsInside(x, y)) return PieceType.Empty;
            return _squares[ToIndex(x, y)];
        }

        public PieceType GetPiece(int index)
        {
            if (index < 0 || index >= TotalSquares) return PieceType.Empty;
            return _squares[index];
        }

        private void SetPiece(int x, int y, PieceType piece)
        {
            _squares[ToIndex(x, y)] = piece;
        }

        /// <summary>
        /// 特定マスが指定プレイヤーにとって合法手かどうか判定
        /// </summary>
        public bool IsValidMove(int x, int y, PieceType player)
        {
            if (!IsInside(x, y) || GetPiece(x, y) != PieceType.Empty) return false;
            if (player != PieceType.Black && player != PieceType.White) return false;

            PieceType opponent = player.Opponent();

            foreach (var (dx, dy) in Directions)
            {
                int nx = x + dx;
                int ny = y + dy;
                int count = 0;

                while (IsInside(nx, ny) && GetPiece(nx, ny) == opponent)
                {
                    nx += dx;
                    ny += dy;
                    count++;
                }

                if (count > 0 && IsInside(nx, ny) && GetPiece(nx, ny) == player)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsValidMove(int index, PieceType player)
        {
            var (x, y) = ToCoord(index);
            return IsValidMove(x, y, player);
        }

        /// <summary>
        /// 指定プレイヤーのすべての合法手（マスインデックス 0〜63）を取得
        /// </summary>
        public List<int> GetValidMoves(PieceType player)
        {
            var moves = new List<int>();
            for (int i = 0; i < TotalSquares; i++)
            {
                if (IsValidMove(i, player))
                {
                    moves.Add(i);
                }
            }
            return moves;
        }

        /// <summary>
        /// 指定プレイヤーに打てる手が存在するか判定
        /// </summary>
        public bool HasAnyValidMove(PieceType player)
        {
            for (int i = 0; i < TotalSquares; i++)
            {
                if (IsValidMove(i, player))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 指定マスに石を配置し、挟んだ石を反転する。
        /// 成功した場合は反転したマスのインデックスリストを返す。
        /// 合法手でない場合は null を返す。
        /// </summary>
        public List<int> PlacePiece(int x, int y, PieceType player)
        {
            if (!IsValidMove(x, y, player)) return null;

            PieceType opponent = player.Opponent();
            var flippedSquares = new List<int>();

            foreach (var (dx, dy) in Directions)
            {
                int nx = x + dx;
                int ny = y + dy;
                var potentialFlips = new List<int>();

                while (IsInside(nx, ny) && GetPiece(nx, ny) == opponent)
                {
                    potentialFlips.Add(ToIndex(nx, ny));
                    nx += dx;
                    ny += dy;
                }

                if (potentialFlips.Count > 0 && IsInside(nx, ny) && GetPiece(nx, ny) == player)
                {
                    flippedSquares.AddRange(potentialFlips);
                }
            }

            if (flippedSquares.Count == 0) return null;

            // 石の配置と反転実行
            SetPiece(x, y, player);
            foreach (int index in flippedSquares)
            {
                _squares[index] = player;
            }

            return flippedSquares;
        }

        public List<int> PlacePiece(int index, PieceType player)
        {
            var (x, y) = ToCoord(index);
            return PlacePiece(x, y, player);
        }

        /// <summary>
        /// 各色の石数をカウント
        /// </summary>
        public (int blackCount, int whiteCount, int emptyCount) GetPieceCounts()
        {
            int black = 0;
            int white = 0;
            int empty = 0;

            for (int i = 0; i < TotalSquares; i++)
            {
                if (_squares[i] == PieceType.Black) black++;
                else if (_squares[i] == PieceType.White) white++;
                else empty++;
            }

            return (black, white, empty);
        }

        /// <summary>
        /// ゲーム終了判定（両者ともに合法手がない、または盤面が全て埋まっている）
        /// </summary>
        public bool IsGameOver()
        {
            return !HasAnyValidMove(PieceType.Black) && !HasAnyValidMove(PieceType.White);
        }

        /// <summary>
        /// ゲーム結果の判定
        /// </summary>
        public GameResult GetGameResult()
        {
            if (!IsGameOver()) return GameResult.InProgress;

            var (black, white, _) = GetPieceCounts();
            if (black > white) return GameResult.BlackWin;
            if (white > black) return GameResult.WhiteWin;
            return GameResult.Draw;
        }

        /// <summary>
        /// AI観測用の生データ配列（コピー）を取得
        /// </summary>
        public PieceType[] GetRawSquares()
        {
            var copy = new PieceType[TotalSquares];
            Array.Copy(_squares, copy, TotalSquares);
            return copy;
        }

        /// <summary>
        /// 盤面の複製を作成
        /// </summary>
        public OthelloBoard Clone()
        {
            var clone = new OthelloBoard();
            Array.Copy(_squares, clone._squares, TotalSquares);
            return clone;
        }
    }
}
