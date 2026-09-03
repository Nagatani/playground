///|
/// Step 2: WASM 線形メモリを介した文字列の送受信
/// WASMとJSは直接オブジェクトを受け渡せないため、共有メモリ(Linear Memory)を介してバイト列を受け渡します。

fn to_ptr(arr : FixedArray[Byte]) -> Int = "%identity"

let buffer_size : Int = 4096
let in_buf : FixedArray[Byte] = FixedArray::make(buffer_size, b"\x00"[0])
let out_buf : FixedArray[Byte] = FixedArray::make(buffer_size, b"\x00"[0])

#export_name("get_in_buf_ptr")
pub fn get_in_buf_ptr() -> Int {
  to_ptr(in_buf)
}

#export_name("get_out_buf_ptr")
pub fn get_out_buf_ptr() -> Int {
  to_ptr(out_buf)
}

#export_name("get_buf_size")
pub fn get_buf_size() -> Int {
  buffer_size
}

/// 入力バッファ内の英字を大文字に変換し、出力バッファへ格納
#export_name("to_upper")
pub fn to_upper(len : Int) -> Int {
  let n = if len > buffer_size { buffer_size } else { len }
  for i = 0; i < n; i = i + 1 {
    let b = in_buf[i].to_int()
    // a-z (97 - 122) -> A-Z (65 - 90)
    let converted = if b >= 97 && b <= 122 {
      b - 32
    } else {
      b
    }
    out_buf[i] = converted.to_byte()
  }
  n
}

/// 入力バッファのバイト列を反転して出力バッファへ格納
#export_name("reverse")
pub fn reverse(len : Int) -> Int {
  let n = if len > buffer_size { buffer_size } else { len }
  for i = 0; i < n; i = i + 1 {
    out_buf[i] = in_buf[n - 1 - i]
  }
  n
}

/// ROT13 暗号化 / 復号
#export_name("rot13")
pub fn rot13(len : Int) -> Int {
  let n = if len > buffer_size { buffer_size } else { len }
  for i = 0; i < n; i = i + 1 {
    let b = in_buf[i].to_int()
    let shifted = if b >= 65 && b <= 90 {
      (b - 65 + 13) % 26 + 65
    } else if b >= 97 && b <= 122 {
      (b - 97 + 13) % 26 + 97
    } else {
      b
    }
    out_buf[i] = shifted.to_byte()
  }
  n
}

/// 単語数カウント (スペース・改行区切り)
#export_name("count_words")
pub fn count_words(len : Int) -> Int {
  let n = if len > buffer_size { buffer_size } else { len }
  let mut count = 0
  let mut in_word = false
  for i = 0; i < n; i = i + 1 {
    let b = in_buf[i].to_int()
    // 32: space, 9: tab, 10: LF, 13: CR
    let is_space = b == 32 || b == 9 || b == 10 || b == 13
    if !is_space && !in_word {
      in_word = true
      count = count + 1
    } else if is_space {
      in_word = false
    }
  }
  count
}

