// MoonBit WASM Playground Main Controller

const state = {
  wasm1: null,
  wasm2: null,
  wasm3: null,
  wasm4: null,
  lifeRunning: false,
  lifeAnimId: null,
  lastFpsTime: 0,
  frameCount: 0,
};

// ----------------------------------------------------
// 1. WASM Loaders
// ----------------------------------------------------
async function initModules() {
  try {
    // Step 1: Math
    const res1 = await fetch('./wasm/step1_math.wasm');
    const bytes1 = await res1.arrayBuffer();
    const inst1 = await WebAssembly.instantiate(bytes1);
    state.wasm1 = inst1.instance.exports;
    console.log('Loaded Step 1 Math WASM');

    // Step 2: String
    const res2 = await fetch('./wasm/step2_string.wasm');
    const bytes2 = await res2.arrayBuffer();
    const inst2 = await WebAssembly.instantiate(bytes2);
    state.wasm2 = inst2.instance.exports;
    console.log('Loaded Step 2 String WASM');

    // Step 3: Canvas
    const res3 = await fetch('./wasm/step3_canvas.wasm');
    const bytes3 = await res3.arrayBuffer();
    const inst3 = await WebAssembly.instantiate(bytes3);
    state.wasm3 = inst3.instance.exports;
    console.log('Loaded Step 3 Canvas WASM');

    // Step 4: FFI
    const ffiImports = {
      env: {
        js_log_number: (tag, val) => {
          const tags = { 1: 'Input', 2: 'Accumulating', 3: 'Elapsed(ms)' };
          appendLog(`Number Tag [${tags[tag] || tag}]: ${val}`, 'info');
        },
        js_log_string: (ptr, len) => {
          const u8 = new Uint8Array(state.wasm4.memory.buffer, ptr, len);
          const msg = new TextDecoder().decode(u8);
          appendLog(`String from WASM: "${msg}"`, 'highlight');
        },
        js_notify: (id) => {
          appendLog(`Event Triggered: Event Code #${id}`, 'warn');
        },
        js_get_time: () => performance.now(),
      }
    };
    const res4 = await fetch('./wasm/step4_ffi.wasm');
    const bytes4 = await res4.arrayBuffer();
    const inst4 = await WebAssembly.instantiate(bytes4, ffiImports);
    state.wasm4 = inst4.instance.exports;
    console.log('Loaded Step 4 FFI WASM');

    // 初期化デモ実行
    onInitReady();
  } catch (err) {
    console.error('Failed to load WASM modules:', err);
    alert('WASMの読み込みに失敗しました。サーバーが起動しているか確認してください: ' + err.message);
  }
}

function onInitReady() {
  // Step 1 初期計算
  runMathDemo();
  // Step 2 初期変換
  runStringTransform('upper');
  // Step 3 フラクタル初期描画
  renderFractal();
}

// ----------------------------------------------------
// 2. Tab Navigation
// ----------------------------------------------------
document.addEventListener('DOMContentLoaded', () => {
  const tabs = document.querySelectorAll('.tab-btn');
  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));

      tab.classList.add('active');
      const target = document.getElementById(tab.dataset.tab);
      if (target) target.classList.add('active');

      // タブ切り替え時に必要なら再描画
      if (tab.dataset.tab === 'step3') {
        if (!state.lifeRunning) renderFractal();
      }
    });
  });

  initModules();
});

// ----------------------------------------------------
// Step 1: Math & Benchmarks
// ----------------------------------------------------
function runMathDemo() {
  if (!state.wasm1) return;
  const a = parseInt(document.getElementById('math-a').value, 10) || 0;
  const b = parseInt(document.getElementById('math-b').value, 10) || 0;
  const sum = state.wasm1.add(a, b);
  document.getElementById('math-add-res').innerText = `${a} + ${b} = ${sum}`;

  const pNum = parseInt(document.getElementById('math-prime-input').value, 10) || 0;
  const isPrime = state.wasm1.is_prime(pNum) === 1;
  document.getElementById('math-prime-res').innerText = isPrime ? `${pNum} は素数です！` : `${pNum} は素数ではありません`;

  const cNum = parseInt(document.getElementById('math-collatz-input').value, 10) || 1;
  const steps = state.wasm1.collatz(cNum);
  document.getElementById('math-collatz-res').innerText = `${cNum} が 1 に達するまでのステップ数: ${steps}`;
}

// JSでのフィボナッチ再帰（比較用）
function jsFib(n) {
  if (n <= 1) return n;
  return jsFib(n - 1) + jsFib(n - 2);
}

function runBenchmark() {
  if (!state.wasm1) return;
  const n = parseInt(document.getElementById('bench-n').value, 10) || 35;
  const btn = document.getElementById('bench-btn');
  btn.disabled = true;
  btn.innerText = '計測中...';

  setTimeout(() => {
    // WASMベンチマーク
    const t0 = performance.now();
    const wasmRes = state.wasm1.fib(n);
    const t1 = performance.now();
    const wasmTime = t1 - t0;

    // JSベンチマーク
    const t2 = performance.now();
    const jsRes = jsFib(n);
    const t3 = performance.now();
    const jsTime = t3 - t2;

    btn.disabled = false;
    btn.innerText = 'ベンチマーク実行';

    // UI更新
    document.getElementById('bench-wasm-val').innerText = `${wasmTime.toFixed(2)} ms (結果: ${wasmRes})`;
    document.getElementById('bench-js-val').innerText = `${jsTime.toFixed(2)} ms (結果: ${jsRes})`;

    const maxTime = Math.max(wasmTime, jsTime, 0.01);
    document.getElementById('bench-wasm-bar').style.width = `${Math.min(100, (wasmTime / maxTime) * 100)}%`;
    document.getElementById('bench-js-bar').style.width = `${Math.min(100, (jsTime / maxTime) * 100)}%`;

    const ratio = (jsTime / Math.max(wasmTime, 0.0001)).toFixed(1);
    const summary = document.getElementById('bench-summary');
    if (wasmTime < jsTime) {
      summary.innerHTML = `<span style="color: var(--accent-green)">🚀 MoonBit WASM が JavaScript より約 <strong>${ratio}倍</strong> 高速でした！</span>`;
    } else {
      summary.innerHTML = `<span>ほぼ同等の実行速度でした。</span>`;
    }
  }, 20);
}

// ----------------------------------------------------
// Step 2: String & Memory Interop
// ----------------------------------------------------
function runStringTransform(mode) {
  if (!state.wasm2) return;
  const inputEl = document.getElementById('str-input');
  const text = inputEl.value;

  const enc = new TextEncoder();
  const dec = new TextDecoder();
  const encoded = enc.encode(text);

  const inPtr = state.wasm2.get_in_buf_ptr();
  const outPtr = state.wasm2.get_out_buf_ptr();
  const bufSize = state.wasm2.get_buf_size();

  // 1. WASM の入力バッファにバイト列を書き込む
  const memU8 = new Uint8Array(state.wasm2.memory.buffer);
  const writeLen = Math.min(encoded.length, bufSize);
  memU8.set(encoded.subarray(0, writeLen), inPtr);

  // 2. MoonBit 関数を呼ぶ
  let outLen = 0;
  if (mode === 'upper') {
    outLen = state.wasm2.to_upper(writeLen);
  } else if (mode === 'reverse') {
    outLen = state.wasm2.reverse(writeLen);
  } else if (mode === 'rot13') {
    outLen = state.wasm2.rot13(writeLen);
  } else if (mode === 'count') {
    const count = state.wasm2.count_words(writeLen);
    document.getElementById('str-output').innerText = `単語数: ${count} 語 (バイト数: ${writeLen} bytes)`;
    updateMemoryHexDump(inPtr, writeLen);
    return;
  }

  // 3. WASM の出力バッファから読み取ってデコード
  const outBytes = new Uint8Array(state.wasm2.memory.buffer, outPtr, outLen);
  const resultText = dec.decode(outBytes);
  document.getElementById('str-output').innerText = resultText;

  // メモリビューアの更新
  updateMemoryHexDump(outPtr, outLen);
}

function updateMemoryHexDump(ptr, len) {
  const dumpEl = document.getElementById('memory-dump');
  if (!dumpEl || !state.wasm2) return;

  const showLen = Math.min(len || 16, 64);
  const u8 = new Uint8Array(state.wasm2.memory.buffer, ptr, showLen);

  let html = '';
  for (let i = 0; i < showLen; i += 8) {
    const addr = '0x' + (ptr + i).toString(16).padStart(4, '0');
    let hex = '';
    let ascii = '';
    for (let j = 0; j < 8; j++) {
      if (i + j < showLen) {
        const b = u8[i + j];
        hex += b.toString(16).padStart(2, '0') + ' ';
        ascii += (b >= 32 && b <= 126) ? String.fromCharCode(b) : '.';
      } else {
        hex += '   ';
      }
    }
    html += `<div><span class="mem-addr">${addr}</span> <span class="mem-hex">${hex}</span> <span class="mem-ascii">${escapeHtml(ascii)}</span></div>`;
  }
  dumpEl.innerHTML = html;
}

function escapeHtml(str) {
  return str.replace(/[&<>"']/g, m => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' }[m]));
}

// ----------------------------------------------------
// Step 3: Canvas Fast Memory Graphics
// ----------------------------------------------------
function renderFractal() {
  if (!state.wasm3) return;
  const canvas = document.getElementById('fractal-canvas');
  const ctx = canvas.getContext('2d');

  const zoom = parseFloat(document.getElementById('frac-zoom').value) || 1.0;
  const maxIter = parseInt(document.getElementById('frac-iter').value, 10) || 64;
  const cx = parseFloat(document.getElementById('frac-cx').value) || -0.7;
  const cy = parseFloat(document.getElementById('frac-cy').value) || 0.0;

  document.getElementById('frac-zoom-label').innerText = `${zoom.toFixed(1)}x`;

  const t0 = performance.now();
  // WASM側でメモリバッファへ描画
  state.wasm3.render_mandelbrot(cx, cy, zoom, maxIter);
  const t1 = performance.now();

  // ゼロコピー転送: WASMの線形メモリバッファから直接 ImageData を生成
  const width = state.wasm3.get_width();
  const height = state.wasm3.get_height();
  const ptr = state.wasm3.get_pixel_buf_ptr();
  const clamped = new Uint8ClampedArray(state.wasm3.memory.buffer, ptr, width * height * 4);
  const imgData = new ImageData(clamped, width, height);

  ctx.putImageData(imgData, 0, 0);

  document.getElementById('frac-time').innerText = `${(t1 - t0).toFixed(2)} ms`;
}

// ライフゲーム
function initLifeGame(mode) {
  if (!state.wasm3) return;
  const seed = mode === 'random' ? Math.floor(Math.random() * 1000000) : 12345;
  state.wasm3.init_life_random(seed);
  drawLifeCanvas();
}

function toggleLifeRunning() {
  const btn = document.getElementById('life-run-btn');
  state.lifeRunning = !state.lifeRunning;
  if (state.lifeRunning) {
    btn.innerText = '停止 (Pause)';
    btn.classList.replace('btn-green', 'btn-secondary');
    state.lastFpsTime = performance.now();
    state.frameCount = 0;
    loopLifeGame();
  } else {
    btn.innerText = '開始 (Start)';
    btn.classList.replace('btn-secondary', 'btn-green');
    if (state.lifeAnimId) cancelAnimationFrame(state.lifeAnimId);
  }
}

function stepLifeOnce() {
  if (!state.wasm3) return;
  state.wasm3.step_life();
  drawLifeCanvas();
}

function loopLifeGame() {
  if (!state.lifeRunning) return;

  state.wasm3.step_life();
  drawLifeCanvas();

  // FPS計測
  state.frameCount++;
  const now = performance.now();
  if (now - state.lastFpsTime >= 500) {
    const fps = (state.frameCount / ((now - state.lastFpsTime) / 1000)).toFixed(1);
    document.getElementById('life-fps').innerText = `${fps} FPS`;
    state.frameCount = 0;
    state.lastFpsTime = now;
  }

  state.lifeAnimId = requestAnimationFrame(loopLifeGame);
}

function drawLifeCanvas() {
  const canvas = document.getElementById('life-canvas');
  const ctx = canvas.getContext('2d');
  const width = state.wasm3.get_width();
  const height = state.wasm3.get_height();
  const ptr = state.wasm3.get_pixel_buf_ptr();

  const clamped = new Uint8ClampedArray(state.wasm3.memory.buffer, ptr, width * height * 4);
  const imgData = new ImageData(clamped, width, height);
  ctx.putImageData(imgData, 0, 0);
}

// Canvas クリックでセル反転
function setupCanvasInteractions() {
  const canvas = document.getElementById('life-canvas');
  if (!canvas) return;
  canvas.addEventListener('click', (e) => {
    if (!state.wasm3) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    const x = Math.floor((e.clientX - rect.left) * scaleX);
    const y = Math.floor((e.clientY - rect.top) * scaleY);
    state.wasm3.toggle_cell(x, y);
    drawLifeCanvas();
  });
}

// ----------------------------------------------------
// Step 4: JavaScript FFI (External Calls)
// ----------------------------------------------------
function triggerFfiDemo() {
  if (!state.wasm4) return;
  const n = parseInt(document.getElementById('ffi-input-n').value, 10) || 20;
  appendLog(`--- MoonBit run_ffi_calc(${n}) 呼び出し開始 ---`, 'info');
  const total = state.wasm4.run_ffi_calc(n);
  document.getElementById('ffi-sum-res').innerText = `合計: ${total}`;
  appendLog(`MoonBit 計算完了: 戻り値 = ${total}`, 'highlight');
}

function triggerFfiGreeting() {
  if (!state.wasm4) return;
  const id = Math.floor(Math.random() * 10);
  appendLog(`--- MoonBit send_greeting(${id}) 呼び出し ---`, 'info');
  state.wasm4.send_greeting(id);
}

function appendLog(msg, type = 'normal') {
  const term = document.getElementById('ffi-terminal');
  if (!term) return;

  const row = document.createElement('div');
  row.className = 'log-item';

  const timeStr = new Date().toTimeString().split(' ')[0];
  let tag = 'HOST JS';
  let typeClass = '';
  if (type === 'highlight') {
    tag = 'WASM->JS';
    typeClass = 'highlight';
  } else if (type === 'warn') {
    tag = 'EVENT';
    typeClass = 'highlight';
  }

  row.innerHTML = `
    <span class="log-time">[${timeStr}]</span>
    <span class="log-tag">${tag}</span>
    <span class="log-msg ${typeClass}">${escapeHtml(msg)}</span>
  `;

  term.appendChild(row);
  term.scrollTop = term.scrollHeight;
}

function clearLog() {
  const term = document.getElementById('ffi-terminal');
  if (term) term.innerHTML = '';
}

// 初期バインド
window.addEventListener('DOMContentLoaded', () => {
  setupCanvasInteractions();
});
