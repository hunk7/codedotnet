/**
 * Lightweight, purely static heuristic for estimating the Big-O time/space complexity of the
 * C# source currently in the editor. This is NOT a real static analyzer - it does not run the
 * Roslyn compiler or inspect the actual control-flow graph. It uses structural signals per
 * method (loop nesting depth, recursion shape, memoization, divide-and-conquer/halving
 * patterns, LINQ usage, hash-based lookups, and collection allocations) to produce a
 * best-effort estimate good enough for a quick sanity check, similar to what a reviewer might
 * eyeball from the code shape alone.
 */

export interface ComplexityEstimate {
  time: string
  space: string
  rationale: string
}

const LOOP_KEYWORD_RE = /\b(for|foreach|while)\s*\(/g
const SORT_CALL_RE = /\.(Sort|OrderBy|OrderByDescending|ThenBy|ThenByDescending)\s*\(/g
const LINQ_SCAN_RE =
  /\.(Where|Select|SelectMany|Any|All|Count|Sum|Average|Min|Max|First|FirstOrDefault|Single|SingleOrDefault|ToList|ToArray|ToDictionary|ToHashSet|Aggregate|Reverse|Distinct|GroupBy)\s*\(/g
const CONTAINS_LOOKUP_RE = /\.(Contains|IndexOf|LastIndexOf|Find|FindIndex)\s*\(/g
const HASH_LOOKUP_TYPE_RE = /\b(Dictionary|HashSet|ConcurrentDictionary|ConcurrentBag)\s*</
const LIST_ARRAY_LOOKUP_TYPE_RE = /\b(List|IList|IEnumerable|Array)\s*</
const METHOD_DECL_RE =
  /\b(?:public|private|protected|internal|static)(?:\s+(?:static|virtual|override|sealed|async|readonly))*\s+[\w<>[\],.?]+\s+(\w+)\s*\(([^)]*)\)\s*(?::[^{]*)?\{/g
const HALVING_RE =
  /\b(?:mid|low|high|left|right|lo|hi|start|end|l|r)\s*(?:\/=\s*2|>>=\s*1|=\s*[\w.]+\s*\/\s*2\b)|\b\w+\s*=\s*\(\s*\w+\s*\+\s*\w+\s*\)\s*\/\s*2\b/
const DOUBLING_RE = /\b(?:i|j|k|n|size|count)\s*(?:\*=\s*2|<<=\s*1|=\s*\w+\s*\*\s*2\b)/
const MEMO_TYPE_HINT_RE = /\b(?:memo|cache|dp|seen|visited)\b/i
const COLLECTION_ALLOC_RE =
  /\bnew\s+(List|Dictionary|HashSet|Queue|Stack|SortedSet|SortedDictionary|ConcurrentDictionary|ConcurrentBag|StringBuilder|\w+\[\])/g
const NESTED_LOOP_LOOKUP_HINT_RE = /\.(Contains|ContainsKey|ContainsValue)\s*\(/

interface MethodInfo {
  name: string
  params: string
  body: string
  loopDepth: number
  loopCount: number
  recursiveCallCount: number
  hasMemoization: boolean
  hasHalving: boolean
  hasDoubling: boolean
  usesSort: boolean
  usesLinqScan: boolean
  usesHashLookupInLoop: boolean
  usesLinearLookupInLoop: boolean
  allocatesCollectionsInLoop: boolean
  allocatesCollections: boolean
}

/** Strips line/block comments and string literal contents so keyword scans do not false-positive. */
function stripCommentsAndStrings(source: string): string {
  return source
    .replace(/\/\/.*$/gm, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/"(?:[^"\\]|\\.)*"/g, '""')
    .replace(/'(?:[^'\\]|\\.)*'/g, "''")
}

/** Finds the end index of the brace block that starts at `openBraceIndex`. */
function findMatchingBrace(code: string, openBraceIndex: number): number {
  let depth = 0
  for (let i = openBraceIndex; i < code.length; i++) {
    if (code[i] === '{') depth++
    else if (code[i] === '}') {
      depth--
      if (depth === 0) return i
    }
  }
  return -1
}

/** Finds the end index of the parenthesized group that starts at `openParenIndex`. */
function findMatchingParen(code: string, openParenIndex: number): number {
  let depth = 0
  for (let i = openParenIndex; i < code.length; i++) {
    if (code[i] === '(') depth++
    else if (code[i] === ')') {
      depth--
      if (depth === 0) return i
    }
  }
  return -1
}

/** Skips whitespace (including newlines) starting at index `i`, returning the next non-whitespace index. */
function skipWhitespace(code: string, i: number): number {
  while (i < code.length && /\s/.test(code[i])) i++
  return i
}

/**
 * Finds the end index (inclusive) of the single statement/block that starts at `start`. Handles
 * plain statements (up to the next top-level `;`), braced blocks (`{ ... }`), and single-statement
 * control constructs (`for`/`foreach`/`while`/`if`/`else` without braces) by recursing into their
 * body, so a non-braced loop whose body is itself another non-braced loop is still resolved to
 * its true extent (e.g. `for (...) for (...) Console.Write(...);`).
 */
function findStatementEnd(code: string, start: number): number {
  const i = skipWhitespace(code, start)
  if (i >= code.length) return code.length - 1

  if (code[i] === '{') {
    const close = findMatchingBrace(code, i)
    return close === -1 ? code.length - 1 : close
  }

  const controlMatch = /^(for|foreach|while|if|else)\b\s*/.exec(code.slice(i))
  if (controlMatch) {
    const keyword = controlMatch[1]
    let afterKeyword = i + controlMatch[0].length

    if (keyword === 'else') {
      // `else if (...)` behaves like another condition; otherwise the body follows directly.
      return findStatementEnd(code, afterKeyword)
    }

    if (code[afterKeyword] === '(') {
      const closeParen = findMatchingParen(code, afterKeyword)
      afterKeyword = closeParen === -1 ? afterKeyword : closeParen + 1
    }

    const bodyEnd = findStatementEnd(code, afterKeyword)

    if (keyword === 'if') {
      const afterBody = skipWhitespace(code, bodyEnd + 1)
      if (/^else\b/.test(code.slice(afterBody))) {
        return findStatementEnd(code, afterBody)
      }
    }

    return bodyEnd
  }

  // Plain statement: scan to the next top-level `;` (not nested inside (), {}, or []).
  let depth = 0
  for (let j = i; j < code.length; j++) {
    const c = code[j]
    if (c === '(' || c === '{' || c === '[') depth++
    else if (c === ')' || c === '}' || c === ']') depth--
    else if (c === ';' && depth === 0) return j
  }
  return code.length - 1
}

/**
 * Locates every for/foreach/while loop body in `code` and returns their [bodyStart, bodyEnd]
 * index intervals (covering both braced `{ ... }` bodies and single-statement bodies), sorted by
 * start position. Unlike a fixed-width text lookbehind, this walks the actual `(...)` header
 * (which may be arbitrarily long/nested) to find where the body starts, so long loop conditions
 * or nested parens never cause a loop to be missed. Single-statement loop bodies are resolved via
 * `findStatementEnd` so nested loops without braces (e.g. `for (...) for (...) Foo();`) are still
 * detected as nested. The trailing `while (cond);` of a do-while is excluded since it has no body.
 */
function findLoopBodyIntervals(code: string): Array<[number, number]> {
  const intervals: Array<[number, number]> = []
  const re = /\b(for|foreach|while)\s*\(/g
  let match: RegExpExecArray | null
  while ((match = re.exec(code))) {
    const openParen = code.indexOf('(', match.index)
    if (openParen === -1) continue
    const closeParen = findMatchingParen(code, openParen)
    if (closeParen === -1) continue

    const bodyStart = skipWhitespace(code, closeParen + 1)
    if (bodyStart >= code.length || code[bodyStart] === ';') continue // do-while's trailing `while (cond);`

    const bodyEnd = findStatementEnd(code, bodyStart)
    intervals.push([bodyStart, bodyEnd])
  }
  intervals.sort((a, b) => a[0] - b[0])
  return intervals
}

/** Finds the maximum nesting depth of for/foreach/while loops using their actual body intervals. */
function maxLoopNestingDepth(code: string): number {
  const intervals = findLoopBodyIntervals(code)
  let maxDepth = 0
  const openStack: number[] = [] // body-end indices of currently-open ancestor loops

  for (const [open, close] of intervals) {
    while (openStack.length > 0 && openStack[openStack.length - 1] < open) {
      openStack.pop()
    }
    openStack.push(close)
    maxDepth = Math.max(maxDepth, openStack.length)
  }

  return maxDepth
}

/** Extracts the source text belonging to the innermost loop bodies (used to check what happens inside loops). */
function extractLoopBodies(code: string): string[] {
  return findLoopBodyIntervals(code).map(([open, close]) =>
    code[open] === '{' ? code.slice(open + 1, close) : code.slice(open, close + 1),
  )
}

/** Splits the whole file into per-method chunks (name, params, body) using brace matching. */
function extractMethods(code: string): MethodInfo[] {
  const methods: MethodInfo[] = []
  METHOD_DECL_RE.lastIndex = 0
  let match: RegExpExecArray | null
  while ((match = METHOD_DECL_RE.exec(code))) {
    const name = match[1]
    const params = match[2] ?? ''
    if (
      !name ||
      ['if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock'].includes(name)
    )
      continue
    const openBrace = code.indexOf('{', match.index)
    if (openBrace === -1) continue
    const closeBrace = findMatchingBrace(code, openBrace)
    if (closeBrace === -1) continue
    const body = code.slice(openBrace + 1, closeBrace)

    const selfCallRe = new RegExp(`\\b${name}\\s*\\(`, 'g')
    const recursiveCallCount = (body.match(selfCallRe) || []).length

    const loopBodies = extractLoopBodies(body)
    const loopBodyText = loopBodies.join('\n')

    methods.push({
      name,
      params,
      body,
      loopDepth: maxLoopNestingDepth(body),
      loopCount: (body.match(LOOP_KEYWORD_RE) || []).length,
      recursiveCallCount,
      hasMemoization:
        MEMO_TYPE_HINT_RE.test(body) && (HASH_LOOKUP_TYPE_RE.test(body) || /\[.+\]\s*=/.test(body)),
      hasHalving: HALVING_RE.test(body),
      hasDoubling: DOUBLING_RE.test(body),
      usesSort: SORT_CALL_RE.test(body),
      usesLinqScan: LINQ_SCAN_RE.test(body),
      usesHashLookupInLoop:
        loopBodies.length > 0 &&
        HASH_LOOKUP_TYPE_RE.test(loopBodyText) &&
        NESTED_LOOP_LOOKUP_HINT_RE.test(loopBodyText),
      usesLinearLookupInLoop:
        loopBodies.length > 0 &&
        LIST_ARRAY_LOOKUP_TYPE_RE.test(loopBodyText) &&
        CONTAINS_LOOKUP_RE.test(loopBodyText),
      allocatesCollectionsInLoop: loopBodies.some((b) => COLLECTION_ALLOC_RE.test(b)),
      allocatesCollections: COLLECTION_ALLOC_RE.test(body),
    })
    LOOP_KEYWORD_RE.lastIndex = 0
    SORT_CALL_RE.lastIndex = 0
    LINQ_SCAN_RE.lastIndex = 0
    COLLECTION_ALLOC_RE.lastIndex = 0
  }
  return methods
}

/** Classifies a single method's recursion shape and returns a time-complexity contribution. */
function classifyRecursion(method: MethodInfo): { time: string; rationale: string } | null {
  if (method.recursiveCallCount === 0) return null

  if (method.hasMemoization) {
    return {
      time: 'O(n) / O(n^2) (memoized)',
      rationale: `'${method.name}' recurses but caches results (memo/dp/cache pattern), collapsing repeated subproblems to roughly polynomial time.`,
    }
  }

  if (method.recursiveCallCount >= 2 && (method.hasHalving || /\bmid\b/.test(method.body))) {
    return {
      time: 'O(n log n)',
      rationale: `'${method.name}' makes multiple recursive calls on halved ranges (divide-and-conquer, e.g. merge/quick sort pattern).`,
    }
  }

  if (method.recursiveCallCount === 1 && method.hasHalving) {
    return {
      time: 'O(log n)',
      rationale: `'${method.name}' recurses once per call while halving its range (binary-search-like pattern).`,
    }
  }

  if (method.recursiveCallCount >= 2) {
    return {
      time: 'O(2^n) (exponential branching)',
      rationale: `'${method.name}' makes ${method.recursiveCallCount} recursive calls per invocation without memoization (tree recursion, e.g. naive Fibonacci).`,
    }
  }

  if (method.loopDepth >= 1) {
    return {
      time: `O(n^${method.loopDepth + 1})`,
      rationale: `'${method.name}' combines single recursion with ${method.loopDepth} level(s) of looping in its body.`,
    }
  }

  return {
    time: 'O(n)',
    rationale: `'${method.name}' uses simple single-branch (linear/tail) recursion.`,
  }
}

/** Converts a loop nesting depth + modifiers within one method into a time-complexity contribution. */
function classifyLoops(method: MethodInfo): { rank: number; time: string; rationale: string } {
  const { loopDepth, loopCount } = method

  if (loopDepth === 0) {
    if (method.usesSort)
      return {
        rank: 1,
        time: 'O(n log n)',
        rationale: `'${method.name}' calls Sort/OrderBy with no explicit loops.`,
      }
    if (method.usesLinqScan)
      return {
        rank: 0.5,
        time: 'O(n)',
        rationale: `'${method.name}' uses LINQ (Where/Select/etc.), each a single O(n) pass.`,
      }
    return {
      rank: 0,
      time: 'O(1)',
      rationale: `'${method.name}' has no loops or recursion; runs in constant time.`,
    }
  }

  if (loopDepth === 1 && (method.hasHalving || method.hasDoubling)) {
    return {
      rank: 0.7,
      time: 'O(log n)',
      rationale: `'${method.name}' has a single loop whose counter halves/doubles each iteration.`,
    }
  }

  if (loopDepth >= 1 && method.usesHashLookupInLoop) {
    return {
      rank: loopDepth,
      time: loopDepth === 1 ? 'O(n)' : `O(n^${loopDepth - 1})`,
      rationale: `'${method.name}' nests ${loopDepth} loop(s) but uses a Dictionary/HashSet lookup to avoid an inner linear scan.`,
    }
  }

  if (loopDepth >= 1 && method.usesLinearLookupInLoop) {
    return {
      rank: loopDepth + 1,
      time: `O(n^${loopDepth + 1})`,
      rationale: `'${method.name}' nests ${loopDepth} loop(s) and also calls Contains/IndexOf on a List/Array inside the loop, adding another linear scan.`,
    }
  }

  if (loopDepth >= 3) {
    return {
      rank: loopDepth,
      time: `O(n^${loopDepth})`,
      rationale: `'${method.name}' has ${loopDepth} levels of nested loops.`,
    }
  }

  if (loopDepth === 2) {
    const withSort = method.usesSort ? ' plus a Sort/OrderBy call' : ''
    return {
      rank: 2,
      time: 'O(n^2)',
      rationale: `'${method.name}' has two nested loops${withSort}.`,
    }
  }

  // loopDepth === 1
  if (method.usesSort) {
    return {
      rank: 1.5,
      time: 'O(n log n)',
      rationale: `'${method.name}' has a single loop alongside a Sort/OrderBy call.`,
    }
  }
  return {
    rank: 1,
    time: 'O(n)',
    rationale:
      loopCount > 1
        ? `'${method.name}' has ${loopCount} sequential (non-nested) loops.`
        : `'${method.name}' has a single loop.`,
  }
}

/** Assigns a comparable numeric rank to a complexity time-string so the worst offender across methods can win. */
function rankOf(time: string): number {
  if (time.startsWith('O(2^n)')) return 100
  if (time.startsWith('O(n^')) {
    const match = /O\(n\^(\d+)/.exec(time)
    return match ? 10 + Number(match[1]) : 50
  }
  if (time.startsWith('O(n log n)')) return 5
  if (time === 'O(n)' || time.startsWith('O(n)')) return 4
  if (time.startsWith('O(log n)')) return 2
  if (time.startsWith('O(n) / O(n^2)')) return 8
  return 1 // O(1)
}

/**
 * Produces a best-effort Big-O estimate for the given C# source (developer-facing insight
 * pane). Analyzes each method individually (loop nesting, recursion shape, memoization,
 * divide-and-conquer/halving, LINQ, and hash vs. linear lookups) and reports the worst-case
 * contributor found. Falls back to O(1)/O(1) for trivial programs with no loops or recursion.
 */
export function estimateComplexity(source: string): ComplexityEstimate {
  if (!source || !source.trim()) {
    return { time: 'O(1)', space: 'O(1)', rationale: 'No code to analyze.' }
  }

  const code = stripCommentsAndStrings(source)
  const methods = extractMethods(code)

  // Fallback for scripts / top-level statements with no method declarations detected.
  const analysisTargets: MethodInfo[] =
    methods.length > 0
      ? methods
      : [
          {
            name: 'Main',
            params: '',
            body: code,
            loopDepth: maxLoopNestingDepth(code),
            loopCount: (code.match(LOOP_KEYWORD_RE) || []).length,
            recursiveCallCount: 0,
            hasMemoization: false,
            hasHalving: HALVING_RE.test(code),
            hasDoubling: DOUBLING_RE.test(code),
            usesSort: SORT_CALL_RE.test(code),
            usesLinqScan: LINQ_SCAN_RE.test(code),
            usesHashLookupInLoop:
              HASH_LOOKUP_TYPE_RE.test(code) && NESTED_LOOP_LOOKUP_HINT_RE.test(code),
            usesLinearLookupInLoop:
              LIST_ARRAY_LOOKUP_TYPE_RE.test(code) && CONTAINS_LOOKUP_RE.test(code),
            allocatesCollectionsInLoop: extractLoopBodies(code).some((b) =>
              COLLECTION_ALLOC_RE.test(b),
            ),
            allocatesCollections: COLLECTION_ALLOC_RE.test(code),
          },
        ]

  let best: { rank: number; time: string; rationale: string } | null = null

  for (const method of analysisTargets) {
    const recursionResult = classifyRecursion(method)
    const loopResult = classifyLoops(method)

    const candidates = [
      recursionResult
        ? {
            rank: rankOf(recursionResult.time),
            time: recursionResult.time,
            rationale: recursionResult.rationale,
          }
        : null,
      { rank: loopResult.rank, time: loopResult.time, rationale: loopResult.rationale },
    ].filter((c): c is { rank: number; time: string; rationale: string } => c !== null)

    for (const candidate of candidates) {
      if (!best || candidate.rank > best.rank) {
        best = candidate
      }
    }
  }

  const time = best?.time ?? 'O(1)'
  const rationale =
    best?.rationale ?? 'No loops or recursion detected; execution appears to run in constant time.'

  // Space: driven by recursion depth (call stack), memoization structures, and heap allocations
  // that scale with input, taking the worst signal found across all analyzed methods.
  let spaceRank = 0
  let spaceReason = 'No structures or recursion that scale with input were detected.'

  for (const method of analysisTargets) {
    if (method.recursiveCallCount > 0 && method.hasMemoization && spaceRank < 2) {
      spaceRank = 2
      spaceReason = `'${method.name}' uses a memoization cache that grows with the input (O(n) or more entries).`
    } else if (method.recursiveCallCount > 0 && spaceRank < 1) {
      spaceRank = 1
      spaceReason = `'${method.name}' recurses, so the call stack grows with input size/depth.`
    }
    if (
      (method.allocatesCollectionsInLoop ||
        (method.allocatesCollections && method.loopDepth >= 1)) &&
      spaceRank < 2
    ) {
      spaceRank = 2
      spaceReason = `'${method.name}' allocates a collection whose size scales with the loop/input.`
    } else if (method.allocatesCollections && spaceRank < 1) {
      spaceRank = Math.max(spaceRank, 1)
      spaceReason =
        spaceRank === 1
          ? `'${method.name}' allocates a collection sized by the input.`
          : spaceReason
    }
  }

  const space = spaceRank >= 2 ? 'O(n)' : spaceRank === 1 ? 'O(n)' : 'O(1)'

  return {
    time,
    space,
    rationale: `${rationale} ${spaceReason} (heuristic static estimate, not a guarantee — actual complexity depends on runtime data).`,
  }
}
