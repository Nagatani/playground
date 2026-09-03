///|
/// Step 3: Canvas 高速メモリ描画 (Mandelbrot & Game of Life)
/// WASM の共有メモリ内の RGBA 配列に直接書き込み、JS側でゼロコピーで ImageData に渡して描画します。

fn to_ptr(arr : FixedArray[Byte]) -> Int = "%identity"

let width : Int = 256
let height : Int = 256
let pixel_count : Int = width * height
let buffer_size : Int = pixel_count * 4 // RGBA

// RGBA ピクセルバッファ (HTML5 Canvas の ImageData と同一構造)
let pixel_buf : FixedArray[Byte] = FixedArray::make(buffer_size, b"\x00"[0])

// ライフゲーム用のセル配列 (0 = 死, 1 = 生)
let current_cells : FixedArray[Byte] = FixedArray::make(pixel_count, b"\x00"[0])
let next_cells : FixedArray[Byte] = FixedArray::make(pixel_count, b"\x00"[0])

#export_name("get_width")
pub fn get_width() -> Int {
  width
}

#export_name("get_height")
pub fn get_height() -> Int {
  height
}

#export_name("get_pixel_buf_ptr")
pub fn get_pixel_buf_ptr() -> Int {
  to_ptr(pixel_buf)
}

// ----------------------------------------------------
// 1. マンデルブロ集合フラクタル描画
// ----------------------------------------------------

#export_name("render_mandelbrot")
pub fn render_mandelbrot(
  center_x : Double,
  center_y : Double,
  zoom : Double,
  max_iter : Int
) -> Unit {
  let w_f = width.to_double()
  let h_f = height.to_double()
  let range_x = 3.0 / zoom
  let range_y = (3.0 * h_f / w_f) / zoom
  let min_x = center_x - range_x / 2.0
  let min_y = center_y - range_y / 2.0

  let mut idx = 0
  for py = 0; py < height; py = py + 1 {
    let cy = min_y + py.to_double() * (range_y / h_f)
    for px = 0; px < width; px = px + 1 {
      let cx = min_x + px.to_double() * (range_x / w_f)

      let mut zx = 0.0
      let mut zy = 0.0
      let mut iter = 0

      while zx * zx + zy * zy <= 4.0 && iter < max_iter {
        let next_zx = zx * zx - zy * zy + cx
        zy = 2.0 * zx * zy + cy
        zx = next_zx
        iter = iter + 1
      }

      // カラーマップ計算
      let offset = idx * 4
      if iter == max_iter {
        // マンデルブロ集合の内側: ダークネイビー
        pixel_buf[offset + 0] = b"\x0f"[0]
        pixel_buf[offset + 1] = b"\x17"[0]
        pixel_buf[offset + 2] = b"\x2a"[0]
        pixel_buf[offset + 3] = b"\xff"[0]
      } else {
        // 発散速度に応じた鮮やかなグラデーション
        let t = iter.to_double() / max_iter.to_double()
        let r = (9.0 * (1.0 - t) * t * t * t * 255.0).to_int()
        let g = (15.0 * (1.0 - t) * (1.0 - t) * t * t * 255.0).to_int()
        let b_val = (8.5 * (1.0 - t) * (1.0 - t) * (1.0 - t) * t * 255.0).to_int()

        let r_clamped = if r > 255 { 255 } else { r }
        let g_clamped = if g > 255 { 255 } else { g }
        let b_clamped = if b_val > 255 { 255 } else { b_val }

        pixel_buf[offset + 0] = r_clamped.to_byte()
        pixel_buf[offset + 1] = g_clamped.to_byte()
        pixel_buf[offset + 2] = b_clamped.to_byte()
        pixel_buf[offset + 3] = b"\xff"[0] // Alpha = 255
      }
      idx = idx + 1
    }
  }
}

// ----------------------------------------------------
// 2. コンウェイのライフゲーム (Conway Game of Life)
// ----------------------------------------------------

let rng_cell : FixedArray[Int] = [123456789]

fn next_random() -> Int {
  let s = (rng_cell[0] * 1103515245 + 12345) & 0x7FFFFFFF
  rng_cell[0] = s
  s
}

#export_name("init_life_random")
pub fn init_life_random(seed : Int) -> Unit {
  rng_cell[0] = if seed == 0 { 123456789 } else { seed }
  for i = 0; i < pixel_count; i = i + 1 {
    let rand = next_random()
    let alive = if rand % 100 < 20 { 1 } else { 0 }
    current_cells[i] = alive.to_byte()
  }
  draw_cells_to_buffer()
}

#export_name("toggle_cell")
pub fn toggle_cell(x : Int, y : Int) -> Unit {
  if x >= 0 && x < width && y >= 0 && y < height {
    let idx = y * width + x
    let cur = current_cells[idx].to_int()
    let new_val = if cur == 0 { 1 } else { 0 }
    current_cells[idx] = new_val.to_byte()
    draw_cells_to_buffer()
  }
}

#export_name("step_life")
pub fn step_life() -> Unit {
  for y = 0; y < height; y = y + 1 {
    let y_up = if y == 0 { height - 1 } else { y - 1 }
    let y_down = if y == height - 1 { 0 } else { y + 1 }

    for x = 0; x < width; x = x + 1 {
      let x_left = if x == 0 { width - 1 } else { x - 1 }
      let x_right = if x == width - 1 { 0 } else { x + 1 }

      let neighbors = current_cells[y_up * width + x_left].to_int() +
        current_cells[y_up * width + x].to_int() +
        current_cells[y_up * width + x_right].to_int() +
        current_cells[y * width + x_left].to_int() +
        current_cells[y * width + x_right].to_int() +
        current_cells[y_down * width + x_left].to_int() +
        current_cells[y_down * width + x].to_int() +
        current_cells[y_down * width + x_right].to_int()

      let idx = y * width + x
      let is_alive = current_cells[idx].to_int() == 1

      let next_state = if is_alive {
        if neighbors == 2 || neighbors == 3 { 1 } else { 0 }
      } else {
        if neighbors == 3 { 1 } else { 0 }
      }

      next_cells[idx] = next_state.to_byte()
    }
  }

  for i = 0; i < pixel_count; i = i + 1 {
    current_cells[i] = next_cells[i]
  }

  draw_cells_to_buffer()
}

fn draw_cells_to_buffer() -> Unit {
  for i = 0; i < pixel_count; i = i + 1 {
    let offset = i * 4
    if current_cells[i].to_int() == 1 {
      pixel_buf[offset + 0] = b"\x10"[0]
      pixel_buf[offset + 1] = b"\xb9"[0]
      pixel_buf[offset + 2] = b"\x81"[0]
      pixel_buf[offset + 3] = b"\xff"[0]
    } else {
      pixel_buf[offset + 0] = b"\x0f"[0]
      pixel_buf[offset + 1] = b"\x17"[0]
      pixel_buf[offset + 2] = b"\x2a"[0]
      pixel_buf[offset + 3] = b"\xff"[0]
    }
  }
}

