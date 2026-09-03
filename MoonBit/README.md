# MoonBit × WebAssembly 実践学習プレイグラウンド

WebAssembly（WASM）ファーストの新世代プログラミング言語 **MoonBit（ムーンビット）** の環境構築から、WASMのビルド、JavaScriptとの連携、ブラウザでの高速描画までを体系的に学べる実践プロジェクトです。

---

## 🎯 プロジェクトの特長

- **4つの段階的カリキュラム**:
  1. **Step 1: 基本演算 & JS vs WASM 速度比較**: 関数のエクスポートと純粋計算ベンチマーク
  2. **Step 2: 文字列 & 共有メモリ (Linear Memory)**: ポインタとUTF-8エンコードによる双方向データ送受信
  3. **Step 3: Canvas 高速描画**: WASMメモリ上のピクセルバッファを直接描画するゼロコピーアーキテクチャ（マンデルブロ集合 & ライフゲーム）
  4. **Step 4: JavaScript FFI**: ホスト（ブラウザ）関数のインポートと双方向イベント連携
- **インタラクティブWebポータル**: ブラウザ上でコードを読みながら、ボタン操作やスライダーで直感的に動作確認可能
- **超軽量WASMバイナリ**: 各ステップのWASMファイルサイズはわずか 4KB〜14KB 程度！

---

## 🚀 クイックスタート

### 1. MoonBit ツールチェーンのインストール (Windows)

PowerShell で以下のコマンドを実行します（本環境には既にセットアップ済みです）。

```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
irm https://cli.moonbitlang.com/install/powershell.ps1 | iex
```

> **VS Code をお使いの場合:**
> VS Code の拡張機能マーケットプレイスから公式の **「MoonBit」** 拡張機能をインストールすると、シンタックスハイライト、型推論ホバー、コード補完、Linterが有効になります。

### 2. ローカルサーバーの起動

Node.js（標準モジュールのみ使用、`npm install` 不要）でサーバーを起動します。

```powershell
node server.js
```

ブラウザで **[http://localhost:8080](http://localhost:8080)** を開いてください。
インタラクティブな学習ポータルが立ち上がります。

### 3. MoonBit コードの編集と再ビルド

コードを変更した場合は、以下のスクリプトを実行すると自動で型チェック、WASMコンパイル、Web公開ディレクトリへの配置が行われます。

```powershell
pwsh -ExecutionPolicy Bypass -File .\build.ps1
```

または手動コマンド:
```powershell
moon check
moon build --target wasm
```

---

## 📂 ディレクトリ構成

```text
D:\github\playground\MoonBit\
├── moon.mod                    # MoonBit モジュール定義
├── src\
│   ├── step1_math\             # [Step 1] 基本計算 & ベンチマーク
│   │   ├── moon.pkg            #   パッケージ設定 (foreign_library)
│   │   └── math.mbt            #   MoonBit 実装 (add, fib, is_prime, collatz)
│   ├── step2_string\           # [Step 2] 文字列 & 共有メモリ
│   │   ├── moon.pkg            #   メモリ設定 (export-memory-name: "memory")
│   │   └── string.mbt          #   MoonBit 実装 (to_upper, reverse, rot13)
│   ├── step3_canvas\           # [Step 3] Canvas 高速ゼロコピー描画
│   │   ├── moon.pkg            #   メモリ設定
│   │   └── canvas.mbt          #   MoonBit 実装 (Mandelbrot & Conway Game of Life)
│   └── step4_ffi\              # [Step 4] JavaScript FFI 外部関数連携
│       ├── moon.pkg
│       └── ffi.mbt             #   MoonBit 実装 (extern "js" 呼び出し)
├── web\                        # ブラウザ動作用 Web ポータル
│   ├── index.html              #   学習UI & 解説ドキュメント
│   ├── app.js                  #   WebAssembly ロード & JS コントローラ
│   ├── style.css               #   モダン・ダークUIデザイン
│   └── wasm\                   #   ビルドされた .wasm ファイル群
├── server.js                   # 軽量ローカルHTTPサーバー (Node.js)
├── build.ps1                   # ビルド & コピースクリプト
└── README.md                   # 本ドキュメント
```

---

## 📚 各ステップの解説

### Step 1: 基本的な数値計算 (`src/step1_math`)
- **エクスポート**: `#export_name("add")` を関数上部に付与することで、WASMの公開関数としてエクスポートされます。
- **パッケージ種別**: `moon.pkg` に `pkgtype(kind: "foreign_library")` を指定します。
- **パフォーマンス**: フィボナッチ再帰計算 (`fib(38)`) など、CPUヘビーな計算において JavaScript (V8) より高速に実行されます。

### Step 2: 文字列と共有メモリ (`src/step2_string`)
- **メモリモデル**: WebAssemblyの線形メモリ（Linear Memory）を `options(link: { "wasm": { "export-memory-name": "memory" } })` でホストに公開します。
- **ポインタ**: WASM内でのポインタは単なる先頭からのバイトオフセット（`Int`）です。MoonBit組み込みの `fn to_ptr(arr : FixedArray[Byte]) -> Int = "%identity"` で安全にオフセットを取得できます。
- **データ受け渡し**: JS側の `TextEncoder` で UTF-8 バイト列を入力バッファへ書き込み、MoonBitで処理した後、出力バッファから `TextDecoder` で文字列へ復元します。

### Step 3: Canvas 高速描画 (`src/step3_canvas`)
- **ゼロコピー描画**: 256×256 ピクセルの RGBA 配列（262,144 バイト）を MoonBit 内に確保し、WASM内で直接色を書き込みます。
- **ImageData の直結**: JS側で `new ImageData(new Uint8ClampedArray(wasm.memory.buffer, ptr, width * height * 4), width, height)` を作り、`ctx.putImageData` に渡すことで、GPU描画直前までメモリの重複コピーが一切発生しません。
- **マンデルブロ集合**: 複素平面の反復計算をリアルタイム実行。
- **ライフゲーム**: 60FPSで滑らかにセル世代交代シミュレーションを実行。

### Step 4: JavaScript FFI (`src/step4_ffi`)
- **外部関数宣言**: `fn js_log_number(tag : Int, val : Int) -> Unit = "env" "js_log_number"` のように記述することで、ホスト（JS）側の関数を呼び出せます。
- **インスタンス化時の注入**: JS側で `WebAssembly.instantiate(bytes, { env: { js_log_number: ... } })` を渡すことでバインドされます。

---

## 💡 次のステップへの発展アイデア

1. **WASM-GC の活用**: `moon build --target wasm-gc` を試して、WebAssemblyのガベージコレクション提案（WasmGC）を活用した構造体のやり取りを体験する。
2. **Web Audio API との連携**: WASM側でサイン波やシンセサイザーのオーディオバッファ（PCM）をリアルタイム生成し、ブラウザで音を鳴らす。
3. **ゲーム開発**: キーボード操作や物理演算を組み込んだ本格的な 2D レトロゲームの作成。
