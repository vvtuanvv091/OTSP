/**
 * OTSP Common Client Utilities
 * Shared helper for AJAX requests, notifications, and DOM utilities.
 */

window.OTSP = window.OTSP || (function () {
    const getVerificationToken = () => {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    };

    const getToastBox = () => {
        let box = document.getElementById('ajaxAlert');
        if (!box) {
            box = document.createElement('div');
            box.id = 'ajaxAlert';
            box.className = 'position-fixed top-0 end-0 p-3';
            box.style.cssText = 'z-index: 1090; max-width: 380px;';
            document.body.appendChild(box);
        }
        return box;
    };

    const escapeHtml = (str) => {
        return String(str ?? '').replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    };

    const notify = (type, message, timeout = 4000) => {
        const box = getToastBox();
        const alertEl = document.createElement('div');
        alertEl.className = `alert alert-${type} alert-dismissible fade show shadow-sm border`;
        alertEl.setAttribute('role', 'alert');
        alertEl.innerHTML = `
            <div class="d-flex align-items-center gap-2">
                <i class="bi ${type === 'success' ? 'bi-check-circle-fill' : type === 'danger' ? 'bi-exclamation-triangle-fill' : 'bi-info-circle-fill'}"></i>
                <div class="flex-grow-1">${escapeHtml(message)}</div>
                <button type="button" class="btn-close ms-auto" data-bs-dismiss="alert" aria-label="Đóng"></button>
            </div>
        `;
        box.prepend(alertEl);
        if (timeout > 0) {
            setTimeout(() => {
                try {
                    const bsAlert = bootstrap.Alert.getOrCreateInstance(alertEl);
                    bsAlert.close();
                } catch {
                    alertEl.remove();
                }
            }, timeout);
        }
    };

    const send = async (url, body, options = {}) => {
        const token = getVerificationToken();
        const headers = Object.assign({
            'RequestVerificationToken': token,
            'Accept': 'application/json'
        }, options.headers || {});

        const res = await fetch(url, {
            method: options.method || 'POST',
            body: body,
            credentials: 'same-origin',
            headers: headers
        });

        const contentType = res.headers.get('content-type') || '';
        const isJson = contentType.includes('application/json');

        if (!isJson) {
            if (res.status === 413) throw new Error('Dung lượng tệp quá lớn.');
            if (res.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng tải lại trang.');
            throw new Error(`Lỗi máy chủ (${res.status}). Vui lòng thử lại.`);
        }

        const data = await res.json();
        if (!res.ok || data.ok === false || data.success === false) {
            throw new Error(data.message || `Lỗi yêu cầu (${res.status})`);
        }

        return data;
    };

    return {
        notify,
        send,
        escapeHtml,
        getVerificationToken
    };
})();
