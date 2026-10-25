interface StatusBadgeProps {
  success: boolean;
}

/** Status is never carried by colour alone: every badge has an icon and a label. */
export function StatusBadge({ success }: StatusBadgeProps) {
  return success ? (
    <span className="status-badge status-badge--good">
      <span aria-hidden="true">✓</span> Success
    </span>
  ) : (
    <span className="status-badge status-badge--critical">
      <span aria-hidden="true">✕</span> Failed
    </span>
  );
}
