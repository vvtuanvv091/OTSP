import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
import os

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def set_table_borders(table, color="D3D3D3", sz="4", val="single"):
    tblPr = table._tbl.tblPr
    tblBorders = OxmlElement('w:tblBorders')
    for border_name in ['top', 'left', 'bottom', 'right', 'insideH', 'insideV']:
        border = OxmlElement(f'w:{border_name}')
        border.set(qn('w:val'), val)
        border.set(qn('w:sz'), sz)
        border.set(qn('w:space'), '0')
        border.set(qn('w:color'), color)
        tblBorders.append(border)
    tblPr.append(tblBorders)

def set_cell_background(cell, fill_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), fill_hex)
    tcPr.append(shd)

doc = docx.Document()

# Margins: Top 2cm, Bottom 2cm, Left 3cm, Right 2cm
for section in doc.sections:
    section.top_margin = Inches(0.79)
    section.bottom_margin = Inches(0.79)
    section.left_margin = Inches(1.18)
    section.right_margin = Inches(0.79)

style = doc.styles['Normal']
font = style.font
font.name = 'Times New Roman'
font.size = Pt(13)
font.color.rgb = RGBColor(0, 0, 0)
style.paragraph_format.line_spacing = 1.15
style.paragraph_format.space_after = Pt(2.5)

# Helper function
def add_p(indent_inches, bold_prefix, text, space_after=2, space_before=0, italic=False):
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Inches(indent_inches)
    p.paragraph_format.space_before = Pt(space_before)
    p.paragraph_format.space_after = Pt(space_after)
    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.bold = True
    if text:
        r_txt = p.add_run(text)
        if italic:
            r_txt.font.italic = True
    return p

# ----------------- HEADER TABLE -----------------
header_table = doc.add_table(rows=1, cols=2)
header_table.alignment = WD_TABLE_ALIGNMENT.CENTER
header_table.autofit = False

col_widths = [Inches(2.7), Inches(3.8)]
row = header_table.rows[0]

# Left
cell_left = row.cells[0]
cell_left.width = col_widths[0]
p = cell_left.paragraphs[0]
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(2)
r = p.add_run("CÔNG TY CỔ PHẦN\nCÔNG NGHỆ HINOVA")
r.font.bold = True
r.font.size = Pt(12)

p = cell_left.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(0)
r = p.add_run("Số:       /QĐ-HINOVA")
r.font.size = Pt(12)

# Right
cell_right = row.cells[1]
cell_right.width = col_widths[1]
p = cell_right.paragraphs[0]
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(1)
r = p.add_run("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
r.font.bold = True
r.font.size = Pt(12)

p = cell_right.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(2)
r = p.add_run("Độc lập - Tự do - Hạnh phúc")
r.font.bold = True
r.font.size = Pt(12)

p = cell_right.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(3)
r = p.add_run("-----------------------")
r.font.bold = True
r.font.size = Pt(10)

p = cell_right.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(0)
r = p.add_run("Đồng Nai, ngày      tháng      năm 2026")
r.font.italic = True
r.font.size = Pt(12)

p_space = doc.add_paragraph()
p_space.paragraph_format.space_after = Pt(4)

# Title
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(2)
r = p.add_run("QUYẾT ĐỊNH")
r.font.bold = True
r.font.size = Pt(14)

p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(8)
r = p.add_run("Về việc phân công nhiệm vụ thực hiện dự án: Đầu tư hệ thống Camera an ninh\ntrên địa bàn xã Bắc Tân Uyên")
r.font.bold = True
r.font.size = Pt(13)

p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_after = Pt(10)
r = p.add_run("GIÁM ĐỐC CÔNG TY CỔ PHẦN CÔNG NGHỆ HINOVA")
r.font.bold = True
r.font.size = Pt(13)

# Bases
bases = [
    "Căn cứ Luật Doanh nghiệp số 59/2020/QH14 ngày 17 tháng 6 năm 2020 của Quốc hội;",
    "Căn cứ Luật Đầu tư công số 58/2024/QH15 ngày 29 tháng 11 năm 2024 của Quốc hội;",
    "Căn cứ Luật Chuyển đổi số 148/2025/QH15 ngày 11 tháng 12 năm 2025 của Quốc hội;",
    "Căn cứ Nghị định số 85/2025/NĐ-CP ngày 08 tháng 4 năm 2025 và Nghị định số 275/2025/NĐ-CP ngày 18 tháng 10 năm 2025 của Chính phủ quy định chi tiết thi hành Luật Đầu tư công;",
    "Căn cứ Kế hoạch số 305/KH-CATP ngày 21 tháng 01 năm 2026 của Công an Thành phố Hồ Chí Minh về việc rà soát, kết nối hệ thống camera từ các chung cư về Công an cấp xã và Trung tâm thông tin chỉ huy Công an Thành phố Hồ Chí Minh;",
    "Căn cứ Quyết định số 766/QĐ-UBND ngày 29 tháng 4 năm 2026 của Ủy ban nhân dân xã Bắc Tân Uyên về chủ trương đầu tư dự án: Đầu tư hệ thống camera an ninh trên địa bàn xã Bắc Tân Uyên;",
    "Căn cứ Điều lệ tổ chức và hoạt động của Công ty Cổ phần Công nghệ HINOVA;",
    "Căn cứ hồ sơ Nhiệm vụ thiết kế (bước lập Báo cáo kinh tế - kỹ thuật) dự án Đầu tư hệ thống camera an ninh trên địa bàn xã Bắc Tân Uyên do Công ty Cổ phần Công nghệ HINOVA xác lập;",
    "Theo đề nghị của Trưởng Phòng Dự án,"
]
for b in bases:
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Inches(0.2)
    p.paragraph_format.space_after = Pt(2)
    r = p.add_run(b)
    r.font.italic = True
    r.font.size = Pt(12.5)

p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_before = Pt(6)
p.paragraph_format.space_after = Pt(6)
r = p.add_run("QUYẾT ĐỊNH:")
r.font.bold = True
r.font.size = Pt(13)

# Article 1
p = doc.add_paragraph()
p.paragraph_format.space_after = Pt(4)
r = p.add_run("Điều 1. ")
r.font.bold = True
p.add_run("Phân công nhiệm vụ thực hiện dự án: ")
r = p.add_run("Đầu tư hệ thống Camera an ninh trên địa bàn xã Bắc Tân Uyên")
r.font.bold = True
p.add_run(" (Địa điểm: Xã Bắc Tân Uyên, Thành phố Hồ Chí Minh; Chủ đầu tư: Ban Quản lý dự án Đầu tư xây dựng xã Bắc Tân Uyên; Cơ quan lập: Công ty Cổ phần Công nghệ HINOVA; Tổng mức đầu tư dự kiến: 15.000.000.000 đồng), cụ thể như sau:")

# I. Phân công nhiệm vụ cho thành viên thực hiện dự án
add_p(0, "I. Phân công nhiệm vụ cho thành viên thực hiện dự án", "", space_before=4, space_after=3)

# 1. Phụ trách chung dự án
add_p(0, "1. Phụ trách chung dự án:", "", space_before=2, space_after=2)

# 1.1 Trưởng phòng Dự án
add_p(0.2, "1.1. [Ông/Bà…………], Trưởng phòng Dự án: Phụ trách chung dự án, quản lý, điều hành, hành chính tổng hợp:", "", space_after=2)
add_p(0.4, "1.1.1) ", "Xây dựng kế hoạch tổng thể, điều phối và kiểm soát toàn diện tiến độ 45 ngày của dự án theo đúng quy định tại Mục IX Nhiệm vụ thiết kế:")
add_p(0.6, "a) ", "Giai đoạn 1 - Chuẩn bị và lập kế hoạch khảo sát chi tiết (02 ngày): Nghiên cứu hồ sơ Báo cáo đề xuất chủ trương đầu tư, chuẩn bị dụng cụ, thiết bị đo đạc, biểu mẫu khảo sát, họp đội khảo sát phân công nhiệm vụ và liên hệ Công an xã, UBND xã thu xếp lịch ra quân;")
add_p(0.6, "b) ", "Giai đoạn 2 - Khảo sát thực địa tại 20 tuyến điểm lắp đặt camera (07 ngày): Điều phối đoàn khảo sát, bố trí mỗi ngày từ 2 đến 3 tuyến điểm theo nhóm địa lý để tối ưu hóa di chuyển, thu thập tọa độ GPS, hiện trạng hạ tầng điện, mạng và lập biên bản khảo sát có xác nhận;")
add_p(0.6, "c) ", "Giai đoạn 3 - Khảo sát Trung tâm giám sát an ninh và thu thập báo giá thiết bị (03 ngày): Khảo sát toàn diện phòng trung tâm điều hành tại Công an xã và gửi, thu thập báo giá từ tối thiểu 03 nhà cung cấp cho từng hạng mục thiết bị chính, kể cả báo giá cước đường truyền internet;")
add_p(0.6, "d) ", "Giai đoạn 4 - Tổng hợp, phân tích và hoàn thiện hồ sơ khảo sát (03 ngày): Nhập liệu, lập bảng tổng hợp danh mục thiết bị, sơ đồ mặt bằng tổng thể vị trí 20 tuyến điểm, tổng hợp báo giá và hoàn thiện Báo cáo kết quả khảo sát trình Chủ đầu tư nghiệm thu;")
add_p(0.6, "đ) ", "Lập Báo cáo kinh tế - kỹ thuật (30 ngày): Triển khai tính toán thiết kế chi tiết các bộ môn và dự toán kể từ khi Báo cáo kết quả khảo sát được nghiệm thu (không kể thời gian thẩm định, phê duyệt).")

add_p(0.4, "1.1.2) ", "Quản lý, điều hành toàn diện công tác triển khai dự án và trực tiếp thực hiện các nhiệm vụ sau:")
add_p(0.6, "a) ", "Làm đầu mối chính thức của Công ty HINOVA làm việc, phối hợp với Ban Quản lý dự án Đầu tư xây dựng xã Bắc Tân Uyên (Chủ đầu tư), Công an xã và UBND xã Bắc Tân Uyên trong suốt quá trình triển khai;")
add_p(0.6, "b) ", "Kiểm soát mục tiêu, quy mô và hạn mức đầu tư dự án: Bảo đảm tổng mức dự toán nằm trong phạm vi 15.000.000.000 đồng (Mười lăm tỷ đồng), dự án nhóm C, 100% vốn ngân sách bổ sung có mục tiêu theo đúng Quyết định số 766/QĐ-UBND;")
add_p(0.6, "c) ", "Điều hành chung hoạt động của Bộ phận Kỹ thuật, Kế toán - Tài chính, Pháp chế - Hợp đồng và Đào tạo chuyển giao; đôn đốc, đánh giá việc thực hiện nhiệm vụ của các thành viên;")
add_p(0.6, "d) ", "Chủ trì các cuộc họp giao ban định kỳ hàng tuần và đột xuất; chỉ đạo xử lý kịp thời các vướng mắc kỹ thuật, mặt bằng phát sinh tại hiện trường 20 tuyến điểm;")
add_p(0.6, "đ) ", "Chủ trì thẩm định nội bộ, ký xác nhận Báo cáo kết quả khảo sát thực địa và phê duyệt hồ sơ Báo cáo kinh tế - kỹ thuật trước khi trình Chủ đầu tư tổ chức nghiệm thu;")
add_p(0.6, "e) ", "Chủ trì bảo vệ đồ án thiết kế, giải trình các nội dung kỹ thuật và dự toán trước các cơ quan chuyên môn, hội đồng thẩm định cho đến khi có quyết định phê duyệt;")
add_p(0.6, "g) ", "Thực hiện các nhiệm vụ quản lý, điều hành khác theo phân công của Giám đốc Công ty và quy chế hoạt động của Công ty.")

# 1.2 Văn phòng dự án
add_p(0.2, "1.2. [Ông/Bà…………], Văn phòng dự án: Thống kê, hồ sơ, hậu cần:", "", space_before=2, space_after=2)
add_p(0.4, "a) ", "Tiếp nhận, đăng ký, vào sổ theo dõi, phân loại và lưu trữ toàn bộ công văn đi, văn bản đến liên quan đến dự án Camera an ninh xã Bắc Tân Uyên;")
add_p(0.4, "b) ", "Lập, phân loại, bảo mật và lưu trữ an toàn hồ sơ dự án: Quyết định chủ trương đầu tư số 766/QĐ-UBND, 20 biên bản khảo sát thực địa gốc, các bản báo giá thiết bị, bản vẽ thiết kế thi công, dự toán và các văn bản thẩm định, phê duyệt;")
add_p(0.4, "c) ", "Thực hiện công tác thống kê tiến độ: Theo dõi tiến độ chi tiết 45 ngày của từng bộ phận, định kỳ hàng tuần lập báo cáo thống kê tiến độ gửi Trưởng phòng Dự án và Ban Giám đốc Công ty;")
add_p(0.4, "d) ", "Theo dõi, thống kê nhân sự tham gia dự án; phối hợp với Kế toán dự án tổng hợp chấm công, đề xuất chế độ công tác phí cho đoàn khảo sát tại địa bàn xã Bắc Tân Uyên;")
add_p(0.4, "đ) ", "Chuẩn bị các điều kiện vật chất, hậu cần, phương tiện đi lại, trang thiết bị đo đạc kỹ thuật (máy định vị GPS, thước đo laser, máy ảnh...) phục vụ các đợt khảo sát hiện trường;")
add_p(0.4, "e) ", "Tham gia ghi biên bản đầy đủ, chính xác các cuộc họp giữa Công ty HINOVA với Ban Quản lý dự án, UBND xã và Công an xã Bắc Tân Uyên; đôn đốc thực hiện kết luận cuộc họp;")
add_p(0.4, "g) ", "Quản lý và duy trì việc cập nhật dữ liệu dự án trên hệ thống phần mềm quản lý văn bản, điều hành tác nghiệp và lưu trữ số của Công ty HINOVA;")
add_p(0.4, "h) ", "Thực hiện các nhiệm vụ hậu cần, văn phòng khác theo sự phân công của Trưởng phòng Dự án.")

# 2. Bộ phận Kỹ thuật
add_p(0, "2. Bộ phận Kỹ thuật:", "", space_before=4, space_after=2)

# 2.1 Khảo sát, thiết kế, thi công
add_p(0.2, "2.1. [Ông/Bà…………], Kỹ sư Phụ trách Khảo sát, thiết kế, thi công:", "", space_after=2)
add_p(0.4, "a) ", "Lập kế hoạch khảo sát chi tiết 20 tuyến điểm ngoài trời, chuẩn bị biểu mẫu khảo sát, máy định vị GPS chuyên dụng, thước đo laser phục vụ Giai đoạn 2 (07 ngày);")
add_p(0.4, "b) ", "Trực tiếp khảo sát thực địa đối với toàn bộ 20 tuyến điểm theo đúng Mục VI Nhiệm vụ thiết kế:")

# 20 Points
points = [
    ("Tuyến 1", "ĐH 411 ấp Tân Lợi, xã Bắc Tân Uyên (gần Cầu Tân Lợi)", "Cửa ngõ vào xã phía Bắc"),
    ("Tuyến 2", "ĐH 411 + ĐH 436 ấp Tân Lợi, xã Bắc Tân Uyên (Ngã 3 cây trắc)", "Điểm giao cắt quan trọng"),
    ("Tuyến 3", "ĐH 436 + Đất Cuốc 09 (Ngã 3 suối sâu)", "Khu vực nguy cơ cao"),
    ("Tuyến 4", "ĐH 436 + ĐH 415 (Ngã 3 Công an xã Đất Cuốc cũ)", "Giao lộ trọng điểm"),
    ("Tuyến 5", "ĐH 415 + ĐH 414 (Ngã 3 Năm Đưa)", "Tuyến liên ấp"),
    ("Tuyến 6", "ĐH 415 + Đất Cuốc 18 (Ngã 3 Ba Quyên)", "Tuyến liên ấp"),
    ("Tuyến 7", "ĐH 411 (Cổng Khu Công nghiệp Đất Cuốc)", "Khu công nghiệp - ưu tiên cao"),
    ("Tuyến 8", "ĐH 411 + ĐH 415 (Ngã 3 Công an xã)", "Trụ sở Công an"),
    ("Tuyến 9", "ĐH 411 + Trục chính", "Cửa ngõ trung tâm"),
    ("Tuyến 10", "ĐH 411 + Tân Thành 27 (Đội sản xuất Nhà Nai)", "Khu dân cư"),
    ("Tuyến 11", "ĐH 411 + ĐT 746B (Ngã 3 Công an Tân Thành cũ)", "Giao lộ chính"),
    ("Tuyến 12", "ĐT 746, ấp Tân Thành 1 (gần Tân Lập 01)", "Tuyến đường chính"),
    ("Tuyến 13", "ĐT 746 + Tân Thành 01, ấp Tân Thành 1 (lối vào Chợ Tân Thành)", "Khu vực chợ"),
    ("Tuyến 14", "ĐH 411 + Tân Thành 43", "Khu dân cư"),
    ("Tuyến 15", "ĐT 746 + Tạo Lực (Ngã 3 tử thần)", "Điểm tai nạn thường xuyên"),
    ("Tuyến 16", "Tạo Lực + Vàm Tư + Tân Định 61", "Khu vực phức tạp"),
    ("Tuyến 17", "Tạo Lực + Tân Định (gần Cầu Tam Lập)", "Cửa ngõ phía Nam"),
    ("Tuyến 18", "ĐT 746 (Chợ Tân Định cũ)", "Khu vực chợ"),
    ("Tuyến 19", "ĐT 746 + ĐH 415", "Giao lộ quan trọng"),
    ("Tuyến 20", "Ngã 3 hướng vào Trung tâm điều dưỡng Tâm Thần", "Khu vực đặc thù")
]
for pt_num, pt_loc, pt_note in points:
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Inches(0.6)
    p.paragraph_format.space_after = Pt(1.5)
    r_pt = p.add_run(f"- {pt_num}: ")
    r_pt.font.bold = True
    p.add_run(f"{pt_loc} - ")
    r_nt = p.add_run(pt_note)
    r_nt.font.italic = True

add_p(0.4, "c) ", "Đo đạc tọa độ GPS chính xác (hệ tọa độ VN-2000/WGS-84), chụp ảnh hiện trạng 4 hướng và toàn cảnh, đánh dấu vị trí trên bản đồ phục vụ lập sơ đồ mặt bằng tổng thể 20 tuyến điểm;")
add_p(0.4, "d) ", "Ghi nhận hiện trạng cột điện lực, cột đèn chiếu sáng hiện hữu tại từng điểm; ưu tiên tối đa việc tận dụng hạ tầng hiện hữu để gắn camera nhằm tiết kiệm chi phí xây dựng mới;")
add_p(0.4, "đ) ", "Khảo sát và tính toán kết cấu móng trụ mới: Tính toán chịu lực, móng bê tông cốt thép và kiểm toán tải trọng gió đối với trụ thép đa giác cao 6m theo tiêu chuẩn TCVN 2737:2023; quy định mạ kẽm nhúng nóng toàn bộ trụ và tay vươn theo tiêu chuẩn TCVN 7538-2:2005 (ISO 1461);")
add_p(0.4, "e) ", "Thiết kế chi tiết các loại tay vươn trụ đa giác: Tay vươn 1m (05 cái), 2m (06 cái), 3m (04 cái), 4m (06 cái), 6m (13 cái) và 9m (06 cái) phù hợp với góc quan sát từng giao lộ;")
add_p(0.4, "g) ", "Thiết kế vị trí lắp đặt 28 tủ kỹ thuật ngoài trời chuyên dụng treo cao 2,5m trên thân trụ, bảo đảm an toàn giao thông, hành lang an toàn đường bộ và mỹ quan đô thị;")
add_p(0.4, "h) ", "Khảo sát nguồn điện tại 28 vị trí camera cần đăng ký cấp điện lần đầu (hạ đồng hồ); thiết kế cáp điện cấp nguồn tổng (2.800m), cáp nguồn camera (330m) tuân thủ quy chuẩn điện QCVN QTĐ-5:2009/BCT;")
add_p(0.4, "i) ", "Thiết kế hệ thống tiếp địa chống sét lan truyền tại 28 điểm camera và giếng khoan tiếp địa sâu 25m tại phòng trung tâm theo tiêu chuẩn TCVN 9385:2012 (IEC 62305);")
add_p(0.4, "k) ", "Khảo sát, thiết kế hệ thống báo cháy tự động và PCCC cho phòng trung tâm điều hành theo QCVN 06:2022/BXD, TCVN 5760:1993 và TCVN 7568:2015;")
add_p(0.4, "l) ", "Lập biên bản khảo sát hiện trường từng điểm, lấy xác nhận của Công an xã hoặc UBND xã; bóc tách khối lượng xây lắp, kết cấu và phối hợp lập dự toán công trình;")
add_p(0.4, "m) ", "Giám sát kỹ thuật thi công lắp dựng móng trụ, cần vươn, kéo cáp và hoàn trả mặt bằng khi dự án bước vào giai đoạn thi công;")
add_p(0.4, "n) ", "Thực hiện các nhiệm vụ kỹ thuật xây dựng, thi công khác do Trưởng phòng Dự án phân công.")

# 2.2 Hạ tầng mạng, thiết bị, vận hành, bảo hành
add_p(0.2, "2.2. [Ông/Bà…………], Kỹ sư Phụ trách Hạ tầng mạng, thiết bị, vận hành, bảo hành:", "", space_before=2, space_after=2)
add_p(0.4, "a) ", "Khảo sát hạ tầng mạng cáp quang viễn thông, sóng di động tại 20 tuyến điểm, bảo đảm đáp ứng băng thông tối thiểu 4 Mbps cho mỗi camera hiện trường truyền dữ liệu ổn định;")
add_p(0.4, "b) ", "Chủ trì kiểm soát, thiết kế cấu hình và thông số kỹ thuật cho toàn bộ danh mục thiết bị của dự án theo đúng Mục VIII.2.1 Nhiệm vụ thiết kế:")

# Table equipment
equip_data = [
    ("I", "THIẾT BỊ TẠI HIỆN TRƯỜNG TRÊN CÁC TUYẾN ĐIỂM", "", ""),
    ("1", "Thiết bị", "", ""),
    ("1-1", "Camera giám sát an ninh (Bullet)", "Cái", "41"),
    ("1-2", "Camera giám sát an ninh toàn cảnh (Speed Dome)", "Cái", "11"),
    ("1-3", "Camera nhận dạng biển số xe - truy vết", "Cái", "11"),
    ("1-4", "Thiết bị xử lý dữ liệu biên", "Bộ", "9"),
    ("1-5", "Trụ đa giác cao 6m chuyên dụng lắp camera an ninh, loại dùng cho tay vươn 1-4 mét", "Trụ", "9"),
    ("1-6", "Tay vươn dài 1m chuyên dụng lắp camera an ninh (trụ đa giác)", "Cái", "5"),
    ("1-7", "Tay vươn dài 2m chuyên dụng lắp camera an ninh (trụ đa giác)", "Cái", "6"),
    ("1-8", "Tay vươn dài 3m chuyên dụng lắp camera an ninh (trụ đa giác)", "Cái", "4"),
    ("1-9", "Tay vươn dài 4m chuyên dụng lắp camera an ninh (trụ đa giác)", "Cái", "6"),
    ("1-10", "Trụ đa giác cao 6m chuyên dụng lắp camera an ninh, loại dùng cho tay vươn 5~6 mét", "Trụ", "13"),
    ("1-11", "Tay vươn dài 6m chuyên dụng lắp camera an ninh (trụ đa giác)", "Cái", "13"),
    ("1-12", "Trụ đa giác cao 6m chuyên dụng lắp camera an ninh, loại dùng cho tay vươn 9 mét", "Trụ", "6"),
    ("1-13", "Tay vươn dài 9m chuyên dụng lắp camera an ninh (trụ đa giác)", "Cái", "6"),
    ("1-14", "Tủ kỹ thuật ngoài trời chuyên dụng cho camera an ninh (treo cao 2,5m)", "Cái", "28"),
    ("1-15", "Switch công nghiệp 8 cổng Layer 2 + Bộ cấp nguồn", "Bộ", "28"),
    ("1-16", "Dây cáp điện cấp nguồn (tổng)", "Md", "2.800"),
    ("1-17", "Dây cáp điện cấp nguồn (camera)", "Md", "330"),
    ("1-18", "Dây mạng Cat 6 UTP", "Md", "1.080"),
    ("1-19", "Ống ruột gà lõi thép luồn dây mạng, điện", "Md", "840"),
    ("1-20", "Ống PVC D25 luồn dây thoát sét", "Md", "168"),
    ("II", "TRUNG TÂM GIÁM SÁT AN NINH", "", ""),
    ("2", "Thiết bị", "", ""),
    ("2-1", "Tủ rack trung tâm 42U - D1000", "Cái", "1"),
    ("2-2", "Tủ cắt lọc sét 100kA cho nguồn điện 3P/AC", "Bộ", "1"),
    ("2-3", "Chống sét đường Ethernet tốc độ 10Gb/RJ45", "Bộ", "1"),
    ("2-4", "Cáp đồng bọc 10mm² (vàng xanh)", "Md", "100"),
    ("2-5", "Bộ cọc tiếp địa thép mạ đồng + phụ kiện lắp đặt (kẹp cáp, bulong...) + giếng khoan sâu 25m", "Bộ", "1"),
    ("2-6", "Dây đồng trần thoát sét (50mm²)", "Md", "100"),
    ("2-7", "Cầu dao điện MCCB loại khối", "Cái", "1"),
    ("2-8", "Cáp điện cấp nguồn cho tủ rack trung tâm", "Md", "240"),
    ("2-9", "Patch Panel 24 cổng Cat 6", "Bộ", "1"),
    ("2-10", "Dây nhảy mạng (Patch cord) 2M", "Sợi", "24"),
    ("2-11", "UPS 10kVA Online", "Bộ", "1"),
    ("2-12", "Máy chủ VMS và metadata", "Bộ", "1"),
    ("2-14", "Thiết bị tường lửa - Firewall", "Bộ", "2"),
    ("2-15", "Bộ định tuyến - Router (Multi-WAN)", "Bộ", "2"),
    ("2-16", "Thiết bị chuyển mạch trung tâm (Core switch)", "Bộ", "2"),
    ("2-17", "Màn hình ghép chuyên dụng 55\" FHD", "Tấm", "6"),
    ("2-18", "Thiết bị trình chiếu nội dung + phụ kiện kết nối tấm màn hình ghép", "Bộ", "1"),
    ("2-19", "Khung lắp tường cho màn hình ghép chuyên dụng + khung gia cố", "Bộ", "6"),
    ("2-20", "Máy trạm (workstation) chuyên dụng hiển thị video hệ thống", "Bộ", "2"),
    ("2-21", "Máy trạm (workstation) chuyên dụng quản lý hồ sơ + màn hình 27\"", "Bộ", "2"),
    ("2-22", "Máy in A4 (in 2 mặt tự động)", "Bộ", "2"),
    ("2-23", "Bàn điều khiển camera PTZ đa chức năng", "Bộ", "2"),
    ("2-24", "UPS 2kVA Online", "Bộ", "2"),
    ("2-25", "Chi phí điện điểm camera (hạ đồng hồ) - Chủ đầu tư đăng ký cấp điện lần đầu", "Điểm", "28"),
    ("", "Hệ thống PCCC và nội thất trung tâm điều hành", "", ""),
    ("2-27", "Hệ thống báo cháy cho phòng trung tâm", "HT", "1"),
    ("2-28", "Máy lạnh phòng điều hành", "HT", "1"),
    ("2-29", "Nội thất trung tâm điều hành", "Gói", "1"),
    ("III", "PHẦN MỀM", "", ""),
    ("3-1", "Giấy phép kích hoạt VMS", "Lic", "63"),
    ("3-2", "Giấy phép kích hoạt phần mềm quản lý siêu dữ liệu (metadata)", "Lic", "11"),
    ("3-3", "Phần mềm nghiệp vụ giám sát an ninh thông minh (AI)", "Gói", "1"),
    ("3-4", "Phần mềm bản đồ số mở rộng", "Gói", "1"),
    ("3-5", "Phần mềm quản lý danh sách đen phương tiện", "Gói", "1")
]

table_eq = doc.add_table(rows=1, cols=4)
table_eq.alignment = WD_TABLE_ALIGNMENT.CENTER
table_eq.autofit = False
set_table_borders(table_eq)

hdr_cells = table_eq.rows[0].cells
hdr_cells[0].width = Inches(0.8)
hdr_cells[1].width = Inches(3.8)
hdr_cells[2].width = Inches(0.8)
hdr_cells[3].width = Inches(1.1)

headers = ["STT", "Nội dung thiết bị", "ĐVT", "Số lượng"]
for i, h in enumerate(headers):
    set_cell_background(hdr_cells[i], "EFEFEF")
    set_cell_margins(hdr_cells[i], top=120, bottom=120, left=150, right=150)
    p = hdr_cells[i].paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run(h)
    r.font.bold = True
    r.font.size = Pt(11)

for item in equip_data:
    row_cells = table_eq.add_row().cells
    row_cells[0].width = Inches(0.8)
    row_cells[1].width = Inches(3.8)
    row_cells[2].width = Inches(0.8)
    row_cells[3].width = Inches(1.1)
    
    for c in row_cells:
        set_cell_margins(c, top=80, bottom=80, left=120, right=120)
    
    stt, name, dvt, qty = item
    
    if dvt == "" and qty == "":
        set_cell_background(row_cells[0], "F5F5F5")
        set_cell_background(row_cells[1], "F5F5F5")
        set_cell_background(row_cells[2], "F5F5F5")
        set_cell_background(row_cells[3], "F5F5F5")
        
        p0 = row_cells[0].paragraphs[0]
        p0.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p0.add_run(stt)
        r.font.bold = True
        r.font.size = Pt(11)
        
        p1 = row_cells[1].paragraphs[0]
        r = p1.add_run(name)
        r.font.bold = True
        r.font.size = Pt(11)
    else:
        p0 = row_cells[0].paragraphs[0]
        p0.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p0.add_run(stt)
        r.font.size = Pt(10.5)
        
        p1 = row_cells[1].paragraphs[0]
        r = p1.add_run(name)
        r.font.size = Pt(10.5)
        
        p2 = row_cells[2].paragraphs[0]
        p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p2.add_run(dvt)
        r.font.size = Pt(10.5)
        
        p3 = row_cells[3].paragraphs[0]
        p3.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p3.add_run(qty)
        r.font.size = Pt(10.5)

p_sp = doc.add_paragraph()
p_sp.paragraph_format.space_after = Pt(4)

add_p(0.4, "c) ", "Kiểm soát các tiêu chuẩn kỹ thuật thiết bị ngoài trời: Cấp bảo vệ chống bụi, chống nước IP66/IP67 (IEC 60529), chống va đập IK10 (IEC 62262), chuẩn mở ONVIF Profile S, T, G và chuẩn nén video H.265;")
add_p(0.4, "d) ", "Khảo sát và thiết kế phòng Trung tâm giám sát an ninh tại trụ sở Công an xã Bắc Tân Uyên: Tủ rack 42U-D1000, Máy chủ VMS và metadata, 02 Firewall, 02 Router Multi-WAN, 02 Core switch, hệ thống lưu điện UPS 10kVA và UPS 2kVA; băng thông mạng trung tâm đạt tối thiểu 600 Mbps;")
add_p(0.4, "đ) ", "Thiết kế hệ thống hiển thị màn hình ghép 55\" FHD (06 tấm) kèm khung gia cố treo tường, thiết bị trình chiếu, 02 máy trạm hiển thị video, 02 máy trạm quản lý hồ sơ và bàn điều khiển PTZ;")
add_p(0.4, "e) ", "Thiết kế giải pháp phần mềm nghiệp vụ thông minh: 63 license VMS, 11 license metadata, phần mềm AI nhận diện biển số xe (ANPR), bản đồ số GIS mở rộng và phần mềm quản lý danh sách đen xe (Blacklist);")
add_p(0.4, "g) ", "Thiết kế kết nối liên thông camera chung cư về Công an xã và kết nối về Trung tâm thông tin chỉ huy CATP theo Kế hoạch 305/KH-CATP; đáp ứng Mô hình chuyển đổi số cấp xã mã HTS11 (CV 2763/BKHCN-CĐSQG);")
add_p(0.4, "h) ", "Xây dựng quy trình vận hành kỹ thuật, quy trình kiểm tra thử nghiệm hệ thống và phương án bảo trì, bảo dưỡng thiết bị (tuổi thọ thiết bị 5-7 năm; trụ thép ≥ 10 năm);")
add_p(0.4, "i) ", "Chịu trách nhiệm bảo hành kỹ thuật, hỗ trợ xử lý sự cố thiết bị mạng, máy chủ và phần mềm sau khi bàn giao đưa vào sử dụng;")
add_p(0.4, "k) ", "Thực hiện các nhiệm vụ công nghệ, mạng và thiết bị khác do Trưởng phòng Dự án phân công.")

# 3. Kế toán – Tài chính dự án
add_p(0, "3. Kế toán – Tài chính dự án (thanh quyết toán, hóa đơn, chứng từ):", "", space_before=4, space_after=2)
add_p(0.2, "3.1. [Ông/Bà…………], Kế toán trưởng / Kế toán dự án:", "", space_after=2)
add_p(0.4, "a) ", "Tham mưu cho Giám đốc Công ty và Trưởng phòng Dự án trong công tác quản lý tài chính, ngân sách thực hiện dự án Đầu tư hệ thống Camera an ninh xã Bắc Tân Uyên;")
add_p(0.4, "b) ", "Chủ trì tổ chức thu thập báo giá (Giai đoạn 3: 03 ngày): Gửi thư mời và thu thập tối thiểu 03 bản báo giá hợp lệ từ 03 nhà cung cấp độc lập cho từng hạng mục thiết bị chính và cước viễn thông; lập bảng phân tích, so sánh giá thiết bị;")
add_p(0.4, "c) ", "Chủ trì phối hợp với bộ phận kỹ thuật bóc tách khối lượng, áp định mức đơn giá xây dựng, bưu chính viễn thông, CNTT hiện hành để lập dự toán chi tiết công trình;")
add_p(0.4, "d) ", "Kiểm soát chặt chẽ tổng dự toán công trình (chi phí xây dựng, thiết bị, quản lý dự án, tư vấn, dự phòng) bảo đảm tuyệt đối không vượt tổng mức đầu tư 15.000.000.000 đồng theo Quyết định số 766/QĐ-UBND;")
add_p(0.4, "đ) ", "Lập dự toán kinh phí tạm ứng, quản lý công tác phí, chi phí khảo sát hiện trường, chi phí thuê thiết bị đo đạc phục vụ đoàn khảo sát tại xã Bắc Tân Uyên;")
add_p(0.4, "e) ", "Quản lý hợp đồng kinh tế và tiến độ thanh toán cho các đơn vị cung cấp thiết bị, nhà thầu thi công hạ tầng và nhà mạng viễn thông;")
add_p(0.4, "g) ", "Lập hồ sơ thanh toán, hồ sơ đề nghị giải ngân, tạm ứng và thanh quyết toán vốn đầu tư với Ban Quản lý dự án Đầu tư xây dựng xã Bắc Tân Uyên theo tiến độ hợp đồng;")
add_p(0.4, "h) ", "Quản lý hóa đơn chứng từ, sổ sách kế toán, xuất hóa đơn VAT và thực hiện giao dịch tài chính đúng quy định pháp luật;")
add_p(0.4, "i) ", "Thực hiện chế độ báo cáo tài chính dự án định kỳ hàng tháng, quý gửi Ban Giám đốc Công ty;")
add_p(0.4, "k) ", "Phối hợp với Chủ đầu tư hoàn thiện hồ sơ thẩm tra, phê duyệt quyết toán vốn đầu tư công hoàn thành theo Nghị định số 254/2025/NĐ-CP;")
add_p(0.4, "l) ", "Thực hiện các nhiệm vụ tài chính, kế toán khác theo sự phân công của Giám đốc Công ty.")

# 4. Pháp chế – Hợp đồng
add_p(0, "4. Pháp chế – Hợp đồng (hợp đồng, pháp lý, hồ sơ nghiệm thu):", "", space_before=4, space_after=2)
add_p(0.2, "4.1. [Ông/Bà…………], Chuyên viên Pháp chế – Hợp đồng dự án:", "", space_after=2)
add_p(0.4, "a) ", "Rà soát, thẩm định tính pháp lý của Hợp đồng tư vấn khảo sát, lập Báo cáo kinh tế - kỹ thuật ký giữa Công ty HINOVA và Ban Quản lý dự án Đầu tư xây dựng xã Bắc Tân Uyên;")
add_p(0.4, "b) ", "Soạn thảo, đàm phán và quản lý các hợp đồng kinh tế ký kết với các đối tác cung cấp thiết bị camera, máy chủ, màn hình ghép, nhà thầu gia công cơ khí mạ kẽm và các nhà mạng viễn thông;")
add_p(0.4, "c) ", "Kiểm tra, bảo đảm hồ sơ thiết kế, dự toán tuân thủ đúng các quy định pháp luật hiện hành: Luật Đầu tư công 58/2024, Luật Xây dựng, Luật Chuyển đổi số 148/2025, Nghị định 85/2025/NĐ-CP, Nghị định 214/2025/NĐ-CP về đấu thầu;")
add_p(0.4, "d) ", "Tham mưu các thủ tục pháp lý liên quan đến thỏa thuận vị trí chôn trụ, treo tủ kỹ thuật trên hành lang an toàn giao thông đường bộ và thỏa thuận cấp điện với Điện lực;")
add_p(0.4, "đ) ", "Chủ trì xây dựng và kiểm tra tính pháp lý của toàn bộ hồ sơ nghiệm thu: Biên bản nghiệm thu khảo sát thực địa, biên bản nghiệm thu giai đoạn, biên bản nghiệm thu hồ sơ Báo cáo kinh tế - kỹ thuật;")
add_p(0.4, "e) ", "Tư vấn, hỗ trợ xử lý các tranh chấp, khiếu nại hoặc vướng mắc pháp lý phát sinh trong quá trình triển khai dự án;")
add_p(0.4, "g) ", "Thẩm định tính pháp lý của các văn bản, công văn, tờ trình của dự án trước khi trình Trưởng phòng Dự án hoặc Giám đốc Công ty ký ban hành;")
add_p(0.4, "h) ", "Lưu trữ, quản lý hồ sơ pháp lý dự án và phối hợp giải trình với các cơ quan thanh tra, kiểm tra, kiểm toán nhà nước khi có yêu cầu;")
add_p(0.4, "i) ", "Thực hiện các nhiệm vụ pháp lý, hợp đồng khác do Trưởng phòng Dự án và Giám đốc Công ty giao.")

# 5. Đào tạo, chuyển giao và hỗ trợ
add_p(0, "5. Đào tạo, chuyển giao và hỗ trợ:", "", space_before=4, space_after=2)

# 5.1 Hướng dẫn vận hành cho cán bộ xã
add_p(0.2, "5.1. [Ông/Bà…………], Chuyên viên Phụ trách Hướng dẫn vận hành cho cán bộ xã:", "", space_after=2)
add_p(0.4, "a) ", "Xây dựng bộ tài liệu hướng dẫn sử dụng, quy trình vận hành hệ thống camera giám sát an ninh, phần mềm VMS và các tính năng AI thông minh;")
add_p(0.4, "b) ", "Biên soạn cẩm nang tra cứu nhanh, quy trình trích xuất video, hình ảnh và tra cứu biển số xe vi phạm phục vụ công tác nghiệp vụ của Công an xã Bắc Tân Uyên;")
add_p(0.4, "c) ", "Trực tiếp tổ chức các lớp tập huấn, đào tạo chuyển giao công nghệ cho cán bộ, chiến sĩ Công an xã Bắc Tân Uyên và cán bộ phụ trách CNTT của UBND xã;")
add_p(0.4, "d) ", "Hướng dẫn cán bộ xã cách thức khai thác hệ thống bản đồ số GIS, quản lý danh sách đen phương tiện (Blacklist) và phối hợp xử lý tin báo an ninh trật tự;")
add_p(0.4, "đ) ", "Đánh giá kết quả tiếp thu của học viên sau đào tạo và lập biên bản bàn giao tài liệu, biên bản xác nhận hoàn thành công tác đào tạo chuyển giao;")
add_p(0.4, "e) ", "Thực hiện các nhiệm vụ đào tạo, chuyển giao khác theo phân công của Trưởng phòng Dự án.")

# 5.2 Hỗ trợ và bảo hành sau triển khai
add_p(0.2, "5.2. [Ông/Bà…………], Chuyên viên Phụ trách Hỗ trợ và bảo hành sau triển khai:", "", space_before=2, space_after=2)
add_p(0.4, "a) ", "Xây dựng kế hoạch bảo hành, bảo trì định kỳ cho toàn bộ 20 tuyến điểm camera và phòng Trung tâm giám sát điều hành;")
add_p(0.4, "b) ", "Thiết lập kênh tiếp nhận thông tin yêu cầu hỗ trợ kỹ thuật (hotline 24/7) dành riêng cho Công an xã Bắc Tân Uyên để xử lý sự cố khẩn cấp;")
add_p(0.4, "c) ", "Trực tiếp phối hợp với bộ phận kỹ thuật kiểm tra định kỳ tình trạng hoạt động của camera, ống kính, trụ thép, tủ kỹ thuật, nguồn điện và đường truyền mạng;")
add_p(0.4, "d) ", "Định kỳ sao lưu dữ liệu cấu hình hệ thống máy chủ, cập nhật các bản vá bảo mật cho hệ điều hành, phần mềm VMS và thuật toán AI;")
add_p(0.4, "đ) ", "Lập sổ theo dõi lịch sử bảo hành, bảo trì, ghi chép chi tiết nguyên nhân sự cố và biện pháp khắc phục; báo cáo định kỳ cho Trưởng phòng Dự án;")
add_p(0.4, "e) ", "Thực hiện các nhiệm vụ hỗ trợ kỹ thuật và chăm sóc khách hàng khác theo yêu cầu.")

# 5.3 Nhân viên hợp đồng, hậu cần
add_p(0.2, "5.3. [Ông/Bà…………], Nhân viên hợp đồng, hậu cần dự án:", "", space_before=2, space_after=2)
add_p(0.4, "a) ", "Phụ trách công tác hậu cần, chuẩn bị văn phòng làm việc, sắp xếp tài liệu, phục vụ nước uống, vệ sinh trong các buổi họp, hội thảo của Ban điều hành dự án;")
add_p(0.4, "b) ", "Phục vụ công tác hậu cần, ăn uống, phương tiện đi lại cho đoàn khảo sát và kỹ sư trong những ngày làm việc thực địa tại địa bàn xã Bắc Tân Uyên;")
add_p(0.4, "c) ", "Hỗ trợ công tác in ấn, đóng cuốn hồ sơ Báo cáo kết quả khảo sát, hồ sơ bản vẽ thiết kế thi công và dự toán công trình;")
add_p(0.4, "d) ", "Thực hiện các công việc tạp vụ, vận chuyển thiết bị đo đạc, hỗ trợ văn phòng khác theo sự điều động của Văn phòng dự án.")

# II. Trách nhiệm phạm vi giải quyết công việc của nhân viên hợp đồng, cộng tác viên và các tổ thi công
add_p(0, "II. Trách nhiệm phạm vi giải quyết công việc của nhân viên hợp đồng, cộng tác viên và các tổ thi công", "", space_before=5, space_after=3)

# 1. Nhân viên hợp đồng, cộng tác viên
add_p(0, "1. Nhân viên hợp đồng, cộng tác viên:", "", space_after=2)
add_p(0.2, "a) ", "Chịu trách nhiệm trước Giám đốc Công ty và Trưởng phòng Dự án về nhiệm vụ chuyên môn được phân công; tuân thủ đúng nội quy lao động, quy trình kỹ thuật và cam kết bảo mật thông tin dự án;")
add_p(0.2, "b) ", "Thực hiện nghiêm túc sự điều động của các Trưởng bộ phận trong quá trình tham gia khảo sát hiện trường, bóc tách khối lượng và hỗ trợ dự án;")
add_p(0.2, "c) ", "Bảo quản, giữ gìn các trang thiết bị kỹ thuật, máy móc, phương tiện làm việc được Công ty giao;")
add_p(0.2, "d) ", "Kịp thời báo cáo người phụ trách trực tiếp khi có khó khăn, sự cố phát sinh tại hiện trường.")

# 2. Các tổ thi công
add_p(0, "2. Các tổ thi công (Tổ khảo sát địa hình, Tổ đo đạc điện lực, Tổ thi công lắp đặt):", "", space_before=3, space_after=2)
add_p(0.2, "a) ", "Chịu trách nhiệm trước Trưởng phòng Dự án và Phụ trách bộ phận kỹ thuật về tiến độ, chất lượng và an toàn lao động trong suốt quá trình thực hiện nhiệm vụ ngoài hiện trường;")
add_p(0.2, "b) ", "Tổ chức khảo sát, thi công theo đúng phương án kỹ thuật và biện pháp an toàn đã được phê duyệt; bảo đảm an toàn giao thông, an toàn lao động và bảo vệ môi trường tại 20 tuyến điểm;")
add_p(0.2, "c) ", "Thường xuyên báo cáo tình hình công việc hàng ngày, lập sổ nhật ký khảo sát và thi công đầy đủ theo quy định;")
add_p(0.2, "d) ", "Kịp thời phản ánh những vướng mắc phát sinh tại thực địa với Trưởng phòng Dự án để phối hợp với chính quyền xã và Công an xã xử lý kịp thời.")

# Article 2
p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(6)
p.paragraph_format.space_after = Pt(3)
r = p.add_run("Điều 2. ")
r.font.bold = True
p.add_run("Các thành viên thực hiện dự án và các cá nhân liên quan chịu trách nhiệm cá nhân về công việc được phân công trước Giám đốc Công ty và Trưởng phòng Dự án theo đúng quy định của pháp luật và quy chế của Công ty Cổ phần Công nghệ HINOVA. Khi cần thay đổi nhân sự hoặc điều chỉnh phân công, Trưởng phòng Dự án đề xuất để Giám đốc Công ty xem xét, quyết định bằng văn bản.")

# Article 3
p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(4)
p.paragraph_format.space_after = Pt(3)
r = p.add_run("Điều 3. ")
r.font.bold = True
p.add_run("Quyết định này có hiệu lực kể từ ngày ký. Thời hạn thực hiện: ")
r_t = p.add_run("45 ngày")
r_t.font.bold = True
p.add_run(" kể từ ngày ký hợp đồng tư vấn, theo tiến độ quy định tại Điều 1, mục I.1.1.1). Phòng Dự án, Phòng Kỹ thuật, Phòng Kế toán, các bộ phận liên quan và các thành viên có tên tại Điều 1 chịu trách nhiệm thi hành Quyết định này./.")

# Signature Table
sig_table = doc.add_table(rows=1, cols=2)
sig_table.alignment = WD_TABLE_ALIGNMENT.CENTER
sig_table.autofit = False

col_widths = [Inches(3.2), Inches(3.3)]
row = sig_table.rows[0]

# Left: Noinhan
c_left = row.cells[0]
c_left.width = col_widths[0]
p_nn = c_left.paragraphs[0]
p_nn.paragraph_format.space_after = Pt(2)
r = p_nn.add_run("Nơi nhận:")
r.font.bold = True
r.font.italic = True
r.font.size = Pt(12)

nn_items = [
    "- Như Điều 3;",
    "- Ban Giám đốc Công ty (để chỉ đạo);",
    "- Ban Quản lý dự án Đầu tư xây dựng xã Bắc Tân Uyên (để báo cáo);",
    "- Công an xã Bắc Tân Uyên (để phối hợp);",
    "- Lưu: VT, Phòng Dự án."
]
for item in nn_items:
    p = c_left.add_paragraph()
    p.paragraph_format.space_after = Pt(1)
    r = p.add_run(item)
    r.font.size = Pt(11)

# Right: Director
c_right = row.cells[1]
c_right.width = col_widths[1]
p_dir = c_right.paragraphs[0]
p_dir.alignment = WD_ALIGN_PARAGRAPH.CENTER
p_dir.paragraph_format.space_after = Pt(2)
r = p_dir.add_run("TM. CÔNG TY CỔ PHẦN CÔNG NGHỆ HINOVA\nGIÁM ĐỐC")
r.font.bold = True
r.font.size = Pt(12.5)

p_sign_note = c_right.add_paragraph()
p_sign_note.alignment = WD_ALIGN_PARAGRAPH.CENTER
p_sign_note.paragraph_format.space_after = Pt(45)
r = p_sign_note.add_run("(Ký, ghi rõ họ tên và đóng dấu)")
r.font.italic = True
r.font.size = Pt(11)

p_name = c_right.add_paragraph()
p_name.alignment = WD_ALIGN_PARAGRAPH.CENTER
p_name.paragraph_format.space_after = Pt(0)
r = p_name.add_run("[Họ và tên Giám đốc]")
r.font.bold = True
r.font.size = Pt(12.5)

# Save
scratch_path = r"C:\Users\GIGABYTE\.gemini\antigravity\brain\6006b014-e173-4214-bac6-9bf3cae42ae9\scratch\QD_phan_cong_nhiem_vu_Camera_BTU.docx"
doc.save(scratch_path)
print(f"Saved to scratch: {scratch_path}")
