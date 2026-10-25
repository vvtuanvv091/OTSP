(() => {
    const root = document.getElementById('searchRoot');
    const form = document.getElementById('searchForm');
    const f = form.elements;
    const grid = document.getElementById('grid');
    const pager = document.getElementById('pager');
    const resultCount = document.getElementById('resultCount');
    const loadingState = document.getElementById('loadingState');
    const emptyState = document.getElementById('emptyState');
    const errorState = document.getElementById('errorState');
    const errorText = document.getElementById('errorText');
    const clearBtn = document.getElementById('clearBtn');
    const formatBox = document.getElementById('formatBox');
    const classTypeBox = document.getElementById('classTypeBox');
    const tabButtons = document.querySelectorAll('[data-tab]');

    const PAGE_SIZE = 12;
    const CACHE_TTL_MS = 5 * 60 * 1000;
    const money = new Intl.NumberFormat('vi-VN');

    // Thứ tự phải khớp enum TeachingFormat: Online = 0, Offline = 1, Hybrid = 2
    const FORMAT_NAMES = ['Online', 'Offline', 'Hybrid'];
    const FORMAT_LABEL = { Online: 'Trực tuyến', Offline: 'Trực tiếp', Hybrid: 'Trực tuyến & trực tiếp' };

    // Thứ tự phải khớp enum ClassroomType: OneToOne = 0, Group = 1
    const TYPE_NAMES = ['OneToOne', 'Group'];
    const TYPE_LABEL = { OneToOne: '1 kèm 1', Group: 'Nhóm' };

    let tab = root.dataset.tab === 'classes' ? 'classes' : 'tutors';
    let controller = null;
    let debounceTimer = null;
    const cache = new Map();   // url -> { at, data }

    const esc = s => String(s ?? '').replace(/[&<>"']/g,
        c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // Enum có thể về dạng chuỗi (JsonStringEnumConverter) hoặc số
    const enumName = (value, names) => typeof value === 'number' ? names[value] : value;

    function initials(name) {
        return String(name ?? '').trim().split(/\s+/).slice(-2).map(w => w[0] ?? '').join('').toUpperCase();
    }

    function buildParams(page) {
        const p = new URLSearchParams();
        const put = (k, v) => { const t = String(v ?? '').trim(); if (t) p.set(k, t); };

        put('keyword', f.keyword.value);
        put('subjectId', f.subjectId.value);
        put('gradeLevelId', f.gradeLevelId.value);
        put('maxFee', f.maxFee.value);

        if (tab === 'tutors') put('format', f.format.value);
        if (tab === 'classes') put('classType', f.classType.value);

        p.set('page', page);
        p.set('pageSize', PAGE_SIZE);
        return p;
    }

    function syncUrl(params) {
        history.replaceState(null, '', `${location.pathname}?tab=${tab}&${params}`);
    }

    async function load(page = 1) {
        const params = buildParams(page);
        const endpoint = tab === 'classes'
            ? (root?.dataset?.classesUrl || '/Search/Classes')
            : (root?.dataset?.tutorsUrl || '/Search/Tutors');
        const sep = endpoint.includes('?') ? '&' : '?';
        const url = `${endpoint}${sep}${params}`;
        syncUrl(params);

        const hit = cache.get(url);
        if (hit && Date.now() - hit.at < CACHE_TTL_MS) {
            render(hit.data);
            return;
        }

        controller?.abort();
        controller = new AbortController();
        showLoading();

        try {
            const res = await fetch(url, {
                headers: { Accept: 'application/json' },
                signal: controller.signal
            });

            if (!res.ok) {
                const body = await res.json().catch(() => ({}));
                throw new Error(body.message || `Lỗi máy chủ (${res.status})`);
            }

            const data = await res.json();
            cache.set(url, { at: Date.now(), data });
            render(data);
        } catch (err) {
            if (err.name !== 'AbortError') showError(err.message);
        }
    }

    function showLoading() {
        loadingState.classList.remove('d-none');
        errorState.classList.add('d-none');
        emptyState.classList.add('d-none');
        grid.replaceChildren();
        pager.replaceChildren();
        resultCount.textContent = 'Đang tìm...';
    }

    function showError(message) {
        loadingState.classList.add('d-none');
        errorState.classList.remove('d-none');
        errorText.textContent = message || 'Không tải được dữ liệu, vui lòng thử lại.';
        grid.replaceChildren();
        pager.replaceChildren();
        resultCount.textContent = '';
    }

    function render(data) {
        loadingState.classList.add('d-none');
        errorState.classList.add('d-none');
        resultCount.textContent = `Tìm thấy ${data.total} ${tab === 'classes' ? 'lớp học' : 'gia sư'}`;

        if (data.total === 0) {
            grid.innerHTML = '';
            pager.innerHTML = '';
            emptyState.classList.remove('d-none');
            return;
        }

        emptyState.classList.add('d-none');
        grid.innerHTML = data.items
            .map(tab === 'classes' ? classCard : tutorCard)
            .join('');
        renderPager(data);
    }

    function tutorCard(t) {
        const first = t.teaching?.[0];
        const subject = first ? `${first.subjectName} · ${first.gradeName}` : 'Chưa có môn dạy';
        const rating = t.averageRating > 0
            ? `<span class="d-inline-flex align-items-center"><i class="bi bi-star-fill text-warning me-1" style="color: #f59e0b !important;"></i>${t.averageRating.toFixed(1)}</span>`
            : 'Chưa có đánh giá';
        const format = FORMAT_LABEL[enumName(t.teachingFormat, FORMAT_NAMES)] ?? '';
        const avatar = t.avatarUrl
            ? `<img src="${esc(t.avatarUrl)}" class="otsp-avatar" width="56" height="56" alt="${esc(t.fullName)}" loading="lazy">`
            : `<div class="otsp-avatar" aria-hidden="true">${esc(t.initials)}</div>`;
        const qualification = t.verifiedQualificationCount > 0
            ? `<span class="otsp-tag otsp-tag-qual"><i class="bi bi-patch-check-fill me-1"></i>${t.verifiedQualificationCount} bằng cấp</span>`
            : '';

        return `
        <div class="col-12 col-md-6 col-lg-4 d-flex">
            <article class="card otsp-tutor w-100">
                <div class="card-body d-flex flex-column gap-2">
                    <div class="d-flex align-items-center gap-3">
                        ${avatar}
                        <div class="min-w-0">
                            <h2 class="h6 mb-1 text-truncate">${esc(t.fullName)}</h2>
                            <span class="small text-body-secondary">${esc(subject)}</span>
                        </div>
                    </div>
                    <p class="otsp-clamp mb-0">${esc(t.introduction || 'Gia sư chưa viết giới thiệu.')}</p>
                    <div class="d-flex flex-wrap gap-1">
                        ${format ? `<span class="otsp-tag">${esc(format)}</span>` : ''}
                        ${qualification}
                    </div>
                    <div class="d-flex justify-content-between align-items-center mt-auto pt-2">
                        <span class="otsp-price">${money.format(t.hourlyFee)}đ/giờ</span>
                        <span class="small">${rating}</span>
                    </div>
                    <a href="/Search/Details/${t.id}" class="btn btn-sm btn-outline-otsp w-100">Xem hồ sơ</a>
                </div>
            </article>
        </div>`;
    }

    function classCard(c) {
        const typeName = enumName(c.type, TYPE_NAMES);
        const isGroup = typeName === 'Group';
        const typeText = TYPE_LABEL[typeName] ?? '';
        const seats = c.maxStudents - c.studentCount;
        const seatText = seats > 0 ? `Còn ${seats} chỗ` : 'Đã đủ học viên';

        const cover = c.coverImageUrl
            ? `<img src="${esc(c.coverImageUrl)}" alt="${esc(c.name)}" loading="lazy">`
            : `<div class="otsp-class-cover-fallback" aria-hidden="true"><i class="bi bi-book"></i></div>`;

        return `
        <div class="col-12 col-md-6 col-lg-4 d-flex">
            <article class="otsp-class-card w-100 d-flex flex-column">
                <div class="otsp-class-cover">
                    ${cover}
                    <span class="otsp-tag">${esc(c.subjectName)} · ${esc(c.gradeName)}</span>
                </div>

                <div class="otsp-class-body d-flex flex-column gap-2 flex-grow-1">
                    <div class="d-flex justify-content-end">
                        <span class="otsp-price">${money.format(c.fee)}đ</span>
                    </div>

                    <h2 class="otsp-class-title">${esc(c.name)}</h2>
                    <p class="otsp-clamp mb-0">${esc(c.description || '')}</p>

                    <div class="d-flex flex-wrap gap-2 align-items-center">
                        <span class="otsp-tag">
                            <i class="bi ${isGroup ? 'bi-people-fill' : 'bi-person-fill'}"></i> ${esc(typeText)}
                        </span>
                        <span class="otsp-seat">
                            <i class="bi bi-person"></i> ${c.studentCount}/${c.maxStudents} · ${esc(seatText)}
                        </span>
                    </div>

                    <div class="d-flex justify-content-between align-items-center mt-auto pt-3 border-top">
                        <div class="d-flex align-items-center gap-2 min-w-0">
                            <div class="otsp-avatar" style="width:36px;height:36px;font-size:.8rem;" aria-hidden="true">${esc(initials(c.tutorName))}</div>
                            <span class="fw-semibold text-truncate">${esc(c.tutorName)}</span>
                        </div>
                    </div>

                    <a href="/Search/Details/${c.tutorId}" class="btn btn-sm btn-outline-otsp w-100">Xem gia sư</a>
                </div>
            </article>
        </div>`;
    }

    function renderPager(data) {
        const pages = data.totalPages;
        if (pages <= 1) { pager.innerHTML = ''; return; }

        pager.innerHTML = `
            <button type="button" class="btn btn-sm btn-outline-otsp" data-page="${data.page - 1}"
                    ${data.page <= 1 ? 'disabled' : ''}>‹ Trước</button>
            <span class="small text-body-secondary">Trang ${data.page}/${pages}</span>
            <button type="button" class="btn btn-sm btn-outline-otsp" data-page="${data.page + 1}"
                    ${data.page >= pages ? 'disabled' : ''}>Sau ›</button>`;
    }

    function setTabUi() {
        tabButtons.forEach(b => b.classList.toggle('active', b.dataset.tab === tab));
        formatBox.classList.toggle('d-none', tab === 'classes');
        classTypeBox.classList.toggle('d-none', tab !== 'classes');
    }

    // ----- Sự kiện -----

    form.addEventListener('submit', e => {
        e.preventDefault();
        load(1);
    });

    f.keyword.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => load(1), 400);
    });

    ['subjectId', 'gradeLevelId', 'format', 'classType', 'maxFee'].forEach(name =>
        f[name].addEventListener('change', () => load(1)));

    clearBtn.addEventListener('click', () => {
        f.keyword.value = '';
        f.subjectId.value = '';
        f.gradeLevelId.value = '';
        f.format.value = '';
        f.classType.value = '';
        f.maxFee.value = '';
        load(1);
    });

    tabButtons.forEach(btn => btn.addEventListener('click', () => {
        tab = btn.dataset.tab;
        setTabUi();
        load(1);
    }));

    pager.addEventListener('click', e => {
        const btn = e.target.closest('[data-page]');
        if (!btn || btn.disabled) return;
        load(Number(btn.dataset.page));
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    // ----- Khởi tạo -----

    setTabUi();
    const initialPage = Number(new URLSearchParams(location.search).get('page')) || 1;
    load(initialPage);
})();