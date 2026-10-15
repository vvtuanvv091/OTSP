// (() => {
//     const content = document.getElementById('tutorPageContent');
//     if (!content || !window.fetch) return;

//     const allowedControllers = new Set([
//         'TutorProfile',
//         'TutorAvailability',
//         'TutorConnectionRequest',
//         'TutorSubject',
//         'TutorLearningDocument'
//     ].map(controller => controller.toLowerCase()));

//     let activeRequest = null;

//     function isTutorIndexUrl(url) {
//         if (url.origin !== window.location.origin) return false;

//         const segments = url.pathname.split('/').filter(Boolean);
//         const areaIndex = segments.findIndex(segment => segment.toLowerCase() === 'tutor');
//         if (areaIndex < 0) return false;

//         const controller = segments[areaIndex + 1];
//         const action = segments[areaIndex + 2];
//         return allowedControllers.has(controller?.toLowerCase())
//             && (!action || action.toLowerCase() === 'index')
//             && segments.length <= areaIndex + 3;
//     }

//     function isModifiedClick(event) {
//         return event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
//     }

//     function setActiveMenu(url) {
//         const links = Array.from(document.querySelectorAll('.tutor-sidebar-nav a[href]'));
//         links.forEach(link => {
//             link.classList.remove('active');
//             link.removeAttribute('aria-current');
//         });

//         const matchingLinks = links.filter(link => {
//             const linkUrl = new URL(link.href, window.location.href);
//             if (linkUrl.pathname.replace(/\/$/, '') !== url.pathname.replace(/\/$/, '')) return false;

//             if (url.pathname.toLowerCase().includes('/tutorsubject')) {
//                 return (linkUrl.searchParams.get('groupBy') || 'subject')
//                     === (url.searchParams.get('groupBy') || 'subject');
//             }

//             return true;
//         });

//         const activeLink = matchingLinks[0];
//         if (activeLink) {
//             activeLink.classList.add('active');
//             activeLink.setAttribute('aria-current', 'page');
//         }
//     }

//     function runPageScripts() {
//         const scripts = Array.from(content.querySelectorAll('script'));
//         for (const oldScript of scripts) {
//             const script = document.createElement('script');
//             for (const attribute of oldScript.attributes) {
//                 script.setAttribute(attribute.name, attribute.value);
//             }
//             script.textContent = oldScript.textContent;
//             oldScript.replaceWith(script);
//         }
//     }

//     function updatePageTitle() {
//         const heading = content.querySelector('h1');
//         const title = heading?.textContent?.replace(/\s+/g, ' ').trim();
//         if (title) document.title = `${title} - PROJECT_OTSP`;
//     }

//     async function navigate(url, addHistoryEntry = true) {
//         activeRequest?.abort();
//         const request = new AbortController();
//         activeRequest = request;
//         content.setAttribute('aria-busy', 'true');

//         try {
//             const response = await fetch(url.href, {
//                 method: 'GET',
//                 credentials: 'same-origin',
//                 headers: {
//                     'Accept': 'text/html',
//                     'X-Requested-With': 'XMLHttpRequest'
//                 },
//                 signal: request.signal
//             });

//             if (!response.ok || response.headers.get('X-OTSP-Partial') !== 'true') {
//                 window.location.assign(url.href);
//                 return;
//             }

//             const html = await response.text();
//             if (activeRequest !== request) return;

//             content.innerHTML = html;
//             runPageScripts();
//             updatePageTitle();
//             setActiveMenu(url);

//             if (addHistoryEntry) {
//                 window.history.pushState({ tutorAjaxNavigation: true }, '', url.href);
//             }

//             if (document.body.classList.contains('tutor-sidebar-open')) {
//                 document.getElementById('tutorMenuToggle')?.click();
//             }

//             window.scrollTo({ top: 0, behavior: 'auto' });
//             document.dispatchEvent(new CustomEvent('otsp:content-loaded', { detail: { url: url.href } }));
//         } catch (error) {
//             if (error.name !== 'AbortError') window.location.assign(url.href);
//         } finally {
//             if (activeRequest === request) {
//                 activeRequest = null;
//                 content.removeAttribute('aria-busy');
//             }
//         }
//     }

//     document.addEventListener('click', event => {
//         if (isModifiedClick(event)) return;

//         const link = event.target.closest('a[href]');
//         if (!link || link.target || link.hasAttribute('download') || link.closest('.disabled,[aria-disabled="true"]')) return;

//         const rawHref = link.getAttribute('href');
//         if (!rawHref || rawHref.startsWith('#')) return;

//         const url = new URL(link.href, window.location.href);
//         if (!isTutorIndexUrl(url)) return;
//         if (url.pathname === window.location.pathname && url.search === window.location.search) return;

//         event.preventDefault();
//         navigate(url);
//     });

//     window.addEventListener('popstate', () => {
//         const url = new URL(window.location.href);
//         if (isTutorIndexUrl(url)) navigate(url, false);
//     });
// })();

(() => {
    const content = document.getElementById('tutorPageContent');
    if (!content || !window.fetch || !window.history?.pushState) return;

    const mobileQuery = window.matchMedia('(max-width: 991.98px)');
    const sidebarToggle = document.getElementById('tutorMenuToggle');
    const sidebarNav = document.querySelector('.tutor-sidebar-nav');

    const AREA = 'tutor';
    const ALLOWED_CONTROLLERS = new Set([
        'tutorprofile',
        'tutoravailability',
        'tutorconnectionrequest',
        'tutorsubject',
        'tutorlearningdocument'
    ]);

    // Script external đã nạp một lần thì không nạp lại khi chuyển trang
    const loadedScriptSources = new Set(
        Array.from(document.querySelectorAll('script[src]')).map(script => script.src)
    );

    let activeRequest = null;
    let currentKey = window.location.pathname + window.location.search;

    function isNavigableUrl(url) {
        if (url.origin !== window.location.origin) return false;

        const segments = url.pathname.split('/').filter(Boolean).map(s => s.toLowerCase());
        const areaIndex = segments.indexOf(AREA);
        if (areaIndex < 0) return false;

        const controller = segments[areaIndex + 1];
        const action = segments[areaIndex + 2];

        return ALLOWED_CONTROLLERS.has(controller)
            && (action === undefined || action === 'index')
            && segments.length <= areaIndex + 3;
    }

    function isModifiedClick(event) {
        return event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
    }

    function setActiveMenu(url) {
        if (!sidebarNav) return;

        const currentPath = url.pathname.replace(/\/$/, '').toLowerCase();
        const currentGroup = url.searchParams.get('groupBy') || 'subject';
        let found = false;

        sidebarNav.querySelectorAll('a[href]').forEach(link => {
            link.classList.remove('active');
            link.removeAttribute('aria-current');

            if (found) return;

            const linkUrl = new URL(link.href, window.location.href);
            const linkPath = linkUrl.pathname.replace(/\/$/, '').toLowerCase();
            const linkGroup = linkUrl.searchParams.get('groupBy') || 'subject';
            const isSubjectPage = linkPath.includes('/tutorsubject');

            const matches = linkPath === currentPath && (!isSubjectPage || linkGroup === currentGroup);
            if (matches) {
                link.classList.add('active');
                link.setAttribute('aria-current', 'page');
                found = true;
            }
        });
    }

    function runPageScripts() {
        for (const oldScript of Array.from(content.querySelectorAll('script'))) {
            if (oldScript.src) {
                if (loadedScriptSources.has(oldScript.src)) {
                    oldScript.remove();
                    continue;
                }
                loadedScriptSources.add(oldScript.src);
            }

            const script = document.createElement('script');
            for (const attribute of oldScript.attributes) {
                script.setAttribute(attribute.name, attribute.value);
            }
            script.textContent = oldScript.textContent;
            oldScript.replaceWith(script);
        }
    }

    function updatePageTitle() {
        const heading = content.querySelector('h1');
        const title = heading?.textContent?.replace(/\s+/g, ' ').trim();
        if (title) document.title = `${title} - PROJECT_OTSP`;
    }

    async function navigate(url, pushHistory) {
        activeRequest?.abort();
        const request = new AbortController();
        activeRequest = request;
        content.classList.add('is-loading');

        try {
            const response = await fetch(url.href, {
                credentials: 'same-origin',
                headers: {
                    'Accept': 'text/html',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                signal: request.signal
            });

            // Server không trả bản partial thì tải trang đầy đủ như bình thường
            if (!response.ok || response.headers.get('X-OTSP-Partial') !== 'true') {
                window.location.assign(url.href);
                return;
            }

            const html = await response.text();
            if (activeRequest !== request) return;

            content.innerHTML = html;
            runPageScripts();
            updatePageTitle();
            setActiveMenu(url);

            if (pushHistory) {
                window.history.pushState({ tutorAjax: true }, '', url.href);
            }
            currentKey = url.pathname + url.search;

            // Chỉ đóng sidebar trên điện thoại. Trên máy tính giữ nguyên trạng thái người dùng chọn.
            if (mobileQuery.matches && document.body.classList.contains('tutor-sidebar-open')) {
                sidebarToggle?.click();
            }

            window.scrollTo({ top: 0, behavior: 'auto' });
            document.dispatchEvent(new CustomEvent('otsp:content-loaded', { detail: { url: url.href } }));
        } catch (error) {
            if (error.name !== 'AbortError') window.location.assign(url.href);
        } finally {
            if (activeRequest === request) {
                activeRequest = null;
                content.classList.remove('is-loading');
            }
        }
    }

    document.addEventListener('click', event => {
        if (event.defaultPrevented || isModifiedClick(event)) return;

        const link = event.target.closest('a[href]');
        if (!link || link.target || link.hasAttribute('download')) return;
        if (link.closest('[aria-disabled="true"], .disabled')) return;

        const href = link.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;

        const url = new URL(link.href, window.location.href);
        if (!isNavigableUrl(url)) return;

        event.preventDefault();
        if (url.href === window.location.href) return;

        navigate(url, true);
    });

    window.addEventListener('popstate', () => {
        const url = new URL(window.location.href);
        const key = url.pathname + url.search;

        // Chỉ đổi hash thì không cần tải lại
        if (key === currentKey) return;

        if (isNavigableUrl(url)) {
            navigate(url, false);
        } else {
            window.location.reload();
        }
    });
})();