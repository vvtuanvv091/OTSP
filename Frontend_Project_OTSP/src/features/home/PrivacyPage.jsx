import { InfoLayout } from './InfoPages';

export default function PrivacyPage() {
  return (
    <InfoLayout
      eyebrow="AN TOÀN DỮ LIỆU & QUYỀN RIÊNG TƯ"
      title="Chính Sách Bảo Mật Thông Tin"
      subtitle="OTSP cam kết bảo vệ quyền riêng tư và dữ liệu cá nhân của học viên, phụ huynh và gia sư trên toàn bộ hệ thống."
    >
      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-shield-check" /> 1. Phạm vi thu thập dữ liệu
        </h2>
        <p className="text-secondary small lh-lg mb-0">
          Chúng tôi chỉ thu thập các thông tin cần thiết phục vụ trực tiếp cho việc định danh tài khoản, đối soát hồ sơ chuyên môn của gia sư (CCCD, bằng cấp/chứng chỉ), thông tin người giám hộ đối với học viên dưới 18 tuổi, lịch học và xác thực giao dịch học phí.
        </p>
      </div>

      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-camera-video-off-fill" /> 2. Quyền riêng tư trong Phòng học trực tuyến &amp; Tin nhắn
        </h2>
        <ul className="text-secondary small lh-lg mb-0">
          <li>
            <strong>Kiểm soát thiết bị (Camera &amp; Micro):</strong> Khi tham gia phòng học Jitsi Meet, hệ thống mặc định đặt trạng thái Tắt cho cả Camera và Micro. Người dùng hoàn toàn chủ động quyết định thời điểm bật thiết bị.
          </li>
          <li>
            <strong>Bảo mật nội dung hội thoại:</strong> Tin nhắn trao đổi học tập giữa Học viên và Gia sư được bảo vệ; Quản trị viên chỉ kiểm tra khi có báo cáo khiếu nại chính thức từ một trong hai bên.
          </li>
        </ul>
      </div>

      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-credit-card-2-front-fill" /> 3. Bảo mật giao dịch thanh toán
        </h2>
        <p className="text-secondary small lh-lg mb-0">
          Mọi giao dịch thanh toán học phí đều thực hiện qua chuẩn mã hóa ngân hàng VietQR và cổng đối soát tự động SePay. OTSP tuyệt đối không lưu trữ mật khẩu ngân hàng, mã OTP hay số thẻ tín dụng của người dùng.
        </p>
      </div>
    </InfoLayout>
  );
}
