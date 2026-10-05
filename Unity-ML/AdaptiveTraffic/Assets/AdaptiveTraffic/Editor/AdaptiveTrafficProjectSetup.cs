// =====================================================================================
// AdaptiveTrafficProjectSetup.cs (Editor only)
// メニュー: Tools > Adaptive Traffic > Apply Project Settings
//
// 学習に必要なプロジェクト設定を Editor API 経由で適用する。
// ProjectSettings/*.asset をエディタ起動中に外部から書き換えても、メモリ上の値で
// 上書き保存されることがあるため、確実に反映したいときはこのメニューを使う。
//
//   - Time > Fixed Timestep = 0.05（20Hz）
//       Decision Period 20 で 1 判断 = 1 秒。信号・車両の時間はこの値から換算される。
//   - Player > Run In Background = ON
//       mlagents-learn 実行中にエディタ/ビルドがフォーカスを失っても学習を止めない。
//   - Player > Visible In Background = ON（スタンドアロンビルド用）
// =====================================================================================
using UnityEditor;
using UnityEngine;

namespace AdaptiveTraffic.EditorTools
{
    public static class AdaptiveTrafficProjectSetup
    {
        public const float FixedTimestep = 0.05f;

        [MenuItem("Tools/Adaptive Traffic/Apply Project Settings")]
        public static void ApplyProjectSettings()
        {
            Time.fixedDeltaTime = FixedTimestep;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log($"[AdaptiveTraffic] Project settings applied: Fixed Timestep={Time.fixedDeltaTime}, " +
                $"Run In Background={PlayerSettings.runInBackground}");
        }

        [MenuItem("Tools/Adaptive Traffic/Apply Project Settings", true)]
        static bool CanApplyProjectSettings() => !EditorApplication.isPlaying;
    }
}
