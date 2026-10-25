(() => {
  const preloader = document.getElementById('preloader');
  if (!preloader) return;

  let isVisible = false;
  const showTimeout = window.setTimeout(() => {
    if (document.readyState !== 'complete') {
      isVisible = true;
      preloader.classList.add('visible');
    }
  }, 350);

  const dismissPreloader = () => {
    window.clearTimeout(showTimeout);
    if (!isVisible) {
      preloader.remove();
      return;
    }

    preloader.classList.remove('visible');
    preloader.classList.add('loaded');
    window.setTimeout(() => preloader.remove(), 300);
  };

  if (document.readyState === 'complete') {
    dismissPreloader();
  } else {
    window.addEventListener('load', dismissPreloader, { once: true });
  }
})();
