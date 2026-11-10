import { Suspense } from 'react';
import { Outlet } from 'react-router-dom';
import Header from './Header';
import Footer from './Footer';

function LayoutPageFallback() {
  return (
    <div className="d-flex flex-column align-items-center justify-content-center py-5 text-secondary" style={{ minHeight: '45vh' }}>
      <span className="spinner-border spinner-border-sm text-danger mb-2" role="status" aria-hidden="true" />
      <span className="small">Đang tải dữ liệu trang…</span>
    </div>
  );
}

export default function MainLayout() {
  return (
    <>
      <Header />
      <main role="main">
        <Suspense fallback={<LayoutPageFallback />}>
          <Outlet />
        </Suspense>
      </main>
      <Footer />
    </>
  );
}

