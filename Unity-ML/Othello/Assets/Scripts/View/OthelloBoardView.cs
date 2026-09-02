using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Othello.Core;

namespace Othello.View
{
    /// <summary>
    /// オセロ盤全体（8x8マス）の描画およびUI制御
    /// </summary>
    public class OthelloBoardView : MonoBehaviour
    {
        [Header("Tile Settings")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private Transform tilesContainer;
        [SerializeField] private List<OthelloTileView> tiles = new List<OthelloTileView>();

        [Header("Display Options")]
        [SerializeField] private bool showLegalMoveHints = true;
        [SerializeField] private Color boardBackgroundColor = new Color(0.08f, 0.25f, 0.12f); // 枠線の濃い緑

        public event Action<int> OnTileClicked;

        private void Awake()
        {
            InitializeGridIfNeeded();
        }

        /// <summary>
        /// タイルリストが未設定の場合、自動的にGridLayoutGroupを構成して64マスを生成
        /// </summary>
        public void InitializeGridIfNeeded()
        {
            if (tilesContainer == null)
            {
                tilesContainer = transform;
            }

            // 既に64マス存在する場合は初期化して終了
            if (tiles != null && tiles.Count == OthelloBoard.TotalSquares)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    tiles[i].Initialize(i, HandleTileClicked);
                }
                return;
            }

            // 子オブジェクトから探索
            var existingTiles = tilesContainer.GetComponentsInChildren<OthelloTileView>();
            if (existingTiles != null && existingTiles.Length == OthelloBoard.TotalSquares)
            {
                tiles = new List<OthelloTileView>(existingTiles);
                for (int i = 0; i < tiles.Count; i++)
                {
                    tiles[i].Initialize(i, HandleTileClicked);
                }
                return;
            }

            // 自動生成
            CreateProceduralBoard();
        }

        private void CreateProceduralBoard()
        {
            tiles.Clear();

            // 背景コンポーネントの設定
            if (TryGetComponent<Image>(out var bgImage))
            {
                bgImage.color = boardBackgroundColor;
            }

            // AspectRatioFitterを追加して正方形を維持
            var aspectFitter = GetComponent<AspectRatioFitter>();
            if (aspectFitter == null)
            {
                aspectFitter = gameObject.AddComponent<AspectRatioFitter>();
            }
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = 1.0f;

            // GridLayoutGroupの設定
            GridLayoutGroup grid = GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = gameObject.AddComponent<GridLayoutGroup>();
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = OthelloBoard.BoardSize;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.spacing = new Vector2(4, 4);
            grid.padding = new RectOffset(6, 6, 6, 6);

            // アスペクト比やセルサイズ（縦横の小さい方に合わせる）
            var rectTransform = GetComponent<RectTransform>();
            float totalWidth = rectTransform != null && rectTransform.rect.width > 0 ? rectTransform.rect.width : 500f;
            float totalHeight = rectTransform != null && rectTransform.rect.height > 0 ? rectTransform.rect.height : 500f;
            float minDim = Mathf.Min(totalWidth, totalHeight);

            float availableDim = minDim - (grid.padding.left + grid.padding.right) - (grid.spacing.x * (OthelloBoard.BoardSize - 1));
            float cellSize = Mathf.Max(20f, availableDim / OthelloBoard.BoardSize);
            grid.cellSize = new Vector2(cellSize, cellSize);

            // 64マスの生成
            for (int i = 0; i < OthelloBoard.TotalSquares; i++)
            {
                GameObject tileObj;
                if (tilePrefab != null)
                {
                    tileObj = Instantiate(tilePrefab, tilesContainer);
                }
                else
                {
                    tileObj = new GameObject($"Tile_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(OthelloTileView));
                    tileObj.transform.SetParent(tilesContainer, false);
                }

                var tileView = tileObj.GetComponent<OthelloTileView>();
                tileView.Initialize(i, HandleTileClicked);
                tiles.Add(tileView);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            AdjustCellSizes();
        }

        private void AdjustCellSizes()
        {
            if (TryGetComponent<GridLayoutGroup>(out var grid) && TryGetComponent<RectTransform>(out var rectTransform))
            {
                float totalWidth = rectTransform.rect.width > 0 ? rectTransform.rect.width : 500f;
                float totalHeight = rectTransform.rect.height > 0 ? rectTransform.rect.height : 500f;
                float minDim = Mathf.Min(totalWidth, totalHeight);

                float availableDim = minDim - (grid.padding.left + grid.padding.right) - (grid.spacing.x * (OthelloBoard.BoardSize - 1));
                float cellSize = Mathf.Max(20f, availableDim / OthelloBoard.BoardSize);
                grid.cellSize = new Vector2(cellSize, cellSize);
            }
        }

        private void HandleTileClicked(int squareIndex)
        {
            OnTileClicked?.Invoke(squareIndex);
        }

        /// <summary>
        /// 盤面データをもとに全マスの表示を更新
        /// </summary>
        public void UpdateView(OthelloBoard board, PieceType currentTurn, bool allowInteraction = true)
        {
            if (board == null || tiles == null || tiles.Count != OthelloBoard.TotalSquares) return;

            var validMoves = board.GetValidMoves(currentTurn);
            var validMoveSet = new HashSet<int>(validMoves);

            for (int i = 0; i < OthelloBoard.TotalSquares; i++)
            {
                PieceType piece = board.GetPiece(i);
                bool isLegal = allowInteraction && showLegalMoveHints && validMoveSet.Contains(i);

                tiles[i].UpdateState(piece, isLegal);
            }
        }

        /// <summary>
        /// すべてのタイルの操作を無効化（相手の手番中など）
        /// </summary>
        public void DisableAllInteractions()
        {
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    tile.UpdateState(tile.GetComponentInChildren<Image>().enabled ? PieceType.Empty : PieceType.Empty, false);
                }
            }
        }
    }
}
