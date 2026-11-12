import '../../styles/components/InvoiceModal.css';

const formatMoney = (val) =>
  new Intl.NumberFormat('vi-VN').format(val || 0) + ' đ';

// Hàm đọc số tiền thành chữ tiếng Việt
function numberToVietnameseWords(num) {
  if (!num || isNaN(num) || num <= 0) return 'Không đồng';
  const units = ['', 'nghìn', 'triệu', 'tỷ', 'nghìn tỷ', 'triệu tỷ'];
  const digits = ['không', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín'];

  function readThreeDigits(n, full) {
    let hundred = Math.floor(n / 100);
    let ten = Math.floor((n % 100) / 10);
    let unit = n % 10;
    let res = '';

    if (hundred > 0 || full) {
      res += digits[hundred] + ' trăm ';
    }
    if (ten > 1) {
      res += digits[ten] + ' mươi ';
    } else if (ten === 1) {
      res += 'mười ';
    } else if (full && unit > 0) {
      res += 'lẻ ';
    }

    if (ten > 1 && unit === 1) {
      res += 'mốt ';
    } else if (ten > 0 && unit === 5) {
      res += 'lăm ';
    } else if (unit > 0) {
      res += digits[unit] + ' ';
    }
    return res.trim();
  }

  let strNum = Math.floor(num).toString();
  let groups = [];
  while (strNum.length > 0) {
    groups.unshift(parseInt(strNum.slice(-3), 10));
    strNum = strNum.slice(0, -3);
  }

  let result = '';
  for (let i = 0; i < groups.length; i++) {
    let g = groups[i];
    if (g > 0) {
      let gStr = readThreeDigits(g, i > 0);
      let unit = units[groups.length - 1 - i];
      result += gStr + (unit ? ' ' + unit : '') + ' ';
    }
  }

  result = result.trim();
  if (!result) return 'Không đồng';
  return result.charAt(0).toUpperCase() + result.slice(1) + ' đồng chẵn.';
}

export default function InvoiceModal({ payment, currentUser, guardianName, isOpen, onClose }) {
  if (!isOpen || !payment) return null;

  const handlePrint = () => {
    window.print();
  };

  const formattedDate = (dateVal) => {
    if (!dateVal) return '—';
    try {
      const d = new Date(dateVal);
      return d.toLocaleDateString('vi-VN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      });
    } catch {
      return String(dateVal);
    }
  };

  const isOneToOne =
    payment.classroomType === 'OneToOne' ||
    payment.classroomType === 0 ||
    payment.classroomType === '0';

  const classroomTypeLabel = isOneToOne ? 'Dạy kèm 1-1' : 'Lớp học nhóm';
  const sessionsPerWeek = payment.sessionsPerWeek || 2;
  const hoursPerSession = payment.hoursPerSession || 2.0;

  return (
    <>
      {/* Backdrop */}
      <div
        className="modal-backdrop fade show no-print"
        style={{ zIndex: 1060 }}
        onClick={onClose}
      ></div>

      {/* Modal Dialog */}
      <div
        className="modal fade show d-block"
        tabIndex={-1}
        role="dialog"
        style={{ zIndex: 1065 }}
        onClick={(e) => {
          if (e.target === e.currentTarget) onClose();
        }}
      >
        <div className="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable" role="document">
          <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden bg-white">
            {/* Modal Header */}
            <div className="modal-header border-bottom py-3 px-4 bg-white d-flex justify-content-between align-items-center no-print">
              <div className="d-flex align-items-center gap-2">
                <span
                  className="d-inline-flex align-items-center justify-content-center rounded-3 text-white fw-bold px-2 py-1"
                  style={{ backgroundColor: '#DB303A', fontSize: 15 }}
                >
                  OTSP
                </span>
                <h2 className="modal-title h5 fw-bold mb-0 text-dark">
                  Hóa đơn học phí gia sư
                </h2>
              </div>
              <div className="d-flex align-items-center gap-2">
                <button
                  type="button"
                  className="btn btn-sm btn-danger fw-semibold d-inline-flex align-items-center gap-1.5 shadow-sm px-3 py-1.5"
                  style={{ backgroundColor: '#DB303A' }}
                  onClick={handlePrint}
                >
                  <i className="bi bi-printer"></i> In / Tải PDF
                </button>
                <button
                  type="button"
                  className="btn-close"
                  onClick={onClose}
                  aria-label="Đóng"
                ></button>
              </div>
            </div>

            {/* Modal Body */}
            <div className="modal-body p-3 p-md-4 bg-light">
              {/* KHU VỰC HÓA ĐƠN ĐƯỢC IN / XUẤT PDF */}
              <div
                id="otsp-invoice-print-area"
                className="bg-white p-4 p-md-5 rounded-4 shadow-sm border mx-auto text-dark"
                style={{ maxWidth: 760, fontFamily: 'system-ui, -apple-system, sans-serif' }}
              >
                {/* Header phiếu */}
                <div className="d-flex justify-content-between align-items-start border-bottom pb-3 mb-4 flex-wrap gap-2">
                  <div>
                    <div className="d-flex align-items-center gap-2 mb-1">
                      <span
                        className="d-inline-flex align-items-center justify-content-center rounded-2 text-white fw-bold px-2 py-0.5"
                        style={{ backgroundColor: '#DB303A', fontSize: 14 }}
                      >
                        OTSP
                      </span>
                      <strong className="fs-5 text-dark">Nền tảng Gia sư OTSP</strong>
                    </div>
                    <div className="small text-secondary">Hóa đơn xác nhận đăng ký học gia sư</div>
                  </div>

                  <div className="text-md-end">
                    <div className="fw-bold text-dark fs-6">
                      Mã hóa đơn: <span className="text-danger">HD-{payment.invoiceId || payment.paymentId}</span>
                    </div>
                    <div className="small text-secondary">
                      Mã giao dịch: <strong>#PAY-{payment.paymentId}</strong>
                    </div>
                    <div className="small text-secondary">
                      Thời gian: {formattedDate(payment.paidAt)}
                    </div>
                  </div>
                </div>

                {/* Tiêu đề chính */}
                <div className="text-center mb-4">
                  <h3 className="h5 fw-bold text-uppercase text-dark mb-1">
                    CHI TIẾT HÓA ĐƠN THANH TOÁN
                  </h3>
                  <span className="badge bg-white text-success border border-success px-3 py-1 rounded-pill small">
                    <i className="bi bi-check-circle-fill me-1"></i> Đã hoàn tất thanh toán
                  </span>
                </div>

                {/* Khối thông tin: Người thanh toán (Học viên) & Người nhận (Gia sư) */}
                <div className="row g-3 mb-4">
                  <div className="col-12 col-md-6">
                    <div className="p-3 rounded-3 border bg-white h-100">
                      <div className="fw-bold text-dark small text-uppercase mb-2 pb-1 border-bottom">
                        <i className="bi bi-person-fill text-danger me-1"></i> Người thanh toán (Học viên)
                      </div>
                      <div className="small mb-1">
                        <span className="text-secondary">Họ và tên:</span>{' '}
                        <strong className="text-dark">{currentUser?.fullName || 'Học viên'}</strong>
                      </div>
                      {guardianName && (
                        <div className="small mb-1">
                          <span className="text-secondary">Người giám hộ:</span>{' '}
                          <strong className="text-dark">{guardianName}</strong>
                        </div>
                      )}
                      <div className="small mb-1">
                        <span className="text-secondary">Email:</span>{' '}
                        <span className="text-dark">{currentUser?.email || '—'}</span>
                      </div>
                      {currentUser?.phoneNumber && (
                        <div className="small">
                          <span className="text-secondary">Số điện thoại:</span>{' '}
                          <span className="text-dark">{currentUser.phoneNumber}</span>
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="col-12 col-md-6">
                    <div className="p-3 rounded-3 border bg-white h-100">
                      <div className="fw-bold text-dark small text-uppercase mb-2 pb-1 border-bottom">
                        <i className="bi bi-mortarboard-fill text-danger me-1"></i> Người nhận (Gia sư phụ trách)
                      </div>
                      <div className="small mb-1">
                        <span className="text-secondary">Gia sư:</span>{' '}
                        <strong className="text-dark">{payment.tutorName}</strong>
                      </div>
                      <div className="small mb-1">
                        <span className="text-secondary">Môn học:</span>{' '}
                        <strong className="text-dark">{payment.subjectName}</strong> ({payment.gradeLevelName})
                      </div>
                      <div className="small mb-1">
                        <span className="text-secondary">Lớp học:</span>{' '}
                        <span className="text-dark">{payment.classroomName}</span>
                      </div>
                      <div className="small">
                        <span className="text-secondary">Hình thức:</span>{' '}
                        <span className="text-dark">{classroomTypeLabel}</span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Bảng chi tiết môn học, giá và thời gian học bao lâu */}
                <div className="table-responsive mb-4">
                  <table className="table table-bordered mb-0 align-middle">
                    <thead className="table-light text-center small text-uppercase">
                      <tr>
                        <th style={{ width: '8%' }}>STT</th>
                        <th className="text-start">Môn học &amp; Lớp học</th>
                        <th style={{ width: '32%' }}>Thời lượng học</th>
                        <th style={{ width: '25%' }} className="text-end">Học phí</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr>
                        <td className="text-center fw-medium">01</td>
                        <td>
                          <strong className="text-dark d-block">{payment.subjectName} - {payment.classroomName}</strong>
                          <span className="small text-secondary">
                            Cấp học: {payment.gradeLevelName} • Gia sư: {payment.tutorName}
                          </span>
                        </td>
                        <td>
                          <div className="small text-dark fw-semibold">
                            {sessionsPerWeek} buổi / tuần
                          </div>
                          <div className="small text-secondary">
                            {hoursPerSession} giờ / buổi ({classroomTypeLabel})
                          </div>
                        </td>
                        <td className="text-end fw-bold text-dark fs-6">
                          {formatMoney(payment.amount)}
                        </td>
                      </tr>
                    </tbody>
                    <tfoot>
                      <tr>
                        <td colSpan="3" className="text-end text-dark fw-bold">
                          Tổng số tiền thanh toán:
                        </td>
                        <td className="text-end fw-bold text-danger fs-5" style={{ color: '#DB303A' }}>
                          {formatMoney(payment.amount)}
                        </td>
                      </tr>
                    </tfoot>
                  </table>
                </div>

                {/* Số tiền bằng chữ */}
                <div className="p-3 rounded-3 bg-light border mb-4">
                  <span className="small text-secondary">Số tiền thanh toán bằng chữ:</span>
                  <div className="fw-semibold text-dark fst-italic mt-0.5">
                    {numberToVietnameseWords(payment.amount)}
                  </div>
                </div>

                {/* Thông tin xác nhận */}
                <div className="d-flex justify-content-between align-items-center flex-wrap pt-2 border-top gap-2">
                  <div className="small text-secondary">
                    <div>Trạng thái: <strong className="text-success">Đã hoàn tất thanh toán</strong></div>
                    <div>Phương thức: <strong>Chuyển khoản SePay / VietQR</strong></div>
                  </div>
                  <div className="text-md-end text-secondary small fst-italic">
                    Xác nhận thanh toán tự động qua hệ thống OTSP
                  </div>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="modal-footer border-top py-2.5 px-4 bg-white d-flex justify-content-end gap-2 no-print">
              <button
                type="button"
                className="btn btn-sm btn-outline-secondary px-3"
                onClick={onClose}
              >
                Đóng
              </button>
              <button
                type="button"
                className="btn btn-sm btn-danger px-3 shadow-sm d-flex align-items-center gap-1.5"
                style={{ backgroundColor: '#DB303A' }}
                onClick={handlePrint}
              >
                <i className="bi bi-printer"></i> In / Tải PDF
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
