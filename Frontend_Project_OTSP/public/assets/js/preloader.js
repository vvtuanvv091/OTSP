(() => {
  const preloader = document.getElementById('preloader');
  if (!preloader) return;

  // Thời gian ngưỡng: Nếu tải trang quá 3s thì mới hiện preloader
  const DELAY_SHOW_MS = 3000;
  let isPageLoaded = false;
  let showTimer = null;

  const dismissPreloader = () => {
    isPageLoaded = true;
    if (showTimer) {
      clearTimeout(showTimer);
      showTimer = null;
    }

    // Nếu preloader đã được kích hoạt hiện lên (do tải quá 3s)
    if (preloader.classList.contains('show')) {
      preloader.classList.remove('show');
      preloader.classList.add('loaded');
      setTimeout(() => {
        if (preloader.parentNode) {
          preloader.remove();
        }
      }, 350);
    } else {
      // Nếu tải xong dưới 3s, hủy bỏ và xóa luôn preloader mà không cần hiển thị
      if (preloader.parentNode) {
        preloader.remove();
      }
    }
  };

  // Hẹn giờ: Chỉ khi tải trang quá 3 giây (3000ms) mà chưa xong mới kích hoạt hiện preloader
  showTimer = setTimeout(() => {
    if (!isPageLoaded && document.readyState !== 'complete') {
      preloader.classList.add('show');
    }
  }, DELAY_SHOW_MS);

  // Khi trang tải xong (toàn bộ DOM, ảnh, scripts)
  if (document.readyState === 'complete') {
    dismissPreloader();
  } else {
    window.addEventListener('load', dismissPreloader, { once: true });
  }

  // Giới hạn an toàn tối đa 12s nếu trang bị nghẽn mạng bên thứ 3
  setTimeout(dismissPreloader, 12000);
})();
