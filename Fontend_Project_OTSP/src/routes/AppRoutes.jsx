import { Suspense, lazy, useEffect, useRef } from 'react';
import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { API_BASE_URL } from '../api/config';

// Các bố cục giao diện (Layout)
import MainLayout from '../components/layout/MainLayout';
import TutorLayout from '../components/layout/TutorLayout';
import StudentLayout from '../components/layout/StudentLayout';
import AdminLayout from '../components/layout/AdminLayout';

// Bộ bảo vệ tuyến đường đăng nhập
import ProtectedRoute from './ProtectedRoute';

// Các trang công khai & xác thực tải trực tiếp để hiển thị ngay lập tức
import HomePage from '../features/home/HomePage';
import LoginPage from '../features/auth/LoginPage';
import RegisterPage from '../features/auth/RegisterPage';

// Tải trễ (lazy-load) các trang công khai & tính năng chung
const PrivacyPage = lazy(() => import('../features/home/PrivacyPage'));
const GuidePage = lazy(() => import('../features/home/InfoPages').then((m) => ({ default: m.GuidePage })));
const FaqPage = lazy(() => import('../features/home/InfoPages').then((m) => ({ default: m.FaqPage })));
const TermsPage = lazy(() => import('../features/home/InfoPages').then((m) => ({ default: m.TermsPage })));
const ContactPage = lazy(() => import('../features/home/InfoPages').then((m) => ({ default: m.ContactPage })));
const AboutCenterPage = lazy(() => import('../features/home/InfoPages').then((m) => ({ default: m.AboutCenterPage })));
const AccessDeniedPage = lazy(() => import('../features/home/AccessDeniedPage'));
const ForgotPasswordPage = lazy(() => import('../features/auth/ForgotPasswordPage'));
const ChangePasswordPage = lazy(() => import('../features/auth/ChangePasswordPage'));
const SearchPage = lazy(() => import('../features/search/SearchPage'));
const LearningDocumentPage = lazy(() => import('../features/documents/LearningDocumentPage'));
const NewsPage = lazy(() => import('../features/news/NewsPage'));
const NewsArticlePage = lazy(() => import('../features/news/NewsArticlePage'));
const ChatPage = lazy(() => import('../features/chat/ChatPage'));
const NotificationsPage = lazy(() => import('../features/notifications/NotificationsPage'));

// Tải trễ các trang tính năng dành cho Gia sư
const TutorDashboardPage = lazy(() => import('../features/tutor/TutorDashboardPage'));
const TutorProfilePage = lazy(() => import('../features/tutor/TutorProfilePage'));
const TutorSchedulePage = lazy(() => import('../features/tutor/TutorSchedulePage'));
const TutorConnectionRequestsPage = lazy(() => import('../features/tutor/TutorConnectionRequestsPage'));
const TutorClassroomsPage = lazy(() => import('../features/tutor/TutorClassroomsPage'));
const TutorClassroomFormPage = lazy(() => import('../features/tutor/TutorClassroomFormPage'));
const TutorAvailabilityPage = lazy(() => import('../features/tutor/TutorAvailabilityPage'));
const TutorStudentsPage = lazy(() => import('../features/tutor/TutorStudentsPage'));
const TutorSubjectsPage = lazy(() => import('../features/tutor/TutorSubjectsPage'));
const TutorDocumentsPage = lazy(() => import('../features/tutor/TutorDocumentsPage'));
const TutorReviewsPage = lazy(() => import('../features/tutor/TutorReviewsPage'));
const TutorIncomePage = lazy(() => import('../features/tutor/TutorIncomePage'));

// Tải trễ các trang tính năng dành cho Học viên
const StudentDashboardPage = lazy(() => import('../features/student/StudentDashboardPage'));
const StudentProfilePage = lazy(() => import('../features/student/StudentProfilePage'));
const StudentSchedulePage = lazy(() => import('../features/student/StudentSchedulePage'));
const StudentClassroomsPage = lazy(() => import('../features/student/StudentClassroomsPage'));
const StudentConnectionRequestsPage = lazy(() => import('../features/student/StudentConnectionRequestsPage'));
const StudentDocumentsPage = lazy(() => import('../features/student/StudentDocumentsPage'));
const StudentPaymentsPage = lazy(() => import('../features/student/StudentPaymentsPage'));
const PaymentDetailPage = lazy(() => import('../features/student/PaymentDetailPage'));
const TutorBookingPage = lazy(() => import('../features/student/TutorBookingPage'));

// Tải trễ trang phòng học trực tuyến Jitsi Meet
const RoomPage = lazy(() => import('../features/classroom/RoomPage'));

// Tải trễ các trang quản trị hệ thống Admin
const AdminDashboardPage = lazy(() => import('../features/admin/AdminDashboardPage'));
const AdminApprovalsPage = lazy(() => import('../features/admin/AdminApprovalsPage'));
const AdminStudentsPage = lazy(() => import('../features/admin/AdminStudentsPage'));
const AdminTutorsPage = lazy(() => import('../features/admin/AdminTutorsPage'));
const AdminConnectionRequestsPage = lazy(() => import('../features/admin/AdminConnectionRequestsPage'));
const AdminClassroomsPage = lazy(() => import('../features/admin/AdminClassroomsPage'));
const AdminCatalogPage = lazy(() => import('../features/admin/AdminCatalogPage'));
const AdminLearningDocumentsPage = lazy(() => import('../features/admin/AdminLearningDocumentsPage'));
const AdminOperationsPage = lazy(() => import('../features/admin/AdminOperationsPage'));
const AdminProfilePage = lazy(() => import('../features/admin/AdminProfilePage'));
const AdminComplaintsPage = lazy(() => import('../features/admin/AdminComplaintsPage'));
const UserComplaintsPage = lazy(() => import('../features/complaints/UserComplaintsPage'));

function RouteFallback() {
  return (
    <div className="d-flex align-items-center justify-content-center py-5 text-secondary" style={{ minHeight: '35vh' }}>
      <span className="spinner-border spinner-border-sm text-danger me-2" role="status" aria-hidden="true" />
      <span>Đang tải trang…</span>
    </div>
  );
}

// Component phụ trợ tự động gán Layout tương ứng theo vai trò cho /chat và /notifications
function AdaptiveChatRoute() {
  const { user, isTutor, isStudent, isAdmin } = useAuth();
  const location = useLocation();
  const searchStr = location.search || '';
  const tutorMode = isTutor || user?.roles?.includes('Tutor') || user?.role === 'Tutor';
  const studentMode = isStudent || user?.roles?.includes('Student_Parent') || user?.roles?.includes('Student') || user?.role === 'Student_Parent' || user?.role === 'Student';
  const adminMode = isAdmin || user?.roles?.includes('Admin') || user?.role === 'Admin';

  if (adminMode) {
    return <Navigate to={`/admin/chat${searchStr}`} replace />;
  }
  if (tutorMode) {
    return <Navigate to={`/tutor/chat${searchStr}`} replace />;
  }
  if (studentMode) {
    return <Navigate to={`/student/chat${searchStr}`} replace />;
  }
  return (
    <MainLayout>
      <ChatPage />
    </MainLayout>
  );
}

function AdaptiveNotificationsRoute() {
  const { user, isTutor, isStudent, isAdmin } = useAuth();
  const tutorMode = isTutor || user?.roles?.includes('Tutor') || user?.role === 'Tutor';
  const studentMode = isStudent || user?.roles?.includes('Student_Parent') || user?.roles?.includes('Student') || user?.role === 'Student_Parent' || user?.role === 'Student';
  const adminMode = isAdmin || user?.roles?.includes('Admin') || user?.role === 'Admin';

  if (adminMode) {
    return <Navigate to="/admin/notifications" replace />;
  }
  if (tutorMode) {
    return <Navigate to="/tutor/notifications" replace />;
  }
  if (studentMode) {
    return <Navigate to="/student/notifications" replace />;
  }
  return (
    <MainLayout>
      <NotificationsPage />
    </MainLayout>
  );
}

function AdaptiveBookingRoute() {
  const { user, isStudent } = useAuth();
  const studentMode = isStudent || user?.roles?.includes('Student_Parent') || user?.roles?.includes('Student') || user?.role === 'Student_Parent' || user?.role === 'Student';

  if (studentMode) {
    return (
      <StudentLayout>
        <TutorBookingPage />
      </StudentLayout>
    );
  }

  return (
    <MainLayout>
      <TutorBookingPage />
    </MainLayout>
  );
}

function AdaptiveRoomRoute() {
  const { user, isTutor } = useAuth();
  const tutorMode = isTutor || user?.roles?.includes('Tutor') || user?.role === 'Tutor';

  if (tutorMode) {
    return (
      <TutorLayout>
        <RoomPage />
      </TutorLayout>
    );
  }

  return (
    <StudentLayout>
      <RoomPage />
    </StudentLayout>
  );
}

export default function AppRoutes() {
  const location = useLocation();
  const lastVisitRef = useRef({ path: '', time: 0 });

  useEffect(() => {
    const path = location.pathname || '/';
    const now = Date.now();
    // Tránh gửi trùng khi React StrictMode mount 2 lần hoặc người dùng bấm lại route trong < 5 giây
    if (lastVisitRef.current.path === path && now - lastVisitRef.current.time < 5000) {
      return;
    }
    lastVisitRef.current = { path, time: now };

    const token =
      localStorage.getItem('token') ||
      sessionStorage.getItem('token') ||
      localStorage.getItem('otsp_token');

    const headers = { 'Content-Type': 'application/json' };
    if (token) headers.Authorization = `Bearer ${token}`;

    fetch(`${API_BASE_URL}/home/visit`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ path }),
      keepalive: true
    }).catch(() => {});
  }, [location.pathname]);

  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
      {/* 1. Public Pages with MainLayout */}
      <Route element={<MainLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/guide" element={<GuidePage />} />
        <Route path="/faq" element={<FaqPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/contact" element={<ContactPage />} />
        <Route path="/about" element={<AboutCenterPage />} />
        <Route path="/access-denied" element={<AccessDeniedPage />} />
        <Route path="/search" element={<SearchPage />} />
        <Route path="/documents" element={<LearningDocumentPage />} />
        <Route path="/news" element={<NewsPage />} />
        <Route path="/news/:articleId" element={<NewsArticlePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ForgotPasswordPage />} />

        {/* Common Authenticated Routes */}
        <Route element={<ProtectedRoute />}>
          <Route path="/account/manage/change-password" element={<ChangePasswordPage />} />
        </Route>
      </Route>

      {/* Adaptive Chat & Notifications Routes (Auto switches to TutorLayout or StudentLayout) */}
      <Route element={<ProtectedRoute />}>
        <Route path="/chat" element={<AdaptiveChatRoute />} />
        <Route path="/notifications" element={<AdaptiveNotificationsRoute />} />
      </Route>

      {/* Admin portal */}
      <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
        <Route path="/admin" element={<AdminLayout />}>
          <Route index element={<AdminDashboardPage />} />
          <Route path="dashboard" element={<AdminDashboardPage />} />
          <Route path="tutor-approvals" element={<AdminApprovalsPage />} />
          <Route path="students" element={<AdminStudentsPage />} />
          <Route path="tutors" element={<AdminTutorsPage />} />
          <Route path="connection-requests" element={<AdminConnectionRequestsPage />} />
          <Route path="classrooms" element={<AdminClassroomsPage />} />
          <Route path="catalog" element={<AdminCatalogPage />} />
          <Route path="learning-documents" element={<AdminLearningDocumentsPage />} />
          <Route path="reviews" element={<AdminOperationsPage />} />
          <Route path="chat" element={<ChatPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="messages" element={<AdminOperationsPage />} />
          <Route path="complaints" element={<AdminComplaintsPage />} />
          <Route path="news" element={<AdminOperationsPage />} />
          <Route path="payments" element={<AdminOperationsPage />} />
          <Route path="reconciliation" element={<AdminOperationsPage />} />
          <Route path="accounts" element={<AdminOperationsPage />} />
          <Route path="audit-logs" element={<AdminOperationsPage />} />
          <Route path="alerts" element={<AdminOperationsPage />} />
          <Route path="ai" element={<AdminOperationsPage />} />
          <Route path="profile" element={<AdminProfilePage />} />
          <Route path="*" element={<Navigate to="/admin/tutor-approvals" replace />} />
        </Route>
      </Route>

      {/* 2. Online Classroom Room (Jitsi Meet with Adaptive Layout) */}
      <Route element={<ProtectedRoute />}>
        <Route path="/room/:sessionId" element={<AdaptiveRoomRoute />} />
        <Route path="/ClassSession/Room" element={<AdaptiveRoomRoute />} />
        <Route path="/student/room/:sessionId" element={<AdaptiveRoomRoute />} />
        <Route path="/tutor/room/:sessionId" element={<AdaptiveRoomRoute />} />
      </Route>

      {/* 3. Tutor Portal (Role: Tutor) */}
      <Route element={<ProtectedRoute allowedRoles={['Tutor']} />}>
        <Route path="/tutor" element={<TutorLayout />}>
          <Route index element={<Navigate to="/tutor/dashboard" replace />} />
          <Route path="dashboard" element={<TutorDashboardPage />} />
          <Route path="profile" element={<TutorProfilePage />} />
          <Route path="income" element={<TutorIncomePage />} />
          <Route path="schedule" element={<TutorSchedulePage />} />
          <Route path="connection-requests" element={<TutorConnectionRequestsPage />} />
          <Route path="classrooms" element={<TutorClassroomsPage />} />
          <Route path="classrooms/create" element={<TutorClassroomFormPage />} />
          <Route path="classrooms/edit/:id" element={<TutorClassroomFormPage />} />
          <Route path="availability" element={<TutorAvailabilityPage />} />
          <Route path="students" element={<TutorStudentsPage />} />
          <Route path="subjects" element={<TutorSubjectsPage />} />
          <Route path="documents" element={<TutorDocumentsPage />} />
          <Route path="reviews" element={<TutorReviewsPage />} />
          <Route path="complaints" element={<UserComplaintsPage />} />
          <Route path="chat" element={<ChatPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
        </Route>

        {/* Legacy ASP.NET Core URL Aliases for Tutor Portal */}
        <Route element={<TutorLayout />}>
          <Route path="/Tutor" element={<Navigate to="/tutor/dashboard" replace />} />
          <Route path="/Tutor/TutorDashboard" element={<TutorDashboardPage />} />
          <Route path="/Tutor/TutorDashboard/Index" element={<TutorDashboardPage />} />
          <Route path="/Tutor/TutorIncome" element={<TutorIncomePage />} />
          <Route path="/Tutor/TutorIncome/Index" element={<TutorIncomePage />} />
          <Route path="/Tutor/TutorProfile" element={<TutorProfilePage />} />
          <Route path="/Tutor/TutorProfile/Index" element={<TutorProfilePage />} />
          <Route path="/Tutor/TutorSchedule" element={<TutorSchedulePage />} />
          <Route path="/Tutor/TutorSchedule/Index" element={<TutorSchedulePage />} />
          <Route path="/Tutor/TutorConnectionRequest" element={<TutorConnectionRequestsPage />} />
          <Route path="/Tutor/TutorConnectionRequest/Index" element={<TutorConnectionRequestsPage />} />
          <Route path="/Tutor/TutorClassroom" element={<TutorClassroomsPage />} />
          <Route path="/Tutor/TutorClassroom/Index" element={<TutorClassroomsPage />} />
          <Route path="/Tutor/TutorClassroom/Create" element={<TutorClassroomFormPage />} />
          <Route path="/Tutor/TutorClassroom/Edit/:id" element={<TutorClassroomFormPage />} />
          <Route path="/Tutor/TutorAvailability" element={<TutorAvailabilityPage />} />
          <Route path="/Tutor/TutorAvailability/Index" element={<TutorAvailabilityPage />} />
          <Route path="/Tutor/TutorStudent" element={<TutorStudentsPage />} />
          <Route path="/Tutor/TutorStudent/Index" element={<TutorStudentsPage />} />
          <Route path="/Tutor/TutorSubject" element={<TutorSubjectsPage />} />
          <Route path="/Tutor/TutorSubject/Index" element={<TutorSubjectsPage />} />
          <Route path="/Tutor/TutorLearningDocument" element={<TutorDocumentsPage />} />
          <Route path="/Tutor/TutorLearningDocument/Index" element={<TutorDocumentsPage />} />
          <Route path="/Tutor/TutorReview" element={<TutorReviewsPage />} />
          <Route path="/Tutor/TutorReview/Index" element={<TutorReviewsPage />} />
          <Route path="/Tutor/TutorComplaint" element={<UserComplaintsPage />} />
        </Route>
      </Route>

      {/* 4. Student & Parent Portal (Role: Student_Parent) */}
      <Route element={<ProtectedRoute allowedRoles={['Student_Parent', 'Student']} />}>
        <Route path="/student" element={<StudentLayout />}>
          <Route index element={<Navigate to="/student/dashboard" replace />} />
          <Route path="dashboard" element={<StudentDashboardPage />} />
          <Route path="profile" element={<StudentProfilePage />} />
          <Route path="schedule" element={<StudentSchedulePage />} />
          <Route path="classrooms" element={<StudentClassroomsPage />} />
          <Route path="connection-requests" element={<StudentConnectionRequestsPage />} />
          <Route path="requests" element={<Navigate to="/student/connection-requests" replace />} />
          <Route path="documents" element={<StudentDocumentsPage />} />
          <Route path="payments" element={<StudentPaymentsPage />} />
          <Route path="payments/:paymentId/checkout" element={<PaymentDetailPage />} />
          <Route path="complaints" element={<UserComplaintsPage />} />
          <Route path="chat" element={<ChatPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
        </Route>

        {/* Legacy ASP.NET URLs used by the Student_Parent portal */}
        <Route element={<StudentLayout />}>
          <Route path="/Student_Parent/StudentDashboard" element={<StudentDashboardPage />} />
          <Route path="/Student_Parent/StudentSchedule" element={<StudentSchedulePage />} />
          <Route path="/Student_Parent/StudentClassroom" element={<StudentClassroomsPage />} />
          <Route path="/Student_Parent/Send_Details_Tutor/MyRequests" element={<StudentConnectionRequestsPage />} />
          <Route path="/Student_Parent/StudentLearningDocument" element={<StudentDocumentsPage />} />
          <Route path="/Student_Parent/StudentParent_Payment" element={<StudentPaymentsPage />} />
          <Route path="/Student_Parent/StudentParent_Payment/Checkout/:paymentId" element={<PaymentDetailPage />} />
          <Route path="/Student_Parent/StudentParent_Payment/Details_Payment/:paymentId" element={<PaymentDetailPage />} />
          <Route path="/Student_Parent/Student_Parent_Profile" element={<StudentProfilePage />} />
          <Route path="/Student_Parent/StudentComplaint" element={<UserComplaintsPage />} />
        </Route>
      </Route>

      {/* 5. Tutor Profile & Booking (Adaptive Layout by Role, Open to Guests, Students, and Tutors) */}
      <Route path="/tutors/:tutorId" element={<AdaptiveBookingRoute />} />
      <Route path="/tutor-detail/:tutorId" element={<AdaptiveBookingRoute />} />
      <Route path="/student/booking/:tutorId" element={<AdaptiveBookingRoute />} />
      <Route path="/student/send-details-tutor/:tutorId" element={<AdaptiveBookingRoute />} />
      <Route path="/Student_Parent/Send_Details_Tutor/Index/:tutorId" element={<AdaptiveBookingRoute />} />
      <Route path="/Student_Parent/Send_Details_Tutor/:tutorId" element={<AdaptiveBookingRoute />} />

        {/* Điều hướng dự phòng khi không tìm thấy trang */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  );
}
