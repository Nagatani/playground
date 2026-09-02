# ML-Agents による 2D オセロ 強化学習ガイド

このプロジェクトでは、Unity ML-Agents の **Self-Play（自己対戦）機能** を使用して、オセロAIが自身と対戦を繰り返しながら徐々に強い手を学習するシステムを構築しています。

---

## 1. 前提条件と環境構築

### (1) Python 環境の準備
Python 3.9 〜 3.10 推奨（※ML-Agents release 23 の対応バージョン）。

```bash
# 仮想環境の作成（venv または conda）
python -m venv venv-othello
# 仮想環境の有効化 (Windows PowerShellの場合)
.\venv-othello\Scripts\Activate.ps1
# 仮想環境の有効化 (コマンドプロンプトの場合)
.\venv-othello\Scripts\activate.bat

# PyTorch のインストール（CUDA対応GPUがある場合はGPU版を推奨）
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118  # CUDA 11.8の場合
# または CPU版: pip install torch torchvision

# ML-Agents Python パッケージのインストール
pip install mlagents
```

### (2) インストール確認
```bash
mlagents-learn --help
```
ヘルプが表示されればPython側の準備完了です。

---

## 2. Unity 側のセットアップ

### エージェントのインスペクター設定（Behavior Parameters）
オセロのエージェント（`OthelloAgent`）を持つ GameObject に、ML-Agents の **`Behavior Parameters`** コンポーネントをアタッチして以下のように設定します。

- **Behavior Name**: `Othello` （※YAMLファイル内の設定名と一致させます）
- **Vector Observation**:
  - **Space Size**: `64` （8x8盤面マス）
  - **Stacked Vectors**: `1`
- **Actions**:
  - **Continuous Actions**: `0`
  - **Discrete Branches**: `1`
    - **Branch 0 Size**: `64` （着手するマスのインデックス 0〜63）
- **Model**:
  - 学習前・学習中: `None`（空欄）
  - 学習完了後: エクスポートされた `.onnx` ファイルをドラッグ＆ドロップ
- **Behavior Type**:
  - 学習時: `Default`
  - 人間操作テスト時: `Heuristic Only`
  - 学習済みモデルで推論時: `Inference Only`（または `Default` にして Model を指定）
- **Team ID**:
  - 黒番エージェント: `0`
  - 白番エージェント: `1`
  - ※Self-play では Team ID が異なるエージェント同士が対戦相手として認識されます。

---

## 3. 学習の実行（Training）

### (1) コマンドの実行
プロジェクトのルートディレクトリ（または `ML-Agents` ディレクトリ）で以下のコマンドを実行します。

```bash
# 学習開始コマンド
mlagents-learn ML-Agents/config/othello_selfplay.yaml --run-id=Othello_SelfPlay_01
```

### (2) Unity エディタで再生
コマンドを実行するとコンソールに以下のようなメッセージが表示され待機状態になります：
```
[INFO] Listening on port 5004. Start training by pressing the Play button in the Unity Editor.
```
この状態で **Unity エディタの「Play（再生）」ボタン** を押すと、学習が開始されます。

> **💡 高速学習のヒント**:
> 1. シーン内に `OthelloEnvironment` の Prefab を複数（8〜32個程度）並べると並列で対戦が進み、劇的に学習速度が向上します。
> 2. Unity エディタ上ではなく、Stand-alone ビルド（.exe）を作成して `--env=Build/Othello.exe --num-envs=4` のように実行するとさらに高速に学習できます。

---

## 4. 学習進捗の確認（TensorBoard）

学習中の Elo レート（強さの指標）や累積報酬の推移をブラウザでリアルタイムに確認できます。

```bash
# 別のターミナルを開いて実行
tensorboard --logdir results
```
ブラウザで `http://localhost:6006` を開くと以下が確認できます：
- **`Policy/Elo`**: 自己対戦における強さの指標（順調に学習が進むと右肩上がりに上昇します）。
- **`Environment/Cumulative Reward`**: エピソードごとの獲得報酬。
- **`Losses/Value Loss` / `Losses/Policy Loss`**: ニューラルネットワークの学習ロス。

---

## 5. 学習済みモデル（ONNX）の適用と対戦

1. 学習が完了（または途中で `Ctrl + C` で終了）すると、`results/Othello_SelfPlay_01/Othello/Othello.onnx` が生成されます。
2. この `Othello.onnx` ファイルを Unity の `Assets` フォルダ（例: `Assets/Models/`）にコピー＆ドラッグします。
3. Unity エディタ上で、AIプレイヤーとして動かしたい `OthelloAgent` の **`Behavior Parameters` -> `Model`** スロットに、この `Othello.onnx` を割り当てます。
4. **`Behavior Type`** を `Inference Only`（または `Default`）に設定し、シーンを再生すれば、学習したAIと人間が対戦できます！
