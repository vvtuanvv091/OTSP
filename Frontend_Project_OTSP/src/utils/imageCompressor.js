/**
 * Tiện ích nén ảnh phía client (Client-side Image Compressor)
 * Giúp tối ưu tốc độ tải lên máy chủ lên tới 10-20 lần bằng cách nén ảnh
 * từ 5-15 MB (ảnh chụp smartphone) xuống còn 200-400 KB mà vẫn giữ nguyên độ nét chuẩn HD (1920px)
 * để AI OCR có thể đọc chuẩn từng chữ số, con dấu và thông tin bằng cấp.
 */

export async function compressImageFile(file, options = {}) {
  const {
    maxWidth = 1920,
    maxHeight = 1920,
    quality = 0.82,
    maxSizeBytes = 500 * 1024, // 500 KB
  } = options;

  if (!file || !(file instanceof File)) return file;

  // Nếu là file PDF hoặc không phải ảnh, không nén
  const fileType = file.type?.toLowerCase() || '';
  const fileName = file.name?.toLowerCase() || '';
  if (fileType.includes('pdf') || fileName.endsWith('.pdf')) {
    return file;
  }

  // Nếu file ảnh đã nhỏ hơn maxSizeBytes, không cần nén
  if (file.size <= maxSizeBytes) {
    return file;
  }

  return new Promise((resolve) => {
    try {
      const reader = new FileReader();
      reader.onload = (e) => {
        const img = new Image();
        img.onload = () => {
          try {
            let { width, height } = img;

            // Tính toán kích thước mới giữ nguyên tỉ lệ aspect ratio
            if (width > maxWidth || height > maxHeight) {
              const ratio = Math.min(maxWidth / width, maxHeight / height);
              width = Math.round(width * ratio);
              height = Math.round(height * ratio);
            }

            const canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;

            const ctx = canvas.getContext('2d');
            if (!ctx) {
              resolve(file);
              return;
            }

            // Vẽ nền trắng (phòng trường hợp png trong suốt chuyển sang jpeg không bị đen nền)
            ctx.fillStyle = '#FFFFFF';
            ctx.fillRect(0, 0, width, height);
            ctx.drawImage(img, 0, 0, width, height);

            // Xuất ra dạng JPEG chất lượng cao
            canvas.toBlob(
              (blob) => {
                if (!blob || blob.size >= file.size) {
                  // Nếu sau nén không nhẹ hơn thì giữ file gốc
                  resolve(file);
                  return;
                }

                const baseName = file.name.substring(0, file.name.lastIndexOf('.')) || file.name;
                const newFileName = `${baseName}.jpg`;
                const compressedFile = new File([blob], newFileName, {
                  type: 'image/jpeg',
                  lastModified: Date.now(),
                });

                console.info(
                  `[ImageCompressor] Đã nén "${file.name}": ${(file.size / 1024).toFixed(0)}KB -> ${(compressedFile.size / 1024).toFixed(0)}KB (giảm ${(100 - (compressedFile.size / file.size) * 100).toFixed(0)}%)`
                );

                resolve(compressedFile);
              },
              'image/jpeg',
              quality
            );
          } catch (canvasErr) {
            console.warn('[ImageCompressor] Canvas error, fallback to original:', canvasErr);
            resolve(file);
          }
        };

        img.onerror = () => resolve(file);
        img.src = e.target.result;
      };

      reader.onerror = () => resolve(file);
      reader.readAsDataURL(file);
    } catch (err) {
      console.warn('[ImageCompressor] Compression exception, fallback to original:', err);
      resolve(file);
    }
  });
}

export default compressImageFile;

