import { useEffect, useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import adminApi from '../../api/adminApi';
import adminOperationsApi from '../../api/adminOperationsApi';
import '../../styles/admin-pages.css';

export default function AdminDashboardPage() {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [lastUpdated, setLastUpdated] = useState(new Date());

  // Backend state
  const [tutorApprovalData, setTutorApprovalData] = useState(null);
  const [reconciliationData, setReconciliationData] = useState(null);
  const [aiUsageData, setAiUsageData] = useState(null);
  const [paymentsData, setPaymentsData] = useState([]);
  const [alertsData, setAlertsData] = useState([]);
  const [complaintsData, setComplaintsData] = useState([]);
  const [studentsCount, setStudentsCount] = useState(0);
  const [classroomsCount, setClassroomsCount] = useState(0);
  const [tutorsCount, setTutorsCount] = useState(0);

  // Dynamic lists from backend
  const [dailyMetrics, setDailyMetrics] = useState([]);
  const [topTutors, setTopTutors] = useState([]);
  const [topStudents, setTopStudents] = useState([]);
  const [recentTutorNotifications, setRecentTutorNotifications] = useState([]);

  // Tab điều khiển sơ đồ tăng trưởng & con trỏ rê chuột
  const [chartActiveSeries, setChartActiveSeries] = useState('all'); // 'all' | 'revenue' | 'ai' | 'students'
  const [hoveredIdx, setHoveredIdx] = useState(null);

  // Load all data from real backend APIs
  const loadAllDashboardData = async (isManualRefresh = false) => {
    if (isManualRefresh) setRefreshing(true);
    else setLoading(true);

    try {
      // 1. Thử gọi API chuyên biệt Admin Dashboard từ Backend .NET
      try {
        const directStatsRes = await adminApi.getDashboardStats();
        if (directStatsRes?.data?.overview) {
          const d = directStatsRes.data;
          setTutorApprovalData({
            stats: {
              pending: d.overview.pendingTutors ?? 0,
              pendingQualifications: d.overview.pendingQualifications ?? 0
            },
            totalApprovedTutors: d.overview.approvedTutors ?? d.overview.totalTutors ?? 0
          });
          setReconciliationData({
            totals: {
              totalRevenue: d.overview.totalRevenue ?? 0,
              platformFee: d.overview.platformFee ?? 0,
              tutorNet: d.overview.tutorPayout ?? 0
            }
          });
          setAiUsageData(d.aiUsage ?? null);
          if (Array.isArray(d.recentPayments)) {
            setPaymentsData(d.recentPayments);
          }
          if (Array.isArray(d.dailyMetrics)) {
            setDailyMetrics(d.dailyMetrics);
          }
          if (Array.isArray(d.topTutors)) {
            setTopTutors(d.topTutors);
          }
          if (Array.isArray(d.topStudents)) {
            setTopStudents(d.topStudents);
          }
          if (Array.isArray(d.recentTutorNotifications)) {
            setRecentTutorNotifications(d.recentTutorNotifications);
          }
          setStudentsCount(d.overview.totalStudents ?? 0);
          setClassroomsCount(d.overview.activeClassrooms ?? d.overview.totalClassrooms ?? 0);
          setTutorsCount(d.overview.totalTutors ?? 0);
          setAlertsData(new Array(d.overview.pendingAlerts || 0).fill({}));
          setComplaintsData(new Array(d.overview.pendingComplaints || 0).fill({}));
          setLastUpdated(new Date());
          return;
        }
      } catch (directErr) {
        console.warn('API /admin/dashboard/stats fallbacking:', directErr);
      }

      // 2. Fallback đồng thời qua các API Backend lẻ nếu cần
      const [
        approvalsRes,
        reconRes,
        aiRes,
        paymentsRes,
        alertsRes,
        complaintsRes,
        studentsRes,
        classroomsRes
      ] = await Promise.allSettled([
        adminApi.getTutorApprovalDashboard({ page: 1, pageSize: 5 }),
        adminOperationsApi.getReconciliation(),
        adminOperationsApi.getAiUsage(),
        adminOperationsApi.getPayments({ page: 1, pageSize: 6 }),
        adminOperationsApi.getAlerts({ status: 'New', pageSize: 5 }),
        adminOperationsApi.getComplaints({ status: 'Pending', pageSize: 5 }),
        adminApi.getStudents({ page: 1, pageSize: 1 }),
        adminApi.getClassrooms({ page: 1, pageSize: 1 })
      ]);

      if (approvalsRes.status === 'fulfilled') setTutorApprovalData(approvalsRes.value?.data);
      if (reconRes.status === 'fulfilled') setReconciliationData(reconRes.value?.data);
      if (aiRes.status === 'fulfilled') setAiUsageData(aiRes.value?.data);
      if (paymentsRes.status === 'fulfilled') {
        const rawPayments = paymentsRes.value?.data?.payments?.items || paymentsRes.value?.data?.items || [];
        setPaymentsData(Array.isArray(rawPayments) ? rawPayments : []);
      }
      if (alertsRes.status === 'fulfilled') {
        const rawAlerts = alertsRes.value?.data?.alerts?.items || alertsRes.value?.data?.alerts || [];
        setAlertsData(Array.isArray(rawAlerts) ? rawAlerts : []);
      }
      if (complaintsRes.status === 'fulfilled') {
        const rawComplaints = complaintsRes.value?.data?.complaints?.items || complaintsRes.value?.data?.complaints || [];
        setComplaintsData(Array.isArray(rawComplaints) ? rawComplaints : []);
      }
      if (studentsRes.status === 'fulfilled') {
        setStudentsCount(studentsRes.value?.data?.total || studentsRes.value?.data?.students?.total || 0);
      }
      if (classroomsRes.status === 'fulfilled') {
        setClassroomsCount(classroomsRes.value?.data?.total || classroomsRes.value?.data?.classrooms?.total || 0);
      }

      setLastUpdated(new Date());
    } catch (err) {
      console.error('Lỗi khi tải dữ liệu Admin Dashboard:', err);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadAllDashboardData();
  }, []);

  // Format VND chuẩn dấu chấm
  const formatVnd = (val) => {
    if (!val || isNaN(val)) return '0 đ';
    return `${Number(val).toLocaleString('vi-VN')} đ`;
  };

  // Tính toán KPI thực tế khớp 100% Backend
  const stats = useMemo(() => {
    const totalRev = Number(reconciliationData?.totals?.totalRevenue ?? 0);
    const platformFee = Number(reconciliationData?.totals?.platformFee ?? Math.round(totalRev * 0.2));
    const tutorPayout = Number(reconciliationData?.totals?.tutorNet ?? (totalRev - platformFee));

    const pendingTutors = Number(tutorApprovalData?.stats?.pending ?? 0);
    const pendingQuals = Number(tutorApprovalData?.stats?.pendingQualifications ?? 0);
    const totalApprovedTutors = Number(tutorApprovalData?.totalApprovedTutors ?? tutorsCount ?? 0);

    const aiSessions = Number(aiUsageData?.totalSessions ?? 0);
    const aiToday = Number(aiUsageData?.sessionsToday ?? 0);
    const todayMetric = dailyMetrics?.[dailyMetrics.length - 1];
    const aiQuestionsToday = todayMetric ? Number(todayMetric.aiQuestions ?? 0) : aiToday;
    const tokensToday = todayMetric ? Number(todayMetric.tokens ?? 0) : (aiQuestionsToday * 1420);
    const visitsToday = todayMetric ? Number(todayMetric.visits ?? 0) : 0;

    const pendingIssues = pendingQuals + pendingTutors + alertsData.length + complaintsData.length;

    return {
      revenue: totalRev,
      platformFee,
      tutorPayout,
      pendingTutors,
      pendingQuals,
      totalTutors: totalApprovedTutors,
      approvedTutors: totalApprovedTutors,
      aiSessions,
      aiToday,
      aiQuestionsToday,
      tokensToday,
      visitsToday,
      pendingIssues,
    };
  }, [tutorApprovalData, reconciliationData, aiUsageData, alertsData, complaintsData, dailyMetrics, tutorsCount]);

  // Dữ liệu sơ đồ tăng trưởng 7 ngày - NẠP TRỰC TIẾP 100% TỪ BACKEND API .NET
  const chartData = useMemo(() => {
    if (!Array.isArray(dailyMetrics) || dailyMetrics.length === 0) {
      return {
        items: [],
        labels: [],
        revenuePoints: [],
        aiPoints: [],
        studentPoints: [],
        classPoints: [],
        maxCumRev: 1,
        maxDayRev: 1,
        maxAi: 1,
        maxStud: 1,
        maxVisits: 1,
        totalWeekRevenue: 0,
        totalWeekAi: 0,
        totalWeekVisits: 0,
        peakDay: null
      };
    }

    const items = dailyMetrics.map((dm, idx) => {
      const dayName = dm?.dayName || `T.${idx + 1}`;
      const date = dm?.date || '';
      const fullDate = dm?.fullDate || `${dayName}, ${date}`;

      const rev = Number(dm?.revenue) || 0;
      const cumRev = Number(dm?.cumulativeRevenue) || (Number(dm?.revenueMillions) ? Number(dm.revenueMillions) * 1_000_000 : rev);
      const aiQ = Number(dm?.aiQuestions) || 0;
      const tok = Number(dm?.tokens) || (aiQ * 1420);
      const vis = Number(dm?.visits) || 0;
      const stud = Number(dm?.students) || 0;

      return {
        idx,
        dayName,
        date,
        fullDate,
        revenue: rev,
        cumulativeRevenue: cumRev,
        revenueMillions: Math.round((cumRev / 1_000_000) * 100) / 100,
        aiQuestions: aiQ,
        tokens: tok,
        visits: vis,
        students: stud
      };
    });

    const maxCumRev = Math.max(...items.map(i => i.cumulativeRevenue), 1);
    const maxDayRev = Math.max(...items.map(i => i.revenue), 1);
    const maxAi = Math.max(...items.map(i => i.aiQuestions), 1);
    const maxStud = Math.max(...items.map(i => i.students), 1);
    const maxVisits = Math.max(...items.map(i => i.visits), 1);

    const totalWeekRevenue = items.reduce((sum, i) => sum + i.revenue, 0);
    const totalWeekAi = items.reduce((sum, i) => sum + i.aiQuestions, 0);
    const totalWeekVisits = items.reduce((sum, i) => sum + i.visits, 0);

    // Xác định ngày cao điểm
    let peakIdx = 0;
    let maxCompositeScore = -1;
    items.forEach((item, i) => {
      const score = (item.revenue / maxDayRev) + (item.aiQuestions / maxAi) + (item.visits / maxVisits);
      if (score > maxCompositeScore) {
        maxCompositeScore = score;
        peakIdx = i;
      }
    });

    return {
      items,
      labels: items.map(i => i.date),
      revenuePoints: items.map(i => i.revenueMillions),
      aiPoints: items.map(i => i.aiQuestions),
      studentPoints: items.map(i => i.students),
      classPoints: items.map(i => i.students),
      maxCumRev,
      maxDayRev,
      maxAi,
      maxStud,
      maxVisits,
      totalWeekRevenue,
      totalWeekAi,
      totalWeekVisits,
      peakDay: items[peakIdx]
    };
  }, [dailyMetrics]);

  const chartSeries = chartData;

  // Hằng số tọa độ SVG chuẩn hóa
  const CHART_LEFT = 52;
  const CHART_RIGHT = 624;
  const CHART_TOP = 22;
  const CHART_BOTTOM = 170;
  const CHART_W = CHART_RIGHT - CHART_LEFT; // 572
  const CHART_H = CHART_BOTTOM - CHART_TOP; // 148

  // Tọa độ X cho điểm idx
  const getChartX = (idx, total = 7) => {
    return CHART_LEFT + idx * (CHART_W / (total - 1 || 1));
  };

  // Tọa độ Y theo giá trị thực tế
  const getChartY = (val, maxVal) => {
    if (!maxVal || maxVal <= 0) return CHART_BOTTOM;
    const ratio = Math.max(0, Math.min(1, Number(val) / Number(maxVal)));
    return CHART_BOTTOM - ratio * CHART_H;
  };

  // Hàm tạo đường cong Bézier mượt mà chuẩn SVG (Smooth Curved Path)
  const buildSmoothCurve = (points, width = 640, height = 200, maxVal = 100) => {
    if (!points || points.length === 0) return '';
    const pts = points.map((val, idx) => ({
      x: getChartX(idx, points.length),
      y: getChartY(val, maxVal)
    }));

    let d = `M ${pts[0].x.toFixed(1)},${pts[0].y.toFixed(1)}`;
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[i];
      const p1 = pts[i + 1];
      const cx = (p0.x + p1.x) / 2;
      d += ` C ${cx.toFixed(1)},${p0.y.toFixed(1)} ${cx.toFixed(1)},${p1.y.toFixed(1)} ${p1.x.toFixed(1)},${p1.y.toFixed(1)}`;
    }
    return d;
  };

  // Hàm tạo vùng tô bóng mờ dưới đường cong (Area Path)
  const buildSmoothArea = (points, width = 640, height = 200, maxVal = 100) => {
    const curve = buildSmoothCurve(points, width, height, maxVal);
    if (!curve || points.length === 0) return '';
    const lastX = getChartX(points.length - 1, points.length).toFixed(1);
    const firstX = getChartX(0, points.length).toFixed(1);
    return `${curve} L ${lastX},${CHART_BOTTOM} L ${firstX},${CHART_BOTTOM} Z`;
  };

  // Tính giá trị cực đại cho biểu đồ cột
  const maxDailyMetric = useMemo(() => {
    if (!dailyMetrics || dailyMetrics.length === 0) return { visits: 50, ai: 30, tokens: 10000 };
    return {
      visits: Math.max(...dailyMetrics.map(d => Number(d.visits) || 0), 20),
      ai: Math.max(...dailyMetrics.map(d => Number(d.aiQuestions) || 0), 10),
      tokens: Math.max(...dailyMetrics.map(d => Number(d.tokens) || 0), 5000)
    };
  }, [dailyMetrics]);

  const feePercent = stats.revenue > 0 ? Math.round((stats.platformFee / stats.revenue) * 100) : 20;
  const payoutPercent = 100 - feePercent;

  return (
    <div className="admin-dashboard-container py-3 px-2 px-md-4">
      <style>{`
        /* ============================================================
           PROFESSIONAL EXECUTIVE ADMIN DASHBOARD SYSTEM
           Phong cách Sơ đồ dữ liệu kiến trúc phẳng & Tinh gọn OTSP
           ============================================================ */
        :root {
          --dash-surface: #ffffff;
          --dash-subtle: #f8fafc;
          --dash-border: #e2e8f0;
          --dash-border-subtle: #f1f5f9;
          --dash-text: #0f172a;
          --dash-text-muted: #64748b;
          --dash-primary: #DB303A;
          --dash-primary-soft: rgba(219, 48, 58, 0.08);
          --dash-green: #15803d;
          --dash-green-soft: rgba(21, 128, 61, 0.08);
          --dash-amber: #b45309;
          --dash-amber-soft: rgba(180, 83, 9, 0.08);
          --dash-blue: #0284c7;
          --dash-blue-soft: rgba(2, 132, 199, 0.08);
          --dash-card-shadow: 0 1px 3px rgba(0, 0, 0, 0.04), 0 6px 16px -4px rgba(15, 23, 42, 0.04);
        }

        [data-bs-theme="dark"] {
          --dash-surface: #14152d;
          --dash-subtle: #1c1d3b;
          --dash-border: #282a52;
          --dash-border-subtle: #1f2042;
          --dash-text: #f8fafc;
          --dash-text-muted: #94a3b8;
          --dash-primary: #ff4d5a;
          --dash-primary-soft: rgba(255, 77, 90, 0.12);
          --dash-card-shadow: 0 4px 20px rgba(0, 0, 0, 0.25);
        }

        /* 1. Header điều hành tinh gọn */
        .admin-exec-header {
          background: var(--dash-surface);
          border: 1px solid var(--dash-border);
          border-radius: 14px;
          padding: 16px 20px;
          margin-bottom: 20px;
          box-shadow: var(--dash-card-shadow);
        }

        /* 2. Bento KPI Cards */
        .dash-kpi-grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
          gap: 16px;
          margin-bottom: 22px;
        }
        .dash-kpi-box {
          background: var(--dash-surface);
          border: 1px solid var(--dash-border);
          border-radius: 14px;
          padding: 18px 20px;
          display: flex;
          flex-direction: column;
          justify-content: space-between;
          box-shadow: var(--dash-card-shadow);
          transition: transform 0.18s ease, border-color 0.18s ease;
          position: relative;
        }
        .dash-kpi-box:hover {
          transform: translateY(-2px);
          border-color: #cbd5e1;
        }
        .dash-kpi-title {
          font-size: 11px;
          font-weight: 700;
          letter-spacing: 0.6px;
          text-transform: uppercase;
          color: var(--dash-text-muted);
          margin-bottom: 6px;
        }
        .dash-kpi-val {
          font-size: 24px;
          font-weight: 800;
          color: var(--dash-text);
          letter-spacing: -0.4px;
          line-height: 1.2;
        }
        .dash-kpi-icon {
          width: 40px;
          height: 40px;
          border-radius: 10px;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 19px;
        }

        /* 3. Card chung cho Sơ đồ */
        .diagram-card {
          background: var(--dash-surface);
          border: 1px solid var(--dash-border);
          border-radius: 16px;
          padding: 20px 22px;
          box-shadow: var(--dash-card-shadow);
          height: 100%;
          display: flex;
          flex-direction: column;
        }
        .diagram-header-badge {
          width: 32px;
          height: 32px;
          border-radius: 8px;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          font-size: 16px;
          margin-right: 10px;
        }

        /* 4. SƠ ĐỒ LUỒNG DÒNG TIỀN (Sankey / Financial Flow Architecture) */
        .flow-architecture-stage {
          background: var(--dash-subtle);
          border: 1px solid var(--dash-border);
          border-radius: 14px;
          padding: 20px 16px;
          position: relative;
        }
        .flow-node-box {
          background: var(--dash-surface);
          border: 1px solid var(--dash-border);
          border-radius: 12px;
          padding: 14px 16px;
          box-shadow: 0 2px 6px rgba(0, 0, 0, 0.03);
          position: relative;
          transition: transform 0.18s ease;
        }
        .flow-node-box:hover {
          transform: translateY(-2px);
          border-color: #cbd5e1;
        }
        .flow-connector-line {
          display: flex;
          align-items: center;
          justify-content: center;
          color: var(--dash-text-muted);
          font-size: 18px;
        }

        /* 5. SƠ ĐỒ QUY TRÌNH KIỂM DUYỆT (Quality Pipeline Steps) */
        .pipeline-track {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
          gap: 12px;
          margin-top: 10px;
        }
        .pipeline-step {
          background: var(--dash-subtle);
          border: 1px solid var(--dash-border);
          border-radius: 12px;
          padding: 14px;
          position: relative;
        }
        .pipeline-step-badge {
          width: 24px;
          height: 24px;
          border-radius: 50%;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          font-size: 11px;
          font-weight: 700;
          margin-bottom: 8px;
        }

        /* 6. SƠ ĐỒ BIỂU ĐỒ SÓNG MƯỢT (SVG Smooth Area Chart) */
        .chart-svg-wrap {
          background: var(--dash-subtle);
          border: 1px solid var(--dash-border);
          border-radius: 14px;
          padding: 16px 14px 10px;
          position: relative;
        }
        .chart-filter-pill {
          font-size: 11.5px;
          font-weight: 600;
          padding: 4px 12px;
          border-radius: 20px;
          cursor: pointer;
          border: 1px solid var(--dash-border);
          background: var(--dash-surface);
          color: var(--dash-text-muted);
          transition: all 0.15s ease;
        }
        .chart-filter-pill.active {
          background: #DB303A;
          color: #ffffff;
          border-color: #DB303A;
        }

        .chart-summary-chip {
          background: var(--dash-surface);
          border: 1px solid var(--dash-border);
          border-radius: 8px;
          padding: 4px 10px;
          display: flex;
          align-items: center;
          gap: 6px;
          font-size: 11.5px;
          box-shadow: 0 1px 2px rgba(0, 0, 0, 0.03);
        }

        .chart-y-axis-text {
          font-size: 9.5px;
          fill: var(--dash-text-muted);
          font-weight: 500;
          font-family: inherit;
        }

        .chart-inspector-panel {
          animation: chartFadeIn 0.12s ease-out;
        }
        @keyframes chartFadeIn {
          from { opacity: 0; }
          to { opacity: 1; }
        }

        .pulse-ring {
          animation: chartPulse 1.8s infinite;
        }
        @keyframes chartPulse {
          0% { r: 5px; opacity: 0.6; }
          50% { r: 9px; opacity: 0.15; }
          100% { r: 5px; opacity: 0.6; }
        }

        /* 7. SƠ ĐỒ CỘT 7 NGÀY (Daily Bars Diagram) */
        .daily-bars-container {
          display: grid;
          grid-template-columns: repeat(7, 1fr);
          gap: 8px;
          align-items: flex-end;
          height: 145px;
          padding: 12px 6px 4px;
          border-bottom: 1px dashed var(--dash-border);
        }
        .daily-bar-column {
          display: flex;
          flex-direction: column;
          align-items: center;
          height: 100%;
          justify-content: flex-end;
        }
        .daily-bar-fill {
          width: 14px;
          border-radius: 4px 4px 1px 1px;
          transition: height 0.4s ease;
        }

        .table-responsive {
          overflow-x: auto;
          -webkit-overflow-scrolling: touch;
        }

        /* 8. Modern Table */
        .modern-diagram-table {
          width: 100%;
          border-collapse: separate;
          border-spacing: 0 6px;
        }
        .modern-diagram-table th {
          font-size: 11px;
          text-transform: uppercase;
          letter-spacing: 0.5px;
          color: var(--dash-text-muted);
          font-weight: 700;
          padding: 6px 12px;
          border: none;
        }
        .modern-diagram-table td {
          background: var(--dash-subtle);
          padding: 10px 12px;
          font-size: 12.5px;
          vertical-align: middle;
          border-top: 1px solid var(--dash-border);
          border-bottom: 1px solid var(--dash-border);
        }
        .modern-diagram-table tr td:first-child {
          border-left: 1px solid var(--dash-border);
          border-top-left-radius: 10px;
          border-bottom-left-radius: 10px;
        }
        .modern-diagram-table tr td:last-child {
          border-right: 1px solid var(--dash-border);
          border-top-right-radius: 10px;
          border-bottom-right-radius: 10px;
        }

        .spin-icon {
          animation: spin 0.8s linear infinite;
        }
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }

        /* ============================================================
           MOBILE RESPONSIVE & ADAPTIVE STYLING (IMPECCABLE CRAFT)
           ============================================================ */
        @media (max-width: 991.98px) {
          .dash-kpi-grid {
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 12px;
          }
          .admin-exec-header {
            padding: 14px 16px;
          }
          .chart-svg-wrap {
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
          }
        }

        @media (max-width: 767.98px) {
          .dash-kpi-grid {
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 10px;
          }
          .dash-kpi-box {
            padding: 12px 14px;
            border-radius: 12px;
          }
          .dash-kpi-val {
            font-size: 19px;
          }
          .dash-kpi-icon {
            width: 34px;
            height: 34px;
            font-size: 16px;
          }
          .diagram-card {
            padding: 14px 16px;
            border-radius: 14px;
          }
          .flow-architecture-stage {
            padding: 14px 12px;
          }
          .flow-connector-line {
            transform: rotate(90deg);
            margin: 6px 0;
          }
          .pipeline-track {
            grid-template-columns: 1fr;
            gap: 10px;
          }
          .chart-filter-pill {
            font-size: 10.5px;
            padding: 3px 8px;
          }
          .daily-bars-container {
            height: 120px;
            gap: 4px;
          }
          .daily-bar-fill {
            width: 10px;
          }
          .modern-diagram-table-wrap {
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
            margin: 0 -8px;
            padding: 0 8px;
          }
          .modern-diagram-table th,
          .modern-diagram-table td {
            padding: 8px 10px;
            font-size: 11.5px;
            white-space: nowrap;
          }
        }

        @media (max-width: 479.98px) {
          .dash-kpi-grid {
            grid-template-columns: 1fr;
            gap: 8px;
          }
          .dash-kpi-box {
            padding: 12px;
          }
          .admin-exec-header h5 {
            font-size: 17px;
          }
          .admin-exec-header small {
            font-size: 11.5px;
          }
          .diagram-header-badge {
            width: 28px;
            height: 28px;
            font-size: 14px;
          }
          .chart-summary-chip {
            font-size: 10.5px;
            padding: 2px 6px;
          }
        }
      `}</style>

      {/* 1. THANH TIÊU ĐỀ ĐIỀU HÀNH CHUYÊN NGHIỆP */}
      <div className="admin-exec-header d-flex flex-column flex-md-row align-items-start align-items-md-center justify-content-between gap-3">
        <div>
          <div className="d-flex align-items-center gap-2 mb-1">
            <span className="badge rounded-pill" style={{ background: '#DB303A', color: '#fff', fontSize: 10.5 }}>
              OTSP Core Admin
            </span>
            <span className="text-secondary small">/ Tổng quan &amp; Sơ đồ vận hành</span>
          </div>
          <h5 className="fw-bold mb-0 text-body">
            Sơ Đồ Điều Hành &amp; Thống Kê Hệ Thống OTSP
          </h5>
          <small className="text-secondary">
            Giám sát thời gian thực luồng đối soát doanh thu, kết nối gia sư và tài nguyên tác nhân AI Wein
          </small>
        </div>

        <div className="d-flex align-items-center gap-2 flex-wrap">
          <div className="d-flex align-items-center gap-2 px-3 py-1.5 rounded-pill border bg-light-subtle" style={{ fontSize: 12 }}>
            <span className="badge rounded-circle p-1 bg-success d-inline-block"></span>
            <span className="text-secondary">Đồng bộ:</span>
            <strong className="text-body">
              {lastUpdated.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
            </strong>
          </div>

          <button
            type="button"
            onClick={() => loadAllDashboardData(true)}
            disabled={refreshing || loading}
            className="btn btn-sm btn-outline-danger rounded-pill px-3 py-1.5 fw-semibold d-flex align-items-center gap-1.5"
            style={{ fontSize: 12 }}
          >
            <i className={`bi bi-arrow-clockwise ${refreshing || loading ? 'spin-icon' : ''}`}></i>
            <span>{refreshing ? 'Đang cập nhật...' : 'Làm mới'}</span>
          </button>
        </div>
      </div>

      {/* 2. 5 THẺ BENTO KPI TỔNG QUAN */}
      <div className="dash-kpi-grid">
        {/* KPI 1: TỔNG DOANH THU */}
        <div className="dash-kpi-box" style={{ borderTop: '2px solid #DB303A' }}>
          <div className="d-flex align-items-start justify-content-between">
            <div>
              <div className="dash-kpi-title">TỔNG DOANH THU</div>
              <div className="dash-kpi-val text-danger">{formatVnd(stats.revenue)}</div>
            </div>
            <div className="dash-kpi-icon" style={{ background: 'var(--dash-primary-soft)', color: '#DB303A' }}>
              <i className="bi bi-wallet2"></i>
            </div>
          </div>
          <div className="mt-3 pt-2 border-top d-flex justify-content-between small text-secondary" style={{ fontSize: 11.5 }}>
            <span>Hoa hồng 20%: <strong className="text-body">{formatVnd(stats.platformFee)}</strong></span>
            <span className="text-success">Gia sư 80%: <strong>{formatVnd(stats.tutorPayout)}</strong></span>
          </div>
        </div>

        {/* KPI 2: GIA SƯ NỀN TẢNG */}
        <div className="dash-kpi-box" style={{ borderTop: '2px solid #0284c7' }}>
          <div className="d-flex align-items-start justify-content-between">
            <div>
              <div className="dash-kpi-title">GIA SƯ NỀN TẢNG</div>
              <div className="dash-kpi-val">{stats.totalTutors} <small className="fs-6 fw-normal text-secondary">gia sư</small></div>
            </div>
            <div className="dash-kpi-icon" style={{ background: 'var(--dash-blue-soft)', color: '#0284c7' }}>
              <i className="bi bi-mortarboard"></i>
            </div>
          </div>
          <div className="mt-3 pt-2 border-top d-flex align-items-center justify-content-between small" style={{ fontSize: 11.5 }}>
            <span className="text-secondary">{stats.pendingTutors > 0 ? `${stats.pendingTutors} hồ sơ chờ xác minh` : `${stats.approvedTutors} gia sư đã duyệt`}</span>
            <Link to="/admin/tutors" className="text-decoration-none fw-semibold text-primary">Chi tiết ›</Link>
          </div>
        </div>

        {/* KPI 3: HỌC VIÊN & LỚP HỌC */}
        <div className="dash-kpi-box" style={{ borderTop: '2px solid #15803d' }}>
          <div className="d-flex align-items-start justify-content-between">
            <div>
              <div className="dash-kpi-title">HỌC VIÊN &amp; LỚP HỌC</div>
              <div className="dash-kpi-val">{studentsCount} <small className="fs-6 fw-normal text-secondary">học viên</small></div>
            </div>
            <div className="dash-kpi-icon" style={{ background: 'var(--dash-green-soft)', color: '#15803d' }}>
              <i className="bi bi-people"></i>
            </div>
          </div>
          <div className="mt-3 pt-2 border-top d-flex align-items-center justify-content-between small text-success fw-semibold" style={{ fontSize: 11.5 }}>
            <span><i className="bi bi-broadcast me-1"></i>{classroomsCount} lớp đang mở</span>
            <Link to="/admin/classrooms" className="text-success text-decoration-none">Xem lớp ›</Link>
          </div>
        </div>

        {/* KPI 4: TÁC TỬ AI WEIN */}
        <div className="dash-kpi-box" style={{ borderTop: '2px solid #06b6d4' }}>
          <div className="d-flex align-items-start justify-content-between">
            <div>
              <div className="dash-kpi-title">TÁC TỬ AI WEIN</div>
              <div className="dash-kpi-val" style={{ color: '#0891b2' }}>{stats.aiQuestionsToday} <small className="fs-6 fw-normal text-secondary">hỏi hôm nay</small></div>
            </div>
            <div className="dash-kpi-icon" style={{ background: 'rgba(6, 182, 212, 0.1)', color: '#0891b2' }}>
              <i className="bi bi-cpu"></i>
            </div>
          </div>
          <div className="mt-3 pt-2 border-top d-flex justify-content-between small text-secondary" style={{ fontSize: 11.5 }}>
            <span>~{Number(stats.tokensToday || 0).toLocaleString('vi-VN')} tokens</span>
            <span>{stats.visitsToday} lượt truy cập</span>
          </div>
        </div>

        {/* KPI 5: VIỆC CẦN GIẢI QUYẾT */}
        <div className="dash-kpi-box" style={{ borderTop: '2px solid #b45309' }}>
          <div className="d-flex align-items-start justify-content-between">
            <div>
              <div className="dash-kpi-title">VIỆC CẦN GIẢI QUYẾT</div>
              <div className="dash-kpi-val text-warning">{stats.pendingIssues}</div>
            </div>
            <div className="dash-kpi-icon" style={{ background: 'var(--dash-amber-soft)', color: '#b45309' }}>
              <i className="bi bi-exclamation-triangle"></i>
            </div>
          </div>
          <div className="mt-3 pt-2 border-top d-flex align-items-center justify-content-between small" style={{ fontSize: 11.5 }}>
            <span className="text-secondary">{stats.pendingQuals > 0 ? `${stats.pendingQuals} chứng chỉ chờ AI OCR` : 'Hệ thống an toàn'}</span>
            <Link to="/admin/tutor-approvals" className="text-warning-emphasis fw-semibold text-decoration-none">Xử lý ›</Link>
          </div>
        </div>
      </div>

      {/* 3. SƠ ĐỒ 1: SƠ ĐỒ LUỒNG DÒNG TIỀN & ĐỐI SOÁT OTSP (FLOW ARCHITECTURE) */}
      <div className="row g-4 mb-4">
        <div className="col-12">
          <div className="diagram-card">
            <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
              <div className="d-flex align-items-center">
                <span className="diagram-header-badge" style={{ background: 'var(--dash-primary-soft)', color: '#DB303A' }}>
                  <i className="bi bi-diagram-3-fill"></i>
                </span>
                <div>
                  <h6 className="fw-bold mb-0">Sơ Đồ Phân Phối Dòng Tiền &amp; Đối Soát Tự Động OTSP</h6>
                  <small className="text-secondary">Mô hình phân tách quỹ học phí, bảo chứng thanh toán và hoa hồng hệ thống</small>
                </div>
              </div>
              <Link to="/admin/reconciliation" className="btn btn-sm btn-outline-danger rounded-pill px-3 py-1 fw-semibold" style={{ fontSize: 11.5 }}>
                Báo cáo tài chính chi tiết ›
              </Link>
            </div>

            {/* SƠ ĐỒ DẠNG KHỐI KẾT NỐI (ARCHITECTURAL FLOW DIAGRAM) */}
            <div className="flow-architecture-stage">
              <div className="row g-3 align-items-center">
                {/* Khối 1: Đầu vào Học viên thanh toán */}
                <div className="col-12 col-md-3">
                  <div className="flow-node-box border-start border-4 border-danger">
                    <div className="d-flex align-items-center justify-content-between mb-2">
                      <span className="badge rounded-pill bg-danger-subtle text-danger px-2.5 py-0.5" style={{ fontSize: 10.5 }}>
                        1. Dòng tiền đầu vào
                      </span>
                      <i className="bi bi-arrow-down-left-square text-danger"></i>
                    </div>
                    <div className="fw-bold text-body" style={{ fontSize: 14 }}>Học Viên Thanh Toán</div>
                    <div className="h5 fw-bold text-danger my-1">{formatVnd(stats.revenue)}</div>
                    <small className="text-secondary d-block" style={{ fontSize: 11 }}>
                      100% học phí đăng ký khóa học qua chuyển khoản VietQR
                    </small>
                  </div>
                </div>

                {/* Đường liên kết mũi tên */}
                <div className="col-12 col-md-1 text-center d-none d-md-block">
                  <div className="flow-connector-line">
                    <i className="bi bi-arrow-right-circle-fill text-danger fs-4"></i>
                  </div>
                </div>

                {/* Khối 2: Trung tâm Ký Quỹ & Bảo Chứng OTSP Escrow */}
                <div className="col-12 col-md-4">
                  <div className="flow-node-box border-start border-4 border-primary" style={{ background: 'var(--dash-surface)' }}>
                    <div className="d-flex align-items-center justify-content-between mb-2">
                      <span className="badge rounded-pill bg-primary-subtle text-primary px-2.5 py-0.5" style={{ fontSize: 10.5 }}>
                        2. Bảo chứng ký quỹ OTSP
                      </span>
                      <i className="bi bi-shield-check text-primary"></i>
                    </div>
                    <div className="fw-bold text-body" style={{ fontSize: 14 }}>OTSP Smart Escrow Center</div>
                    <small className="text-secondary d-block my-1" style={{ fontSize: 11.5 }}>
                      Giữ tạm thời &amp; bảo hiểm học tập 100%. Tự động bóc tách doanh thu khi kết thúc buổi dạy.
                    </small>
                    <div className="d-flex gap-2 mt-2 pt-2 border-top" style={{ fontSize: 11 }}>
                      <span className="badge bg-light text-dark border">Đối soát: Hoàn tất</span>
                      <span className="badge bg-light text-dark border">Bảo hiểm: Đảm bảo</span>
                    </div>
                  </div>
                </div>

                {/* Đường liên kết mũi tên */}
                <div className="col-12 col-md-1 text-center d-none d-md-block">
                  <div className="flow-connector-line">
                    <i className="bi bi-arrow-right-circle-fill text-success fs-4"></i>
                  </div>
                </div>

                {/* Khối 3: Đầu ra phân nhánh (20% Sàn & 80% Gia sư) */}
                <div className="col-12 col-md-3">
                  <div className="d-flex flex-column gap-2">
                    {/* Nhánh A: Hoa hồng OTSP 20% */}
                    <div className="flow-node-box border-start border-3 border-warning p-2.5">
                      <div className="d-flex align-items-center justify-content-between">
                        <span className="fw-bold text-warning-emphasis" style={{ fontSize: 12 }}>
                          Hoa hồng OTSP ({feePercent}%)
                        </span>
                        <strong className="text-warning-emphasis" style={{ fontSize: 13 }}>
                          {formatVnd(stats.platformFee)}
                        </strong>
                      </div>
                      <small className="text-secondary d-block" style={{ fontSize: 10.5 }}>
                        Duy trì máy chủ AI Wein, hạ tầng &amp; bảo hiểm
                      </small>
                    </div>

                    {/* Nhánh B: Thù lao Gia sư 80% */}
                    <div className="flow-node-box border-start border-3 border-success p-2.5">
                      <div className="d-flex align-items-center justify-content-between">
                        <span className="fw-bold text-success" style={{ fontSize: 12 }}>
                          Lương trả Gia sư ({payoutPercent}%)
                        </span>
                        <strong className="text-success" style={{ fontSize: 13 }}>
                          {formatVnd(stats.tutorPayout)}
                        </strong>
                      </div>
                      <small className="text-secondary d-block" style={{ fontSize: 10.5 }}>
                        Quyết toán tự động qua mã VietQR cuối tháng
                      </small>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* 4. SƠ ĐỒ 2 & 3: BIỂU ĐỒ SÓNG MƯỢT TĂNG TRƯỞNG & SƠ ĐỒ CỘT 7 NGÀY */}
      <div className="row g-4 mb-4">
        {/* SƠ ĐỒ TĂNG TRƯỞNG TỔNG QUÁT TƯƠNG TÁC RÊ CHUỘT (INTERACTIVE SVG EXECUTIVE CHART) */}
        <div className="col-12 col-xl-7">
          <div className="diagram-card d-flex flex-column justify-content-between">
            <div>
              <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-2">
                <div className="d-flex align-items-center">
                  <span className="diagram-header-badge" style={{ background: 'var(--dash-primary-soft)', color: '#DB303A' }}>
                    <i className="bi bi-activity"></i>
                  </span>
                  <div>
                    <h6 className="fw-bold mb-0">Sơ Đồ Nhịp Độ Vận Hành &amp; Tăng Trưởng Hệ Thống</h6>
                    <small className="text-secondary">Dữ liệu thực tế 7 ngày từ Database &amp; API Backend .NET</small>
                  </div>
                </div>

                {/* Bộ lọc chuyển đổi góc nhìn phân tích */}
                <div className="d-flex align-items-center gap-1.5 flex-wrap">
                  <button
                    type="button"
                    className={`chart-filter-pill ${chartActiveSeries === 'all' ? 'active' : ''}`}
                    onClick={() => setChartActiveSeries('all')}
                  >
                    Tổng quan 3D
                  </button>
                  <button
                    type="button"
                    className={`chart-filter-pill ${chartActiveSeries === 'revenue' ? 'active' : ''}`}
                    onClick={() => setChartActiveSeries('revenue')}
                  >
                    Doanh thu (VNĐ)
                  </button>
                  <button
                    type="button"
                    className={`chart-filter-pill ${chartActiveSeries === 'ai' ? 'active' : ''}`}
                    onClick={() => setChartActiveSeries('ai')}
                  >
                    AI Wein
                  </button>
                  <button
                    type="button"
                    className={`chart-filter-pill ${chartActiveSeries === 'students' ? 'active' : ''}`}
                    onClick={() => setChartActiveSeries('students')}
                  >
                    Học viên &amp; Lớp
                  </button>
                </div>
              </div>

              {/* Dải chỉ số tóm tắt nhanh tuần (Executive Summary Strip) */}
              <div className="d-flex flex-wrap align-items-center gap-2 mb-3 pt-2 border-top">
                <div className="chart-summary-chip">
                  <span className="text-secondary" style={{ fontSize: 11 }}>Doanh thu tuần:</span>
                  <strong className="text-danger fw-bold">{formatVnd(chartData.totalWeekRevenue)}</strong>
                </div>
                <div className="chart-summary-chip">
                  <span className="text-secondary" style={{ fontSize: 11 }}>Hỏi AI Wein:</span>
                  <strong className="text-info-emphasis fw-bold">{chartData.totalWeekAi} câu</strong>
                </div>
                <div className="chart-summary-chip">
                  <span className="text-secondary" style={{ fontSize: 11 }}>Truy cập web:</span>
                  <strong className="text-body fw-bold">{chartData.totalWeekVisits.toLocaleString('vi-VN')} lượt</strong>
                </div>
                <div className="chart-summary-chip ms-auto">
                  <span className="badge bg-success-subtle text-success border border-success-subtle d-flex align-items-center gap-1" style={{ fontSize: 10.5 }}>
                    <i className="bi bi-lightning-charge-fill"></i>
                    <span>Cao điểm: {chartData.peakDay?.dayName} ({chartData.peakDay?.date})</span>
                  </span>
                </div>
              </div>

              {/* KHUNG BIỂU ĐỒ SVG TƯƠNG TÁC RÊ CHUỘT (INSPECTOR READY) */}
              <div className="chart-svg-wrap position-relative flex-grow-1 d-flex flex-column justify-content-between">
                {/* Chú giải góc trên biểu đồ */}
                <div className="d-flex justify-content-between align-items-center mb-2 px-1 text-secondary" style={{ fontSize: 11 }}>
                  <span>
                    <i className="bi bi-cursor-fill me-1 text-danger"></i>
                    {hoveredIdx !== null ? (
                      <span className="text-danger fw-semibold">
                        Đang xem: {chartData.items[hoveredIdx]?.fullDate}
                      </span>
                    ) : (
                      <span>Rê chuột trên đồ thị để xem chi tiết từng ngày</span>
                    )}
                  </span>
                  <span className="d-flex gap-3">
                    {(chartActiveSeries === 'all' || chartActiveSeries === 'revenue') && (
                      <span className="d-flex align-items-center gap-1">
                        <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#DB303A' }}></span>
                        <span>Doanh thu {chartActiveSeries === 'revenue' ? '(VNĐ & Lũy kế)' : '(Tr.đ)'}</span>
                      </span>
                    )}
                    {(chartActiveSeries === 'all' || chartActiveSeries === 'ai') && (
                      <span className="d-flex align-items-center gap-1">
                        <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#0284c7' }}></span>
                        <span>AI Wein (Câu hỏi)</span>
                      </span>
                    )}
                    {(chartActiveSeries === 'all' || chartActiveSeries === 'students') && (
                      <span className="d-flex align-items-center gap-1">
                        <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#15803d' }}></span>
                        <span>Học viên kết nối</span>
                      </span>
                    )}
                  </span>
                </div>

                {/* SVG CANVAS */}
                {dailyMetrics.length === 0 ? (
                  <div className="text-center py-5 text-secondary">
                    <div className="spinner-border spinner-border-sm text-danger me-2" role="status"></div>
                    <small>Đang nạp dữ liệu thống kê từ Backend .NET...</small>
                  </div>
                ) : (
                  <>
                    <div className="position-relative">
                      <svg
                        viewBox="0 0 640 200"
                        className="w-100"
                        style={{ height: 200, overflow: 'visible', cursor: 'crosshair', userSelect: 'none' }}
                        onMouseMove={(e) => {
                          const rect = e.currentTarget.getBoundingClientRect();
                          const clientX = e.clientX - rect.left;
                          const svgX = (clientX / rect.width) * 640;
                          const step = CHART_W / (chartData.items.length - 1 || 1);
                          const rawIdx = Math.round((svgX - CHART_LEFT) / step);
                          const clampedIdx = Math.max(0, Math.min(chartData.items.length - 1, rawIdx));
                          setHoveredIdx(clampedIdx);
                        }}
                        onMouseLeave={() => setHoveredIdx(null)}
                      >
                        <defs>
                          <linearGradient id="revenueGrad" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#DB303A" stopOpacity="0.32" />
                            <stop offset="100%" stopColor="#DB303A" stopOpacity="0.02" />
                          </linearGradient>
                          <linearGradient id="aiGrad" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#0284c7" stopOpacity="0.25" />
                            <stop offset="100%" stopColor="#0284c7" stopOpacity="0.01" />
                          </linearGradient>
                          <linearGradient id="studentGrad" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#15803d" stopOpacity="0.22" />
                            <stop offset="100%" stopColor="#15803d" stopOpacity="0.01" />
                          </linearGradient>
                          <linearGradient id="dailyBarGrad" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#DB303A" stopOpacity="0.75" />
                            <stop offset="100%" stopColor="#DB303A" stopOpacity="0.2" />
                          </linearGradient>
                        </defs>

                        {/* Đường lưới tọa độ Y và nhãn số liệu trục tung */}
                        {[0, 0.25, 0.5, 0.75, 1].map((ratio, i) => {
                          const yPos = CHART_BOTTOM - ratio * CHART_H;
                          let yLabel = '';
                          if (chartActiveSeries === 'all') {
                            yLabel = `${Math.round(ratio * 100)}%`;
                          } else if (chartActiveSeries === 'revenue') {
                            const mVal = (chartData.maxCumRev / 1000000) * ratio;
                            yLabel = ratio === 0 ? '0 đ' : `${mVal >= 1 ? mVal.toFixed(1) : mVal.toFixed(2)} Tr`;
                          } else if (chartActiveSeries === 'ai') {
                            yLabel = `${Math.round(chartData.maxAi * ratio)} câu`;
                          } else {
                            yLabel = `${Math.round(chartData.maxStud * ratio)} HV`;
                          }

                          return (
                            <g key={i}>
                              <line
                                x1={CHART_LEFT}
                                y1={yPos}
                                x2={CHART_RIGHT}
                                y2={yPos}
                                stroke="rgba(148, 163, 184, 0.22)"
                                strokeDasharray={ratio === 0 ? '0' : '3 3'}
                              />
                              <text
                                x={CHART_LEFT - 8}
                                y={yPos + 3.5}
                                textAnchor="end"
                                className="chart-y-axis-text"
                              >
                                {yLabel}
                              </text>
                            </g>
                          );
                        })}

                        {/* Khi ở chế độ Doanh thu: Vẽ thêm các cột doanh thu ngày (Daily Intake Bars) */}
                        {chartActiveSeries === 'revenue' &&
                          chartData.items.map((it, i) => {
                            const x = getChartX(i);
                            const barW = 22;
                            const barH = Math.max(6, (it.revenue / chartData.maxDayRev) * (CHART_H * 0.42));
                            const isHovered = hoveredIdx === i;
                            return (
                              <rect
                                key={i}
                                x={x - barW / 2}
                                y={CHART_BOTTOM - barH}
                                width={barW}
                                height={barH}
                                rx="4"
                                fill="url(#dailyBarGrad)"
                                opacity={isHovered ? 1 : 0.65}
                                stroke={isHovered ? '#DB303A' : 'none'}
                                strokeWidth="1.5"
                              />
                            );
                          })}

                        {/* Vùng diện tích (Area Fills) */}
                        {(chartActiveSeries === 'all' || chartActiveSeries === 'revenue') && (
                          <path
                            d={buildSmoothArea(
                              chartActiveSeries === 'all'
                                ? chartData.items.map(it => (it.cumulativeRevenue / chartData.maxCumRev) * 100)
                                : chartData.items.map(it => it.cumulativeRevenue),
                              640,
                              200,
                              chartActiveSeries === 'all' ? 100 : chartData.maxCumRev
                            )}
                            fill="url(#revenueGrad)"
                          />
                        )}
                        {(chartActiveSeries === 'all' || chartActiveSeries === 'ai') && (
                          <path
                            d={buildSmoothArea(
                              chartActiveSeries === 'all'
                                ? chartData.items.map(it => (it.aiQuestions / chartData.maxAi) * 100)
                                : chartData.items.map(it => it.aiQuestions),
                              640,
                              200,
                              chartActiveSeries === 'all' ? 100 : chartData.maxAi
                            )}
                            fill="url(#aiGrad)"
                          />
                        )}
                        {(chartActiveSeries === 'all' || chartActiveSeries === 'students') && (
                          <path
                            d={buildSmoothArea(
                              chartActiveSeries === 'all'
                                ? chartData.items.map(it => (it.students / chartData.maxStud) * 100)
                                : chartData.items.map(it => it.students),
                              640,
                              200,
                              chartActiveSeries === 'all' ? 100 : chartData.maxStud
                            )}
                            fill="url(#studentGrad)"
                          />
                        )}

                        {/* Đường cong Bézier sắc nét (Smooth Line Curves) */}
                        {(chartActiveSeries === 'all' || chartActiveSeries === 'revenue') && (
                          <path
                            d={buildSmoothCurve(
                              chartActiveSeries === 'all'
                                ? chartData.items.map(it => (it.cumulativeRevenue / chartData.maxCumRev) * 100)
                                : chartData.items.map(it => it.cumulativeRevenue),
                              640,
                              200,
                              chartActiveSeries === 'all' ? 100 : chartData.maxCumRev
                            )}
                            fill="none"
                            stroke="#DB303A"
                            strokeWidth="3"
                            strokeLinecap="round"
                          />
                        )}
                        {(chartActiveSeries === 'all' || chartActiveSeries === 'ai') && (
                          <path
                            d={buildSmoothCurve(
                              chartActiveSeries === 'all'
                                ? chartData.items.map(it => (it.aiQuestions / chartData.maxAi) * 100)
                                : chartData.items.map(it => it.aiQuestions),
                              640,
                              200,
                              chartActiveSeries === 'all' ? 100 : chartData.maxAi
                            )}
                            fill="none"
                            stroke="#0284c7"
                            strokeWidth="2.5"
                            strokeLinecap="round"
                          />
                        )}
                        {(chartActiveSeries === 'all' || chartActiveSeries === 'students') && (
                          <path
                            d={buildSmoothCurve(
                              chartActiveSeries === 'all'
                                ? chartData.items.map(it => (it.students / chartData.maxStud) * 100)
                                : chartData.items.map(it => it.students),
                              640,
                              200,
                              chartActiveSeries === 'all' ? 100 : chartData.maxStud
                            )}
                            fill="none"
                            stroke="#15803d"
                            strokeWidth="2.5"
                            strokeDasharray={chartActiveSeries === 'all' ? '4 3' : 'none'}
                            strokeLinecap="round"
                          />
                        )}

                        {/* HIỆU ỨNG RÊ CHUỘT: ĐƯỜNG DÓNG VÀ ĐIỂM CHẠM XUNG NHỊP */}
                        {hoveredIdx !== null && (
                          <g>
                            {/* Đường dóng dọc (Vertical Hairline Guide) */}
                            <line
                              x1={getChartX(hoveredIdx)}
                              y1={CHART_TOP - 6}
                              x2={getChartX(hoveredIdx)}
                              y2={CHART_BOTTOM}
                              stroke="#DB303A"
                              strokeWidth="1.5"
                              strokeDasharray="3 3"
                              opacity="0.75"
                            />

                            {/* Điểm neo Doanh thu */}
                            {(chartActiveSeries === 'all' || chartActiveSeries === 'revenue') && (
                              <g>
                                <circle
                                  cx={getChartX(hoveredIdx)}
                                  cy={getChartY(
                                    chartActiveSeries === 'all'
                                      ? (chartData.items[hoveredIdx].cumulativeRevenue / chartData.maxCumRev) * 100
                                      : chartData.items[hoveredIdx].cumulativeRevenue,
                                    chartActiveSeries === 'all' ? 100 : chartData.maxCumRev
                                  )}
                                  r="7"
                                  fill="#DB303A"
                                  opacity="0.3"
                                  className="pulse-ring"
                                />
                                <circle
                                  cx={getChartX(hoveredIdx)}
                                  cy={getChartY(
                                    chartActiveSeries === 'all'
                                      ? (chartData.items[hoveredIdx].cumulativeRevenue / chartData.maxCumRev) * 100
                                      : chartData.items[hoveredIdx].cumulativeRevenue,
                                    chartActiveSeries === 'all' ? 100 : chartData.maxCumRev
                                  )}
                                  r="4.5"
                                  fill="#ffffff"
                                  stroke="#DB303A"
                                  strokeWidth="2.5"
                                />
                              </g>
                            )}

                            {/* Điểm neo AI */}
                            {(chartActiveSeries === 'all' || chartActiveSeries === 'ai') && (
                              <g>
                                <circle
                                  cx={getChartX(hoveredIdx)}
                                  cy={getChartY(
                                    chartActiveSeries === 'all'
                                      ? (chartData.items[hoveredIdx].aiQuestions / chartData.maxAi) * 100
                                      : chartData.items[hoveredIdx].aiQuestions,
                                    chartActiveSeries === 'all' ? 100 : chartData.maxAi
                                  )}
                                  r="7"
                                  fill="#0284c7"
                                  opacity="0.3"
                                  className="pulse-ring"
                                />
                                <circle
                                  cx={getChartX(hoveredIdx)}
                                  cy={getChartY(
                                    chartActiveSeries === 'all'
                                      ? (chartData.items[hoveredIdx].aiQuestions / chartData.maxAi) * 100
                                      : chartData.items[hoveredIdx].aiQuestions,
                                    chartActiveSeries === 'all' ? 100 : chartData.maxAi
                                  )}
                                  r="4.5"
                                  fill="#ffffff"
                                  stroke="#0284c7"
                                  strokeWidth="2.5"
                                />
                              </g>
                            )}

                            {/* Điểm neo Học viên */}
                            {(chartActiveSeries === 'all' || chartActiveSeries === 'students') && (
                              <g>
                                <circle
                                  cx={getChartX(hoveredIdx)}
                                  cy={getChartY(
                                    chartActiveSeries === 'all'
                                      ? (chartData.items[hoveredIdx].students / chartData.maxStud) * 100
                                      : chartData.items[hoveredIdx].students,
                                    chartActiveSeries === 'all' ? 100 : chartData.maxStud
                                  )}
                                  r="7"
                                  fill="#15803d"
                                  opacity="0.3"
                                  className="pulse-ring"
                                />
                                <circle
                                  cx={getChartX(hoveredIdx)}
                                  cy={getChartY(
                                    chartActiveSeries === 'all'
                                      ? (chartData.items[hoveredIdx].students / chartData.maxStud) * 100
                                      : chartData.items[hoveredIdx].students,
                                    chartActiveSeries === 'all' ? 100 : chartData.maxStud
                                  )}
                                  r="4.5"
                                  fill="#ffffff"
                                  stroke="#15803d"
                                  strokeWidth="2.5"
                                />
                              </g>
                            )}
                          </g>
                        )}
                      </svg>

                      {/* BẢNG TOOLTIP NỔI THÔNG MINH KHI RÊ CHUỘT (FLOATING INSPECTOR CARD) */}
                      {hoveredIdx !== null && chartData.items[hoveredIdx] && (
                        <div
                          className="chart-inspector-panel position-absolute"
                          style={{
                            top: '12px',
                            left: `${(getChartX(hoveredIdx) / 640) * 100}%`,
                            transform: hoveredIdx >= 4 ? 'translate(-104%, 0)' : 'translate(12px, 0)',
                            pointerEvents: 'none',
                            zIndex: 25,
                            minWidth: '230px',
                            background: 'var(--dash-surface)',
                            border: '1px solid var(--dash-border)',
                            borderRadius: '12px',
                            boxShadow: '0 12px 28px -4px rgba(15, 23, 42, 0.2), 0 4px 8px -2px rgba(0, 0, 0, 0.08)',
                            padding: '10px 14px',
                            transition: 'left 0.08s ease-out, transform 0.08s ease-out'
                          }}
                        >
                          <div className="d-flex align-items-center justify-content-between pb-1.5 mb-1.5 border-bottom">
                            <div className="d-flex align-items-center gap-1.5">
                              <i className="bi bi-calendar-check text-danger" style={{ fontSize: 12 }}></i>
                              <strong className="text-body" style={{ fontSize: 12 }}>
                                {chartData.items[hoveredIdx].fullDate}
                              </strong>
                            </div>
                            <span className="badge bg-danger-subtle text-danger border border-danger-subtle" style={{ fontSize: 9.5 }}>
                              {hoveredIdx === chartData.items.length - 1 ? 'Hôm nay' : `Ngày ${hoveredIdx + 1}/7`}
                            </span>
                          </div>

                          <div className="d-flex flex-column gap-1.5" style={{ fontSize: 11.5 }}>
                            {/* Doanh thu */}
                            <div className="d-flex align-items-center justify-content-between">
                              <span className="d-flex align-items-center gap-1.5 text-secondary">
                                <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#DB303A' }}></span>
                                Doanh thu ngày:
                              </span>
                              <strong className="text-danger">+{formatVnd(chartData.items[hoveredIdx].revenue)}</strong>
                            </div>
                            <div className="d-flex justify-content-between text-secondary ps-3" style={{ fontSize: 10.5 }}>
                              <span>Lũy kế tuần:</span>
                              <span className="fw-semibold text-body">{formatVnd(chartData.items[hoveredIdx].cumulativeRevenue)}</span>
                            </div>

                            {/* Tác tử AI */}
                            <div className="d-flex align-items-center justify-content-between pt-1 border-top">
                              <span className="d-flex align-items-center gap-1.5 text-secondary">
                                <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#0284c7' }}></span>
                                Hỏi AI Wein:
                              </span>
                              <strong className="text-primary">{chartData.items[hoveredIdx].aiQuestions} câu hỏi</strong>
                            </div>
                            <div className="d-flex justify-content-between text-secondary ps-3" style={{ fontSize: 10.5 }}>
                              <span>Tiêu thụ token:</span>
                              <span className="fw-semibold text-body">~{chartData.items[hoveredIdx].tokens.toLocaleString('vi-VN')} tok</span>
                            </div>

                            {/* Học viên & Lớp học */}
                            <div className="d-flex align-items-center justify-content-between pt-1 border-top">
                              <span className="d-flex align-items-center gap-1.5 text-secondary">
                                <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#15803d' }}></span>
                                Học viên kết nối:
                              </span>
                              <strong className="text-success">{chartData.items[hoveredIdx].students} HV</strong>
                            </div>

                            {/* Lượt truy cập */}
                            <div className="d-flex align-items-center justify-content-between">
                              <span className="d-flex align-items-center gap-1.5 text-secondary">
                                <i className="bi bi-activity text-secondary" style={{ fontSize: 11 }}></i>
                                Lượt truy cập:
                              </span>
                              <strong className="text-body">{chartData.items[hoveredIdx].visits} lượt</strong>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>

                    {/* TRỤC HOÀNH X: DANH SÁCH 7 NGÀY TƯƠNG TÁC RÊ CHUỘT */}
                    <div
                      className="d-flex justify-content-between px-1 mt-2 pt-1 border-top text-secondary fw-semibold"
                      style={{ fontSize: 11, marginLeft: 44, marginRight: 8 }}
                    >
                      {chartData.items.map((it, i) => {
                        const isHovered = hoveredIdx === i;
                        return (
                          <div
                            key={i}
                            className={`d-flex flex-column align-items-center px-2 py-0.5 rounded cursor-pointer ${
                              isHovered ? 'bg-danger text-white shadow-sm' : ''
                            }`}
                            style={{ cursor: 'pointer', transition: 'all 0.15s ease' }}
                            onMouseEnter={() => setHoveredIdx(i)}
                          >
                            <span style={{ fontSize: 9.5, opacity: isHovered ? 0.95 : 0.75 }}>{it.dayName}</span>
                            <strong style={{ fontSize: 11 }}>{it.date}</strong>
                          </div>
                        );
                      })}
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* SƠ ĐỒ CỘT 7 NGÀY: TRUY CẬP & TOKEN AI (ĐỒNG BỘ RÊ CHUỘT) */}
        <div className="col-12 col-xl-5">
          <div className="diagram-card justify-content-between">
            <div>
              <div className="d-flex align-items-center justify-content-between mb-3">
                <div className="d-flex align-items-center">
                  <span className="diagram-header-badge" style={{ background: 'var(--dash-blue-soft)', color: '#0284c7' }}>
                    <i className="bi bi-bar-chart-steps"></i>
                  </span>
                  <div>
                    <h6 className="fw-bold mb-0">Hoạt Động Truy Cập &amp; Tiêu Thụ Token</h6>
                    <small className="text-secondary">Tỷ trọng tương tác người dùng và tác nhân AI</small>
                  </div>
                </div>
                <span className="badge rounded-pill" style={{ background: '#15803d', color: '#fff', fontSize: 11 }}>
                  Database
                </span>
              </div>

              {dailyMetrics.length === 0 ? (
                <div className="text-center py-5 text-secondary">
                  <div className="spinner-border spinner-border-sm text-danger me-2" role="status"></div>
                  <small>Đang nạp dữ liệu thống kê...</small>
                </div>
              ) : (
                <>
                  <div className="chart-svg-wrap mb-3">
                    <div className="daily-bars-container">
                      {dailyMetrics.map((dm, idx) => {
                        const vH = Math.max(14, Math.round(((Number(dm.visits) || 0) / maxDailyMetric.visits) * 110));
                        const aH = Math.max(10, Math.round(((Number(dm.aiQuestions) || 0) / maxDailyMetric.ai) * 110));
                        const isHovered = hoveredIdx === idx;
                        return (
                          <div
                            key={idx}
                            className="daily-bar-column"
                            style={{
                              cursor: 'pointer',
                              transform: isHovered ? 'scale(1.05)' : 'none',
                              transition: 'transform 0.15s ease'
                            }}
                            onMouseEnter={() => setHoveredIdx(idx)}
                            onMouseLeave={() => setHoveredIdx(null)}
                          >
                            <div className="d-flex gap-1 align-items-end h-100">
                              <div
                                className="daily-bar-fill"
                                style={{
                                  height: `${vH}px`,
                                  background: isHovered ? '#0ea5e9' : '#0284c7',
                                  boxShadow: isHovered ? '0 0 8px rgba(14, 165, 233, 0.5)' : 'none'
                                }}
                                title={`${dm.date}: ${dm.visits} truy cập`}
                              ></div>
                              <div
                                className="daily-bar-fill"
                                style={{
                                  height: `${aH}px`,
                                  background: isHovered ? '#22d3ee' : '#06b6d4',
                                  boxShadow: isHovered ? '0 0 8px rgba(34, 211, 238, 0.5)' : 'none'
                                }}
                                title={`${dm.date}: ${dm.aiQuestions} hỏi AI`}
                              ></div>
                            </div>
                            <small
                              className={`mt-1 fw-semibold ${isHovered ? 'text-danger fw-bold' : 'text-secondary'}`}
                              style={{ fontSize: 10 }}
                            >
                              {dm.dayName || 'T'}
                            </small>
                          </div>
                        );
                      })}
                    </div>
                    <div className="d-flex justify-content-center gap-3 mt-2 text-secondary" style={{ fontSize: 11 }}>
                      <span className="d-flex align-items-center gap-1"><span style={{ width: 8, height: 8, background: '#0284c7', borderRadius: 2 }}></span> Truy cập web</span>
                      <span className="d-flex align-items-center gap-1"><span style={{ width: 8, height: 8, background: '#06b6d4', borderRadius: 2 }}></span> Hỏi AI Wein</span>
                    </div>
                  </div>

                  <div className="d-flex flex-column gap-1.5" style={{ maxHeight: 155, overflowY: 'auto' }}>
                    {dailyMetrics.slice(-3).map((dm, idx) => (
                      <div key={idx} className="p-2 rounded-3 border d-flex align-items-center justify-content-between" style={{ background: 'var(--dash-subtle)', fontSize: 12 }}>
                        <div>
                          <strong>{dm.date}</strong> <span className="text-secondary">({dm.dayName})</span>
                        </div>
                        <div className="d-flex gap-2">
                          <span className="badge bg-primary-subtle text-primary border">{dm.visits} lượt</span>
                          <span className="badge bg-info-subtle text-info border">~{Number(dm.tokens || 0).toLocaleString('vi-VN')} token</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* 5. SƠ ĐỒ 4: SƠ ĐỒ QUY TRÌNH KIỂM DUYỆT GIA SƯ (QUALITY ASSURANCE PIPELINE) */}
      <div className="row g-4 mb-4">
        <div className="col-12">
          <div className="diagram-card">
            <div className="d-flex align-items-center justify-content-between mb-2">
              <div className="d-flex align-items-center">
                <span className="diagram-header-badge" style={{ background: 'var(--dash-green-soft)', color: '#15803d' }}>
                  <i className="bi bi-shield-lock-fill"></i>
                </span>
                <div>
                  <h6 className="fw-bold mb-0">Sơ Đồ Phễu Kiểm Định &amp; Đảm Bảo Chất Lượng Gia Sư</h6>
                  <small className="text-secondary">Chuỗi quy trình 4 bước thẩm định hồ sơ, bằng cấp OCR và kích hoạt giảng dạy</small>
                </div>
              </div>
              <Link to="/admin/tutor-approvals" className="btn btn-sm btn-link text-danger p-0 text-decoration-none fw-semibold" style={{ fontSize: 12 }}>
                Vào duyệt hồ sơ ›
              </Link>
            </div>

            <div className="pipeline-track">
              {/* Bước 1 */}
              <div className="pipeline-step">
                <div className="pipeline-step-badge bg-primary text-white">1</div>
                <div className="fw-bold text-body" style={{ fontSize: 13 }}>Đăng Ký Tài Khoản</div>
                <small className="text-secondary d-block my-1" style={{ fontSize: 11 }}>
                  Tiếp nhận hồ sơ cơ bản, số điện thoại &amp; CCCD
                </small>
                <span className="badge rounded-pill bg-primary-subtle text-primary mt-1" style={{ fontSize: 11 }}>
                  {stats.totalTutors} gia sư trong hệ thống
                </span>
              </div>

              {/* Bước 2 */}
              <div className="pipeline-step border-warning">
                <div className="pipeline-step-badge bg-warning text-dark">2</div>
                <div className="fw-bold text-body" style={{ fontSize: 13 }}>Gemini OCR Bằng Cấp</div>
                <small className="text-secondary d-block my-1" style={{ fontSize: 11 }}>
                  Trí tuệ nhân tạo quét nhận diện chứng chỉ &amp; bằng đại học
                </small>
                <span className="badge rounded-pill bg-warning-subtle text-warning-emphasis mt-1" style={{ fontSize: 11 }}>
                  {stats.pendingQuals > 0 ? `${stats.pendingQuals} chứng chỉ chờ soát` : 'Đã quét toàn bộ'}
                </span>
              </div>

              {/* Bước 3 */}
              <div className="pipeline-step">
                <div className="pipeline-step-badge bg-info text-white">3</div>
                <div className="fw-bold text-body" style={{ fontSize: 13 }}>Admin Thẩm Định</div>
                <small className="text-secondary d-block my-1" style={{ fontSize: 11 }}>
                  Quản trị viên đối chiếu thông tin ngân hàng &amp; QR nhận tiền
                </small>
                <span className="badge rounded-pill bg-info-subtle text-info mt-1" style={{ fontSize: 11 }}>
                  Đã đồng bộ VietQR
                </span>
              </div>

              {/* Bước 4 */}
              <div className="pipeline-step border-success">
                <div className="pipeline-step-badge bg-success text-white">4</div>
                <div className="fw-bold text-body" style={{ fontSize: 13 }}>Cấp Huy Hiệu Xác Minh</div>
                <small className="text-secondary d-block my-1" style={{ fontSize: 11 }}>
                  Mở tính năng tạo lớp 1-1, lớp nhóm và nhận học viên
                </small>
                <span className="badge rounded-pill bg-success-subtle text-success mt-1" style={{ fontSize: 11 }}>
                  ✓ Đạt chuẩn 100%
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* 6. BẢNG XẾP HẠNG: TOP GIA SƯ & TOP HỌC VIÊN CHĂM HỌC */}
      <div className="row g-4 mb-4">
        {/* TOP GIA SƯ NỔI BẬT */}
        <div className="col-12 col-lg-6">
          <div className="diagram-card">
            <div className="d-flex align-items-center justify-content-between mb-3">
              <div className="d-flex align-items-center">
                <span className="diagram-header-badge" style={{ background: 'var(--dash-amber-soft)', color: '#b45309' }}>
                  <i className="bi bi-trophy-fill"></i>
                </span>
                <div>
                  <h6 className="fw-bold mb-0">Top Gia Sư Nổi Bật Nhất</h6>
                  <small className="text-secondary">Đánh giá cao từ học viên và quy mô lớp học</small>
                </div>
              </div>
              <Link to="/admin/tutors" className="btn btn-sm btn-link text-danger p-0 text-decoration-none fw-semibold" style={{ fontSize: 12 }}>
                Quản lý gia sư ›
              </Link>
            </div>

            {topTutors.length === 0 ? (
              <div className="text-center py-4 text-secondary">
                <i className="bi bi-mortarboard fs-2 mb-2 d-block opacity-50"></i>
                <small>Đang nạp danh sách gia sư tiêu biểu...</small>
              </div>
            ) : (
              <div className="d-flex flex-column gap-2">
                {topTutors.map((t, idx) => (
                  <div
                    key={idx}
                    className="p-2.5 rounded-3 border d-flex align-items-center justify-content-between gap-2"
                    style={{ background: 'var(--dash-subtle)' }}
                  >
                    <div className="d-flex align-items-center gap-2.5 overflow-hidden">
                      <div
                        className="rounded-circle d-flex align-items-center justify-content-center text-white fw-bold flex-shrink-0"
                        style={{
                          width: 32,
                          height: 32,
                          fontSize: 12,
                          background: idx === 0 ? '#f59e0b' : idx === 1 ? '#64748b' : idx === 2 ? '#b45309' : '#0d6efd'
                        }}
                      >
                        #{idx + 1}
                      </div>
                      <div className="overflow-hidden">
                        <strong className="d-block text-truncate" style={{ fontSize: 13 }}>
                          {t.fullName}
                        </strong>
                        <small className="text-secondary" style={{ fontSize: 11.5 }}>
                          {t.subjects || 'Đa môn'} · <span className="text-danger fw-semibold">{formatVnd(t.hourlyFee)}/h</span>
                        </small>
                      </div>
                    </div>

                    <div className="text-end flex-shrink-0">
                      <span className="badge rounded-pill" style={{ background: '#15803d', color: '#fff', fontSize: 11.5 }}>
                        ★ {Number(t.rating || 0).toFixed(1)}/5
                      </span>
                      <small className="d-block text-secondary mt-0.5" style={{ fontSize: 11 }}>
                        {t.totalClasses} lớp · {t.totalStudents} học viên
                      </small>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* TOP SINH VIÊN HỌC NHIỀU NHẤT */}
        <div className="col-12 col-lg-6">
          <div className="diagram-card">
            <div className="d-flex align-items-center justify-content-between mb-3">
              <div className="d-flex align-items-center">
                <span className="diagram-header-badge" style={{ background: 'var(--dash-primary-soft)', color: '#DB303A' }}>
                  <i className="bi bi-award-fill"></i>
                </span>
                <div>
                  <h6 className="fw-bold mb-0">Top Học Viên Chăm Học Nhất</h6>
                  <small className="text-secondary">Đo lường bằng số buổi học và lớp tham gia tích cực</small>
                </div>
              </div>
              <Link to="/admin/students" className="btn btn-sm btn-link text-danger p-0 text-decoration-none fw-semibold" style={{ fontSize: 12 }}>
                Quản lý học viên ›
              </Link>
            </div>

            {topStudents.length === 0 ? (
              <div className="text-center py-4 text-secondary">
                <i className="bi bi-people fs-2 mb-2 d-block opacity-50"></i>
                <small>Đang nạp danh sách học viên tích cực...</small>
              </div>
            ) : (
              <div className="d-flex flex-column gap-2">
                {topStudents.map((s, idx) => (
                  <div
                    key={idx}
                    className="p-2.5 rounded-3 border d-flex align-items-center justify-content-between gap-2"
                    style={{ background: 'var(--dash-subtle)' }}
                  >
                    <div className="d-flex align-items-center gap-2.5 overflow-hidden">
                      <div
                        className="rounded-circle d-flex align-items-center justify-content-center text-white fw-bold flex-shrink-0"
                        style={{
                          width: 32,
                          height: 32,
                          fontSize: 12,
                          background: idx === 0 ? '#DB303A' : idx === 1 ? '#0284c7' : '#15803d'
                        }}
                      >
                        #{idx + 1}
                      </div>
                      <div className="overflow-hidden">
                        <strong className="d-block text-truncate" style={{ fontSize: 13 }}>
                          {s.fullName}
                        </strong>
                        <small className="text-secondary" style={{ fontSize: 11.5 }}>
                          Đang học {s.enrolledClasses} lớp kèm
                        </small>
                      </div>
                    </div>

                    <div className="text-end flex-shrink-0">
                      <span className="badge rounded-pill" style={{ background: '#15803d', color: '#fff', fontSize: 11.5 }}>
                        <i className="bi bi-check2-circle me-1"></i>
                        {s.completedSessions || 1} buổi hoàn thành
                      </span>
                      <small className="d-block text-secondary mt-0.5" style={{ fontSize: 11 }}>
                        Chuyên cần tích cực
                      </small>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* 7. BẢNG GIAO DỊCH HỌC PHÍ MỚI & ĐỐI SOÁT */}
      <div className="row g-4 mb-4">
        <div className="col-12">
          <div className="diagram-card">
            <div className="d-flex align-items-center justify-content-between mb-3">
              <div className="d-flex align-items-center">
                <span className="diagram-header-badge" style={{ background: 'var(--dash-primary-soft)', color: '#DB303A' }}>
                  <i className="bi bi-receipt"></i>
                </span>
                <div>
                  <h6 className="fw-bold mb-0">Hoạt Động Giao Dịch Học Phí Mới Nhất</h6>
                  <small className="text-secondary">Bóc tách tự động 20% hoa hồng OTSP và 80% thù lao gia sư</small>
                </div>
              </div>
              <Link to="/admin/reconciliation" className="btn btn-sm btn-outline-danger rounded-pill px-3" style={{ fontSize: 12 }}>
                Đối soát toàn sàn ›
              </Link>
            </div>

            {paymentsData.length === 0 ? (
              <div className="text-center py-4 text-secondary">
                <i className="bi bi-receipt fs-2 mb-2 d-block opacity-50"></i>
                <small>Đang nạp các giao dịch mới...</small>
              </div>
            ) : (
              <div className="table-responsive">
                <table className="modern-diagram-table">
                  <thead>
                    <tr>
                      <th>Mã GD</th>
                      <th>Học viên</th>
                      <th>Gia sư phụ trách</th>
                      <th>Lớp học</th>
                      <th>Tổng học phí</th>
                      <th>Hoa hồng sàn (20%)</th>
                      <th>Lương gia sư (80%)</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paymentsData.slice(0, 5).map((p, idx) => (
                      <tr key={idx}>
                        <td>
                          <strong>GD#{p.id || idx + 1}</strong>
                          <div className="text-secondary" style={{ fontSize: 10.5 }}>
                            {p.paidAt ? new Date(p.paidAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : 'Hôm nay'}
                          </div>
                        </td>
                        <td><strong>{p.studentName || 'Học viên'}</strong></td>
                        <td>{p.tutorName || 'Gia sư'}</td>
                        <td>
                          <span className="badge bg-secondary-subtle text-body fw-normal">
                            {p.className || 'Lớp học kèm'}
                          </span>
                        </td>
                        <td><strong className="text-danger">{formatVnd(p.amount)}</strong></td>
                        <td><span className="text-warning-emphasis fw-semibold">{formatVnd(p.platformFee || Math.round((p.amount || 0) * 0.2))}</span></td>
                        <td><strong className="text-success">{formatVnd(p.tutorPayout || Math.round((p.amount || 0) * 0.8))}</strong></td>
                        <td>
                          <span
                            className="badge rounded-pill"
                            style={{
                              background: p.status === 'Success' ? '#15803d' : '#b45309',
                              color: '#fff',
                              fontSize: 11
                            }}
                          >
                            {p.status === 'Success' ? 'Thành công' : 'Chờ xử lý'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* 8. PHÍM TẮT ĐIỀU HÀNH NHANH */}
      <div className="row g-4">
        <div className="col-12">
          <div className="diagram-card">
            <div className="d-flex align-items-center mb-3">
              <span className="diagram-header-badge" style={{ background: 'var(--dash-primary-soft)', color: '#DB303A' }}>
                <i className="bi bi-grid-fill"></i>
              </span>
              <div>
                <h6 className="fw-bold mb-0">Phím Tắt Điều Hành Module Quản Trị</h6>
                <small className="text-secondary">Truy cập tức thì tới các phân hệ nghiệp vụ chính</small>
              </div>
            </div>

            <div className="row g-2">
              <div className="col-6 col-sm-4 col-md-2">
                <Link to="/admin/tutor-approvals" className="p-2.5 rounded-3 border d-flex align-items-center gap-2 text-decoration-none text-body bg-light-subtle">
                  <i className="bi bi-person-check text-danger fs-5"></i>
                  <span className="small fw-semibold">Duyệt gia sư</span>
                </Link>
              </div>
              <div className="col-6 col-sm-4 col-md-2">
                <Link to="/admin/students" className="p-2.5 rounded-3 border d-flex align-items-center gap-2 text-decoration-none text-body bg-light-subtle">
                  <i className="bi bi-people text-primary fs-5"></i>
                  <span className="small fw-semibold">Học viên</span>
                </Link>
              </div>
              <div className="col-6 col-sm-4 col-md-2">
                <Link to="/admin/classrooms" className="p-2.5 rounded-3 border d-flex align-items-center gap-2 text-decoration-none text-body bg-light-subtle">
                  <i className="bi bi-calendar3 text-success fs-5"></i>
                  <span className="small fw-semibold">Lớp học</span>
                </Link>
              </div>
              <div className="col-6 col-sm-4 col-md-2">
                <Link to="/admin/reconciliation" className="p-2.5 rounded-3 border d-flex align-items-center gap-2 text-decoration-none text-body bg-light-subtle">
                  <i className="bi bi-graph-up text-warning fs-5"></i>
                  <span className="small fw-semibold">Doanh thu</span>
                </Link>
              </div>
              <div className="col-6 col-sm-4 col-md-2">
                <Link to="/admin/learning-documents" className="p-2.5 rounded-3 border d-flex align-items-center gap-2 text-decoration-none text-body bg-light-subtle">
                  <i className="bi bi-files text-info fs-5"></i>
                  <span className="small fw-semibold">Tài liệu</span>
                </Link>
              </div>
              <div className="col-6 col-sm-4 col-md-2">
                <Link to="/admin/ai" className="p-2.5 rounded-3 border d-flex align-items-center gap-2 text-decoration-none text-body bg-light-subtle">
                  <i className="bi bi-cpu text-danger fs-5"></i>
                  <span className="small fw-semibold">Tác tử AI</span>
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
