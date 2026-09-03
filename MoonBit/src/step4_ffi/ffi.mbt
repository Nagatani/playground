///|
/// Step 4: JavaScript FFI (Foreign Function Interface)
/// MoonBit から ホスト (JS) の関数をインポートして呼び出す

fn to_ptr(arr : FixedArray[Byte]) -> Int = "%identity"

fn js_log_number(tag : Int, val : Int) -> Unit = "env" "js_log_number"
fn js_log_string(ptr : Int, len : Int) -> Unit = "env" "js_log_string"
fn js_notify(event_id : Int) -> Unit = "env" "js_notify"
fn js_get_time() -> Double = "env" "js_get_time"

let msg_buf : FixedArray[Byte] = FixedArray::make(256, b"\x00"[0])

#export_name("get_msg_buf_ptr")
pub fn get_msg_buf_ptr() -> Int {
  to_ptr(msg_buf)
}

/// JS側のログ関数を呼び出すデモ
#export_name("run_ffi_calc")
pub fn run_ffi_calc(n : Int) -> Int {
  js_log_number(1, n)
  let start_time = js_get_time()
  
  let mut sum = 0
  for i = 1; i <= n; i = i + 1 {
    sum = sum + i
    if i % 5 == 0 {
      js_log_number(2, sum)
    }
  }
  
  let end_time = js_get_time()
  let elapsed = end_time - start_time
  js_log_number(3, elapsed.to_int())
  
  if sum > 100 {
    js_notify(99)
  }
  
  sum
}

/// メッセージをバッファに書き込み、JS側へ通知
#export_name("send_greeting")
pub fn send_greeting(user_id : Int) -> Unit {
  let prefix : Bytes = b"Greetings from MoonBit WASM! User #"
  let len = prefix.length()
  for i = 0; i < len; i = i + 1 {
    msg_buf[i] = prefix[i]
  }
  
  let id_char = (48 + (user_id % 10)).to_byte()
  msg_buf[len] = id_char
  let total_len = len + 1
  
  js_log_string(to_ptr(msg_buf), total_len)
}

