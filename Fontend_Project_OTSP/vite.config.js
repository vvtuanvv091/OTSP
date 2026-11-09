import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    target: 'es2020',
    cssCodeSplit: true,
    chunkSizeWarningLimit: 600,
    rolldownOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined;
          if (id.includes('@microsoft/signalr')) return 'vendor-signalr';
          if (id.includes('axios')) return 'vendor-http';
          if (id.includes('react-router') || id.includes('react-dom') || id.includes('node_modules/react/')) {
            return 'vendor-react';
          }
          return undefined;
        },
      },
    },
  },
  server: {
    port: 5173, // Giữ 5173 để khớp với Google OAuth đã đăng ký
    allowedHosts: ['.ngrok-free.app', '.ngrok.io', '.ngrok-free.dev'],
    proxy: {
      '/api': {
        target: 'http://localhost:5170',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: 'http://localhost:5170',
        ws: true,
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5170',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});