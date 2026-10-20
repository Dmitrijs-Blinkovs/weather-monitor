import { Component, type ErrorInfo, type ReactNode } from 'react';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  error: Error | null;
}

/**
 * Catches rendering errors and failed lazy page loads (for example an old chunk that no longer exists after a
 * deployment) so the user sees a message and a way out instead of a blank page.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled UI error', error, info.componentStack);
  }

  render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <div className="notice notice--error" role="alert">
        <span className="notice__icon" aria-hidden="true">
          !
        </span>
        <p className="notice__text">
          <strong>Something went wrong while showing this page.</strong> Reloading usually helps.
        </p>
        <button type="button" className="button" onClick={() => window.location.reload()}>
          Reload
        </button>
      </div>
    );
  }
}
