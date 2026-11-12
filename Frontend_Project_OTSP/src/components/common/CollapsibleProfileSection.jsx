import '../../styles/profile-collapse.css';

export default function CollapsibleProfileSection({ label, children, defaultOpen = true, className = '' }) {
  return (
    <details className={`profile-collapsible ${className}`.trim()} open={defaultOpen}>
      <summary className="profile-collapsible__summary">
        <span className="profile-collapsible__expanded">
          Thu gọn <i className="bi bi-chevron-up ms-1" aria-hidden="true" />
        </span>
        <span className="profile-collapsible__collapsed">
          <strong>{label}</strong>
          <span>Mở rộng <i className="bi bi-chevron-down ms-1" aria-hidden="true" /></span>
        </span>
      </summary>
      {children}
    </details>
  );
}