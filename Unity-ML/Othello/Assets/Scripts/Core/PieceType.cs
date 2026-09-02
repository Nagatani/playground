namespace Othello.Core
{
    /// <summary>
    /// オセロの盤面マスおよびプレイヤーの手番を表す列挙型
    /// </summary>
    public enum PieceType
    {
        Empty = 0,
        Black = 1,
        White = 2
    }

    public static class PieceTypeExtensions
    {
        /// <summary>
        /// 相手プレイヤーの手番（色）を取得
        /// </summary>
        public static PieceType Opponent(this PieceType piece)
        {
            return piece switch
            {
                PieceType.Black => PieceType.White,
                PieceType.White => PieceType.Black,
                _ => PieceType.Empty
            };
        }
    }
}
