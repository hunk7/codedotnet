import { Component, type ErrorInfo, type ReactNode } from 'react'

export interface ErrorBoundaryProps {
  /** Label used in the fallback UI and console diagnostics (e.g. "Editor", "I/O pane"). */
  label: string
  children: ReactNode
}

interface ErrorBoundaryState {
  error: Error | null
}

/**
 * Isolates rendering failures in a subtree (editor, I/O pane, settings panel) so that a crash
 * in one part of the shell doesn't take down the whole app. Provides a "Try again" action that
 * resets the boundary's error state and re-renders the children.
 */
class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // eslint-disable-next-line no-console
    console.error(`[${this.props.label}] rendering error:`, error, info.componentStack)
  }

  private reset = () => {
    this.setState({ error: null })
  }

  render() {
    if (this.state.error) {
      return (
        <div className="error-boundary" role="alert">
          <p>
            <strong>{this.props.label}</strong> failed to render: {this.state.error.message}
          </p>
          <button type="button" onClick={this.reset}>
            Try again
          </button>
        </div>
      )
    }

    return this.props.children
  }
}

export default ErrorBoundary
