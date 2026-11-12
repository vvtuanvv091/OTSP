import { useEffect, useState } from 'react';
import tutorApi from '../../api/tutorApi';

const formatCurrency = (value) => `${new Intl.NumberFormat('vi-VN').format(value || 0)} đ`;

export default function TutorIncomePage() {
  const [income, setIncome] = useState(null);
  const [selectedMonth, setSelectedMonth] = useState('');
  const [requestedMonth, setRequestedMonth] = useState('');
  const [loading, setLoading] = useState(true);
  const [savingBankAccount, setSavingBankAccount] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;
    async function loadIncome() {
      setLoading(true);
      setError('');
      try {
        const response = await tutorApi.getIncome(requestedMonth);
        if (!active) return;
        setIncome(response.data);
        setSelectedMonth(response.data?.selectedMonth || requestedMonth);
      } catch (requestError) {
        if (!active) return;
        console.error('Error fetching tutor income', requestError);
        setError(requestError.response?.data?.message || 'Không tải được dữ liệu thu nhập.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadIncome();
    return () => {
      active = false;
    };
  }, [requestedMonth]);

  const progress = Math.min(100, Math.max(0, income?.completionPercentage || 0));
  const saveBankAccount = async (event) => {
    event.preventDefault();
    setSavingBankAccount(true);
    setError('');
    setMessage('');
    try {
      const response = await tutorApi.saveIncomeBankAccount(income.bankAccount);
      setIncome((current) => ({ ...current, bankAccount: response.data.bankAccount }));
      setMessage(response.data?.message || 'Đã lưu thông tin tài khoản nhận tiền.');
    } catch (requestError) {
      console.error('Error saving tutor bank account', requestError);
      setError(requestError.response?.data?.message || 'Không thể lưu thông tin tài khoản nhận tiền.');
    } finally {
      setSavingBankAccount(false);
    }
  };
  const updateBankAccount = (field, value) => {
    setIncome((current) => ({
      ...current,
      bankAccount: { ...current.bankAccount, [field]: value },
    }));
  };

  return (
    <div className="tutor-page-container tutor-income-page">
      <div className="tutor-page-header d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3">
        <div>
          <div className="text-uppercase fw-bold text-muted small mb-1">TÀI CHÍNH / THU NHẬP</div>
          <h1 className="tutor-page-title mb-1">Thu nhập của tôi</h1>
          <p className="tutor-page-subtitle mb-0">Theo dõi buổi dạy và tiền thanh toán thực tế</p>
        </div>
        <label className="visually-hidden" htmlFor="incomeMonth">Chọn tháng</label>
        <select
          id="incomeMonth"
          className="form-select fw-semibold income-input"
          value={selectedMonth}
          onChange={(event) => {
            setSelectedMonth(event.target.value);
            setRequestedMonth(event.target.value);
          }}
          disabled={loading || !income?.availableMonths?.length}
        >
          {(income?.availableMonths || []).map((month) => (
            <option key={month} value={month}>Tháng {month}</option>
          ))}
        </select>
      </div>

      {error && <div className="alert alert-danger" role="alert">{error}</div>}
      {message && <div className="alert alert-success" role="status">{message}</div>}

      {loading && !income ? (
        <div className="text-center py-5" role="status" aria-live="polite">
          <span className="spinner-border text-danger" aria-hidden="true"></span>
          <span className="visually-hidden">Đang tải dữ liệu thu nhập...</span>
        </div>
      ) : income ? (
        <>
          {loading && <div className="small text-secondary mb-3" role="status">Đang cập nhật dữ liệu...</div>}

          <div className="row g-3 mb-3">
            <IncomeStat label="Tổng số buổi dạy" value={income.totalSessions} unit="buổi" detail="Theo lịch các lớp đang mở" />
            <IncomeStat label="Đã hoàn thành" value={income.completedSessions} unit="buổi" detail="Đủ điều kiện tính thu nhập" accent="success" />
            <IncomeStat label="Chờ xác nhận" value={income.pendingConfirmSessions} unit="buổi" detail="Chờ học viên xác nhận" accent="warning" />
          </div>

          <div className="row g-3 mb-3">
            <IncomeStat label="Buổi sắp diễn ra" value={income.upcomingSessions} unit="buổi" detail="Theo lịch dạy sắp tới" accent="info" />
            <IncomeStat label="Tổng giờ dạy" value={Number(income.totalTeachingHours || 0).toLocaleString('vi-VN')} unit="giờ" detail="Từ các buổi đã hoàn thành" />
            <IncomeStat label="Trung bình / buổi" value={formatCurrency(income.averageFeePerSession)} detail="Học phí trước hoa hồng" />
          </div>

          <div className="row g-3 mb-4">
            <IncomeStat label="Tổng học phí hoàn thành" value={formatCurrency(income.totalTuition)} detail={`${income.completedSessions || 0} buổi đã dạy xong`} />
            <IncomeStat label="Hoa hồng nền tảng (20%)" value={formatCurrency(income.platformCommission)} detail="Phí dịch vụ vận hành OTSP" accent="warning" />
            <IncomeStat label="Thu nhập gia sư (80%)" value={formatCurrency(income.tutorPayout)} detail="Chờ Admin quyết toán cuối tháng" accent="success" emphasized />
          </div>

          <section className="income-section-card mb-4" aria-labelledby="incomeProgressTitle">
            <div className="d-flex justify-content-between align-items-center gap-3 mb-3">
              <div>
                <h2 className="h5 fw-bold mb-1" id="incomeProgressTitle">Thống kê tiến độ giảng dạy</h2>
                <p className="text-muted small mb-0">Tình trạng các buổi học trong tháng</p>
              </div>
              <span className="badge rounded-pill bg-primary">
                Tháng {(selectedMonth || '').split('/')[0] || 'hiện tại'}
              </span>
            </div>
            <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 small mb-2">
              <span>
                Tiến độ hoàn thành:{' '}
                <strong className="text-success">
                  {income.completedSessions}/{income.totalSessions} buổi ({income.completionPercentage}%)
                </strong>
              </span>
              <span className="text-muted">Tổng dự kiến: {income.totalSessions} buổi</span>
            </div>
            <div
              className="progress income-progress"
              role="progressbar"
              aria-label="Tiến độ hoàn thành buổi học"
              aria-valuenow={progress}
              aria-valuemin="0"
              aria-valuemax="100"
            >
              <div className="progress-bar bg-success" style={{ width: `${progress}%` }}></div>
            </div>
            <div className="d-flex flex-wrap gap-4 pt-3 small">
              <span><i className="bi bi-circle-fill text-success me-2"></i>Đã hoàn thành: <strong>{income.completedSessions}</strong> buổi</span>
              <span><i className="bi bi-circle-fill text-warning me-2"></i>Chờ xác nhận: <strong>{income.pendingConfirmSessions}</strong> buổi</span>
              <span><i className="bi bi-circle-fill text-info me-2"></i>Sắp diễn ra: <strong>{income.upcomingSessions}</strong> buổi</span>
            </div>
          </section>

          <section className="income-section-card" aria-labelledby="payoutHistoryTitle">
            <h2 className="h5 fw-bold mb-1" id="payoutHistoryTitle">Lịch sử thanh toán</h2>
            <p className="text-muted small mb-3">Quyết toán thu nhập hàng tháng từ Admin</p>
            {income.payoutHistories?.length ? (
              <div className="table-responsive">
                <table className="table align-middle mb-0 tutor-custom-table">
                  <thead>
                    <tr>
                      <th scope="col">Kỳ quyết toán</th>
                      <th scope="col">Tổng học phí</th>
                      <th scope="col">Hoa hồng</th>
                      <th scope="col">Gia sư nhận</th>
                      <th scope="col">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {income.payoutHistories.map((item, index) => (
                      <tr key={`${item.period}-${index}`}>
                        <td>{item.period}</td>
                        <td>{formatCurrency(item.tuitionFee)}</td>
                        <td>{formatCurrency(item.commissionFee)}</td>
                        <td className="fw-semibold text-success">{formatCurrency(item.tutorReceive)}</td>
                        <td><span className="badge text-bg-success">{item.status}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="text-center text-secondary py-3">
                <i className="bi bi-clock-history fs-2 d-block mb-2" aria-hidden="true"></i>
                <div className="fw-semibold">Chưa có kỳ quyết toán nào</div>
                <div>Sau khi hoàn thành các buổi dạy trong tháng, Admin sẽ đối soát và giải ngân thu nhập định kỳ tại đây.</div>
              </div>
            )}
          </section>

          <section className="income-section-card" aria-labelledby="bankAccountTitle">
            <div className="d-flex justify-content-between align-items-center gap-3 mb-4">
              <div>
                <h2 className="h5 fw-bold mb-1" id="bankAccountTitle">Tài khoản nhận tiền &amp; Mã QR VietQR</h2>
                <p className="text-muted small mb-0">Thông tin admin sử dụng để quét mã chuyển tiền cuối tháng</p>
              </div>
              {income.bankAccount?.isUpdated && (
                <span className="badge rounded-pill text-bg-success">
                  <i className="bi bi-check-circle me-1" aria-hidden="true"></i>Đã cập nhật
                </span>
              )}
            </div>
            <div className="row g-4 align-items-center">
              <div className="col-12 col-lg-7">
                <form onSubmit={saveBankAccount}>
                  <div className="row g-3">
                    <div className="col-12 col-md-6">
                      <label className="form-label small fw-semibold" htmlFor="incomeBankName">Ngân hàng</label>
                      <select
                        id="incomeBankName"
                        className="form-select income-input w-100"
                        value={income.bankAccount?.bankName || ''}
                        onChange={(event) => updateBankAccount('bankName', event.target.value)}
                        required
                      >
                        <option value="">-- Chọn ngân hàng --</option>
                        {['MBBank', 'Vietcombank', 'Techcombank', 'ACB', 'VPBank', 'BIDV', 'Agribank', 'TPBank', 'Sacombank', 'OCB']
                          .map((bank) => <option key={bank} value={bank}>{bank}</option>)}
                      </select>
                    </div>
                    <div className="col-12 col-md-6">
                      <label className="form-label small fw-semibold" htmlFor="incomeAccountNumber">Số tài khoản</label>
                      <input
                        id="incomeAccountNumber"
                        className="form-control income-input w-100"
                        value={income.bankAccount?.accountNumber || ''}
                        onChange={(event) => updateBankAccount('accountNumber', event.target.value)}
                        maxLength="50"
                        autoComplete="off"
                        required
                      />
                    </div>
                    <div className="col-12 col-md-6">
                      <label className="form-label small fw-semibold" htmlFor="incomeAccountHolder">Chủ tài khoản</label>
                      <input
                        id="incomeAccountHolder"
                        className="form-control income-input w-100"
                        value={income.bankAccount?.accountHolder || ''}
                        onChange={(event) => updateBankAccount('accountHolder', event.target.value)}
                        maxLength="200"
                        autoComplete="name"
                        required
                      />
                    </div>
                    <div className="col-12 col-md-6">
                      <label className="form-label small fw-semibold" htmlFor="incomeCitizenId">CCCD / Định danh</label>
                      <input
                        id="incomeCitizenId"
                        className="form-control income-input w-100"
                        value={income.bankAccount?.citizenId || ''}
                        onChange={(event) => updateBankAccount('citizenId', event.target.value)}
                        maxLength="30"
                        autoComplete="off"
                      />
                    </div>
                  </div>
                  <div className="d-flex flex-wrap justify-content-between align-items-center gap-3 mt-4 pt-3 border-top">
                    <span className="text-muted small fst-italic">Mã QR bên phải tự động đồng bộ theo tài khoản</span>
                    <button type="submit" className="btn btn-primary px-4" disabled={savingBankAccount}>
                      {savingBankAccount ? 'Đang lưu...' : 'Lưu thông tin & Tạo QR'}
                    </button>
                  </div>
                </form>
              </div>
              <div className="col-12 col-lg-5">
                <div className="tutor-qr-box">
                  <div className="small fw-bold mb-2">
                    <i className="bi bi-qr-code-scan me-1 text-primary" aria-hidden="true"></i>
                    Mã VietQR nhận tiền của bạn
                  </div>
                  {income.bankAccount?.qrImageUrl ? (
                    <img
                      src={income.bankAccount.qrImageUrl}
                      alt={`Mã VietQR ${income.bankAccount.bankName} ${income.bankAccount.accountNumber}`}
                      className="tutor-qr-image"
                      loading="lazy"
                    />
                  ) : (
                    <p className="text-muted small mb-0">Nhập tài khoản ngân hàng để tạo mã VietQR.</p>
                  )}
                  <div className="small text-muted mt-2">
                    {income.bankAccount?.bankName} · {income.bankAccount?.accountNumber}
                  </div>
                  <div className="small fw-semibold">{income.bankAccount?.accountHolder}</div>
                </div>
              </div>
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}

function IncomeStat({ label, value, unit, detail, accent = '', emphasized = false }) {
  return (
    <div className="col-12 col-md-4">
      <div className={`income-stat-card ${accent ? `income-stat-${accent}` : ''} ${emphasized ? 'income-stat-emphasized' : ''}`}>
        <div>
          <div className="income-stat-label">{label}</div>
          <div className="income-stat-value">
            {value} {unit && <span className="income-stat-unit">{unit}</span>}
          </div>
        </div>
        <p className="income-stat-subtext">{detail}</p>
      </div>
    </div>
  );
}
