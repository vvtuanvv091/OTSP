import { useState, useEffect } from 'react';

/**
 * AnimatedCounter counts smoothly from 0 to end value when rendered or when value changes.
 */
export default function AnimatedCounter({ end = 0, duration = 1800, prefix = '', suffix = '' }) {
  const [count, setCount] = useState(0);

  useEffect(() => {
    const target = Number(end) || 0;
    let startTimestamp = null;
    let animationFrameId;

    if (target === 0) {
      animationFrameId = window.requestAnimationFrame(() => setCount(0));
      return () => window.cancelAnimationFrame(animationFrameId);
    }

    const step = (timestamp) => {
      if (!startTimestamp) startTimestamp = timestamp;
      const progress = Math.min((timestamp - startTimestamp) / duration, 1);
      // Hiệu ứng mượt mà (Ease out cubic)
      const easeProgress = 1 - Math.pow(1 - progress, 3);
      setCount(Math.floor(easeProgress * target));

      if (progress < 1) {
        animationFrameId = window.requestAnimationFrame(step);
      } else {
        setCount(target);
      }
    };

    animationFrameId = window.requestAnimationFrame(step);
    return () => window.cancelAnimationFrame(animationFrameId);
  }, [end, duration]);

  const formatted = new Intl.NumberFormat('vi-VN').format(count);

  return (
    <span
      className="purecounter"
      style={{
        display: 'inline-block',
        minHeight: '1.15em',
        minWidth: '2ch',
        fontVariantNumeric: 'tabular-nums',
      }}
    >
      {prefix}{formatted}{suffix}
    </span>
  );
}

