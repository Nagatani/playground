///|
/// Step 1: 基本的な数値計算とアルゴリズム
/// MoonBit は静的型付け言語で、Int は 32bit 符号付き整数です。

/// 2つの整数の加算
#export_name("add")
pub fn add(a : Int, b : Int) -> Int {
  a + b
}

/// 再帰によるフィボナッチ数の計算 (CPU負荷ベンチマーク用)
#export_name("fib")
pub fn fib(n : Int) -> Int {
  if n <= 1 {
    n
  } else {
    fib(n - 1) + fib(n - 2)
  }
}

/// ループによる高速なフィボナッチ計算
#export_name("fib_fast")
pub fn fib_fast(n : Int) -> Int {
  if n <= 1 {
    return n
  }
  let mut prev = 0
  let mut curr = 1
  for i = 2; i <= n; i = i + 1 {
    let next = prev + curr
    prev = curr
    curr = next
  }
  curr
}

/// 素数判定 (1 = 素数, 0 = 合成数/非素数)
#export_name("is_prime")
pub fn is_prime(n : Int) -> Int {
  if n <= 1 {
    return 0
  }
  if n <= 3 {
    return 1
  }
  if n % 2 == 0 || n % 3 == 0 {
    return 0
  }
  let mut i = 5
  while i * i <= n {
    if n % i == 0 || n % (i + 2) == 0 {
      return 0
    }
    i = i + 6
  }
  1
}

/// コラッツ予想のステップ数 (1 に到達するまでの試行回数)
#export_name("collatz")
pub fn collatz(n : Int) -> Int {
  let mut current = n
  let mut steps = 0
  while current > 1 {
    if current % 2 == 0 {
      current = current / 2
    } else {
      current = 3 * current + 1
    }
    steps = steps + 1
  }
  steps
}

