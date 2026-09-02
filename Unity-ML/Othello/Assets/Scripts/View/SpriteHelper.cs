using UnityEngine;

namespace Othello.View
{
    /// <summary>
    /// 画像アセットがなくても円や四角のプロシージャルスプライトを生成するヘルパー
    /// </summary>
    public static class SpriteHelper
    {
        public static Sprite CreateCircleSprite(int size = 128, Color? color = null)
        {
            Color fillColor = color ?? Color.white;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = (size - 2) * 0.5f;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius - 1.0f)
                    {
                        texture.SetPixel(x, y, fillColor);
                    }
                    else if (dist <= radius)
                    {
                        // アンチエイリアス
                        float alpha = 1.0f - (dist - (radius - 1.0f));
                        Color c = fillColor;
                        c.a *= Mathf.Clamp01(alpha);
                        texture.SetPixel(x, y, c);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateRoundedSquareSprite(int size = 128, float cornerRadius = 16f, Color? color = null)
        {
            Color fillColor = color ?? Color.white;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - size * 0.5f) - (size * 0.5f - cornerRadius));
                    float dy = Mathf.Max(0, Mathf.Abs(y - size * 0.5f) - (size * 0.5f - cornerRadius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= cornerRadius - 1.0f)
                    {
                        texture.SetPixel(x, y, fillColor);
                    }
                    else if (dist <= cornerRadius)
                    {
                        float alpha = 1.0f - (dist - (cornerRadius - 1.0f));
                        Color c = fillColor;
                        c.a *= Mathf.Clamp01(alpha);
                        texture.SetPixel(x, y, c);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
