import { Outlet } from 'react-router-dom';
import Header from './Header';
import Footer from './Footer';

export default function MainLayout() {
  return (
    <>
      <Header />
      <main role="main">
        <Outlet />
      </main>
      <Footer />
    </>
  );
}

