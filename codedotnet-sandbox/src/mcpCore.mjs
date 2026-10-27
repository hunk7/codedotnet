// Shared MCP-style JSON-RPC 2.0 dispatch core, reused by both the stdio transport
// (src/server.mjs) and the WebSocket transport (src/wsServer.mjs). Keeping the tool
// definitions and method handling in one place guarantees both transports expose
// identical behavior for initialize/tools/list/tools/call/shutdown.

import { DEFAULT_MAX_TIMEOUT_MS, clampTimeout } from './security.mjs'

export const TOOLS = [
  {
    name: 'compile_and_run',
    description:
      'Compiles and executes a single-file C# program using the curated CodeDotNet ' +
      'reference surface, inside a disposable, hard-timeout-enforced sandbox process. ' +
      'Returns diagnostics, captured stdout/stderr, exit code, and timing information.',
    inputSchema: {
      type: 'object',
      properties: {
        source: { type: 'string', description: 'C# top-level-statement program source.' },
        timeoutMs: {
          type: 'number',
          description: 'Hard execution timeout in milliseconds before the sandbox process is killed.',
          default: 10000,
        },
      },
      required: ['source'],
    },
  },
]

/**
 * Builds a handler for one JSON-RPC request against a given WorkerPool, calling back
 * with `send(message)` for every JSON-RPC response/error message that should be
 * delivered to the client. Transport-agnostic: callers decide how `send` serializes
 * and frames each message (newline-delimited stdout, WebSocket text frame, etc).
 */
export function createRequestHandler(pool, { onShutdown, maxTimeoutMs = DEFAULT_MAX_TIMEOUT_MS } = {}) {
  function sendResult(send, id, result) {
    send({ jsonrpc: '2.0', id, result })
  }

  function sendError(send, id, code, message) {
    send({ jsonrpc: '2.0', id, error: { code, message } })
  }

  return async function handleRequest(request, send) {
    const { id, method, params } = request ?? {}

    try {
      switch (method) {
        case 'initialize': {
          sendResult(send, id, {
            protocolVersion: '2024-11-05',
            serverInfo: { name: 'codedotnet-sandbox', version: '0.1.0' },
            capabilities: { tools: {} },
          })
          return
        }

        case 'tools/list': {
          sendResult(send, id, { tools: TOOLS })
          return
        }

        case 'tools/call': {
          const toolName = params?.name
          const args = params?.arguments ?? {}

          if (toolName !== 'compile_and_run') {
            sendError(send, id, -32602, `Unknown tool: ${toolName}`)
            return
          }

          if (typeof args.source !== 'string') {
            sendError(send, id, -32602, "Missing required string argument 'source'")
            return
          }

          const response = await pool.compileAndRun({
            source: args.source,
            timeoutMs: clampTimeout(args.timeoutMs, maxTimeoutMs),
          })

          sendResult(send, id, {
            content: [{ type: 'text', text: JSON.stringify(response) }],
            isError:
              response.status === 'BuildFailed' ||
              response.status === 'ExecutionFailed' ||
              response.status === 'TimedOut',
          })
          return
        }

        case 'shutdown': {
          sendResult(send, id, {})
          onShutdown?.()
          return
        }

        default:
          sendError(send, id, -32601, `Method not found: ${method}`)
      }
    } catch (err) {
      sendError(send, id, -32603, err && err.message ? err.message : String(err))
    }
  }
}
