# MoonBit WASM 一括ビルド & 配置スクリプト

$MoonBin = "$HOME\.moon\bin"
if ($env:Path -split ';' -notcontains $MoonBin) {
    $env:Path = "$MoonBin;$env:Path"
}

Write-Host "🔨 MoonBit コードの型チェック中..." -ForegroundColor Cyan
moon check
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 型チェックでエラーが発生しました。" -ForegroundColor Red
    exit 1
}

Write-Host "🚀 WebAssembly にコンパイル中..." -ForegroundColor Cyan
moon build --target wasm
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ ビルドに失敗しました。" -ForegroundColor Red
    exit 1
}

Write-Host "📦 WASM ファイルを web/wasm/ へコピー中..." -ForegroundColor Cyan
if (-not (Test-Path "web\wasm")) {
    New-Item -ItemType Directory -Path "web\wasm" -Force | Out-Null
}

Copy-Item "_build\wasm\debug\build\src\step1_math\step1_math.wasm" "web\wasm\" -Force
Copy-Item "_build\wasm\debug\build\src\step2_string\step2_string.wasm" "web\wasm\" -Force
Copy-Item "_build\wasm\debug\build\src\step3_canvas\step3_canvas.wasm" "web\wasm\" -Force
Copy-Item "_build\wasm\debug\build\src\step4_ffi\step4_ffi.wasm" "web\wasm\" -Force

Write-Host "✅ すべてのモジュールのビルドと配置が完了しました！" -ForegroundColor Green
Get-ChildItem "web\wasm\*.wasm" | Select-Object Name, Length
