using System;
using UnityEngine;
using UnityEngine.UI;
using Othello.Core;

namespace Othello.View
{
    /// <summary>
    /// オセロ盤の1マス分のUI表示とクリックイベントを制御
    /// </summary>
    [RequireComponent(typeof(Button), typeof(Image))]
    public class OthelloTileView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Image tileBackgroundImage;
        [SerializeField] private Image pieceImage;
        [SerializeField] private Image hintHighlightImage;
        [SerializeField] private Button button;

        [Header("Colors")]
        [SerializeField] private Color tileColor = new Color(0.12f, 0.45f, 0.22f); // 深緑
        [SerializeField] private Color blackPieceColor = new Color(0.1f, 0.1f, 0.1f);
        [SerializeField] private Color whitePieceColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color hintColor = new Color(1f, 0.85f, 0.2f, 0.5f); // 半透明黄

        private int _squareIndex;
        private Action<int> _onClickCallback;

        public int SquareIndex => _squareIndex;

        private void Awake()
        {
            EnsureComponents();
        }

        public void Initialize(int index, Action<int> onClickCallback)
        {
            _squareIndex = index;
            _onClickCallback = onClickCallback;

            EnsureComponents();

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClickCallback?.Invoke(_squareIndex));
            }
        }

        private void EnsureComponents()
        {
            if (button == null) button = GetComponent<Button>();
            if (tileBackgroundImage == null) tileBackgroundImage = GetComponent<Image>();

            if (tileBackgroundImage != null)
            {
                tileBackgroundImage.color = tileColor;
            }

            // 石の表示用Imageが無ければ生成
            if (pieceImage == null)
            {
                Transform pieceTrans = transform.Find("Piece");
                if (pieceTrans != null)
                {
                    pieceImage = pieceTrans.GetComponent<Image>();
                }
                else
                {
                    GameObject pieceObj = new GameObject("Piece", typeof(RectTransform), typeof(Image));
                    pieceObj.transform.SetParent(transform, false);
                    pieceImage = pieceObj.GetComponent<Image>();
                    pieceImage.raycastTarget = false;

                    var rect = pieceImage.rectTransform;
                    rect.anchorMin = new Vector2(0.08f, 0.08f);
                    rect.anchorMax = new Vector2(0.92f, 0.92f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }

            // 合法手ヒント用Imageが無ければ生成
            if (hintHighlightImage == null)
            {
                Transform hintTrans = transform.Find("Hint");
                if (hintTrans != null)
                {
                    hintHighlightImage = hintTrans.GetComponent<Image>();
                }
                else
                {
                    GameObject hintObj = new GameObject("Hint", typeof(RectTransform), typeof(Image));
                    hintObj.transform.SetParent(transform, false);
                    hintHighlightImage = hintObj.GetComponent<Image>();
                    hintHighlightImage.raycastTarget = false;

                    var rect = hintHighlightImage.rectTransform;
                    rect.anchorMin = new Vector2(0.35f, 0.35f);
                    rect.anchorMax = new Vector2(0.65f, 0.65f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }

            // スプライトが未設定の場合、動的円形スプライトを適用
            if (pieceImage != null && pieceImage.sprite == null)
            {
                pieceImage.sprite = SpriteHelper.CreateCircleSprite(128, Color.white);
            }
            if (hintHighlightImage != null && hintHighlightImage.sprite == null)
            {
                hintHighlightImage.sprite = SpriteHelper.CreateCircleSprite(64, Color.white);
            }
        }

        /// <summary>
        /// マスの状態（石の種類、合法手ハイライト）を更新
        /// </summary>
        public void UpdateState(PieceType piece, bool isLegalMove)
        {
            EnsureComponents();

            // 石の表示切替
            if (pieceImage != null)
            {
                if (piece == PieceType.Empty)
                {
                    pieceImage.enabled = false;
                }
                else
                {
                    pieceImage.enabled = true;
                    pieceImage.color = (piece == PieceType.Black) ? blackPieceColor : whitePieceColor;
                }
            }

            // 合法手ハイライトの表示切替
            if (hintHighlightImage != null)
            {
                hintHighlightImage.enabled = isLegalMove;
                hintHighlightImage.color = hintColor;
            }

            // ボタンの活性・非活性（合法手のみクリック可能にするか、全体で制御するか）
            if (button != null)
            {
                button.interactable = isLegalMove;
            }
        }
    }
}
