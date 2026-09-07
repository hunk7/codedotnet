/**
 * Lightweight, purely static heuristic for estimating the Big-O time/space complexity of the
 * C# source currently in the editor. This is NOT a real static analyzer - it does not run the
 * Roslyn compiler or inspect the actual control-flow graph. It uses simple structural signals
 * (loop nesting depth, recursion, and common O(n log n) / collection patterns) to produce a
 * best-effort estimate good enough for a quick sanity check, similar to what a reviewer might
 * eyeball from the code shape alone.
 */

export interface ComplexityEstimate {
  time: string
  space: string
  rationale: string
}

const LOOP_KEYWORD_RE = /\b(for|foreach|while)\s*\(/g
const SORT_CALL_RE = /\.(Sort|OrderBy|OrderByDescending)\s*\(/
const RECURSION_HINT_RE = /\b(static|public|private|protected|internal)[^{;]*\b(\w+)\s*\([^)]*\)\s*(?::[^{]*)?\{/g
const COLLECTION_ALLOC_RE = /\bnew\s+(List|Dictionary|HashSet|Queue|Stack|SortedSet|SortedDictionary|ConcurrentDictionary|StringBuilder|\w+\[\])/

/** Strips line/block comments and string literal contents so keyword scans do not false-positive. */
function stripCommentsAndStrings(source: string): string {
  return source
    .replace(/\/\/.*$/gm, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/"(?:[^"\\]|\\.)*"/g, '""')
    .replace(/'(?:[^'\\]|\\.)*'/g, "''")
}

/** Finds the maximum nesting depth of for/foreach/while loops via a brace-depth walk. */
function maxLoopNestingDepth(code: string): number {
  let depth = 0
  let maxDepth = 0
  const loopStack: number[] = []

  for (let i = 0; i < code.length; i++) {
    const char = code[i]

    if (char === '{') {
      // Check whether the *previous* token sequence up to this brace opened a loop.
      const precedingSlice = code.slice(Math.max(0, i - 40), i)
      if (/\b(for|foreach|while)\s*\([^{]*$/.test(precedingSlice)) {
        loopStack.push(depth + 1)
      }
      depth++
      if (loopStack.length > 0) {
        maxDepth = Math.max(maxDepth, loopStack.length)
      }
    } else if (char === '}') {
      if (loopStack.length > 0 && loopStack[loopStack.length - 1] === depth) {
        loopStack.pop()
      }
      depth = Math.max(0, depth - 1)
    }
  }

  return maxDepth
}

/** Detects simple direct recursion: a method that calls itself by name within its own body. */
function hasDirectRecursion(code: string): boolean {
  RECURSION_HINT_RE.lastIndex = 0
  let match: RegExpExecArray | null
  while ((match = RECURSION_HINT_RE.exec(code))) {
    const name = match[2]
    if (!name || name.length < 2) continue
    // Find this method's body by scanning forward from the match to its matching closing brace.
    const bodyStart = code.indexOf('{', match.index)
    if (bodyStart === -1) continue
    let depth = 0
    let bodyEnd = -1
    for (let i = bodyStart; i < code.length; i++) {
      if (code[i] === '{') depth++
      else if (code[i] === '}') {
        depth--
        if (depth === 0) {
          bodyEnd = i
          break
        }
      }
    }
    if (bodyEnd === -1) continue
    const body = code.slice(bodyStart + 1, bodyEnd)
    const selfCallRe = new RegExp(`\\b${name}\\s*\\(`)
    if (selfCallRe.test(body)) {
      return true
    }
  }
  return false
}

/**
 * Produces a best-effort Big-O estimate for the given C# source (FR: developer-facing
 * insight pane). Falls back to O(1)/O(1) for trivial programs with no loops or recursion.
 */
export function estimateComplexity(source: string): ComplexityEstimate {
  if (!source || !source.trim()) {
    return { time: 'O(1)', space: 'O(1)', rationale: 'No code to analyze.' }
  }

  const code = stripCommentsAndStrings(source)
  const loopDepth = maxLoopNestingDepth(code)
  const recursive = hasDirectRecursion(code)
  const usesSort = SORT_CALL_RE.test(code)
  const allocatesCollections = COLLECTION_ALLOC_RE.test(code)
  const loopCount = (code.match(LOOP_KEYWORD_RE) || []).length

  let time: string
  let rationale: string

  if (recursive && loopDepth >= 1) {
    time = 'O(n^k) or worse'
    rationale = 'Detected recursion combined with loops; complexity depends on branching factor and depth.'
  } else if (recursive) {
    time = 'O(2^n) / O(n) (depends on recursion pattern)'
    rationale = 'Detected a recursive method; estimate varies widely between linear and exponential recursion.'
  } else if (loopDepth >= 3) {
    time = `O(n^${loopDepth})`
    rationale = `Detected ${loopDepth} levels of nested loops.`
  } else if (loopDepth === 2) {
    time = 'O(n^2)'
    rationale = 'Detected two nested loops.'
  } else if (loopDepth === 1 && usesSort) {
    time = 'O(n log n)'
    rationale = 'Detected a single loop alongside a Sort/OrderBy call.'
  } else if (loopDepth === 1) {
    time = 'O(n)'
    rationale = loopCount > 1 ? `Detected ${loopCount} sequential (non-nested) loops.` : 'Detected a single loop.'
  } else if (usesSort) {
    time = 'O(n log n)'
    rationale = 'Detected a Sort/OrderBy call with no explicit loops.'
  } else {
    time = 'O(1)'
    rationale = 'No loops or recursion detected; execution appears to run in constant time.'
  }

  let space: string
  if (recursive) {
    space = 'O(n)'
  } else if (allocatesCollections && loopDepth >= 1) {
    space = 'O(n)'
  } else if (allocatesCollections) {
    space = 'O(n)'
  } else {
    space = 'O(1)'
  }

  return { time, space, rationale: `${rationale} (heuristic estimate, not a guarantee)` }
}
