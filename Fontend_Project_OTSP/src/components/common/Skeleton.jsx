import React from 'react';

/**
 * Skeleton Loader Component
 * Cung cấp hiệu ứng placeholder shimmer hiện đại, loại bỏ giật layout (CLS).
 */
export default function Skeleton({
  variant = 'text', // 'text' | 'circular' | 'rectangular' | 'card' | 'table'
  width,
  height,
  rows = 4,
  columns = 5,
  className = '',
  style = {}
}) {
  const baseStyle = {
    backgroundColor: 'var(--otsp-skeleton-bg, rgba(148, 163, 184, 0.18))',
    backgroundImage: 'linear-gradient(90deg, rgba(255,255,255,0) 0, rgba(255,255,255,0.2) 50%, rgba(255,255,255,0) 100%)',
    backgroundSize: '200% 100%',
    animation: 'otsp-shimmer 1.5s infinite linear',
    borderRadius: variant === 'circular' ? '50%' : '8px',
    display: 'inline-block',
    width: width || (variant === 'circular' ? '40px' : '100%'),
    height: height || (variant === 'circular' ? '40px' : variant === 'text' ? '1rem' : '120px'),
    ...style
  };

  if (variant === 'table') {
    return (
      <div className={`table-responsive ${className}`}>
        <table className="table align-middle mb-0">
          <thead>
            <tr>
              {Array.from({ length: columns }).map((_, cIdx) => (
                <th key={`th-${cIdx}`} className="py-3">
                  <div style={{ ...baseStyle, height: '14px', width: `${60 + (cIdx % 3) * 15}%` }} />
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: rows }).map((_, rIdx) => (
              <tr key={`tr-${rIdx}`}>
                {Array.from({ length: columns }).map((_, cIdx) => (
                  <td key={`td-${rIdx}-${cIdx}`} className="py-3">
                    <div style={{ ...baseStyle, height: '16px', width: `${50 + ((rIdx + cIdx) % 4) * 15}%` }} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  if (variant === 'card') {
    return (
      <div className={`card border shadow-sm rounded-4 p-3 ${className}`} style={{ backgroundColor: 'var(--bs-body-bg)' }}>
        <div style={{ ...baseStyle, height: '160px', borderRadius: '12px', marginBottom: '16px' }} />
        <div style={{ ...baseStyle, height: '20px', width: '70%', marginBottom: '10px' }} />
        <div style={{ ...baseStyle, height: '14px', width: '90%', marginBottom: '6px' }} />
        <div style={{ ...baseStyle, height: '14px', width: '50%', marginBottom: '16px' }} />
        <div className="d-flex justify-content-between align-items-center">
          <div style={{ ...baseStyle, height: '24px', width: '30%', borderRadius: '20px' }} />
          <div style={{ ...baseStyle, height: '32px', width: '25%', borderRadius: '8px' }} />
        </div>
      </div>
    );
  }

  return <div className={`otsp-skeleton ${className}`} style={baseStyle} />;
}

