// Michu Homes — small shared front-end helpers (loaded on every page by Pages/Shared/_CoreScripts.cshtml).

/**
 * Photo slider used on the house / apartment details pages.
 * Same behaviour everywhere: advances every 5 s, pauses while the pointer is over it, keyboard arrows and
 * drag/swipe work. For people whose system asks for reduced motion the photos change with a crossfade
 * instead of sliding across the screen.
 * Expects the usual Swiper markup with .swiper-button-next/.swiper-button-prev/.swiper-pagination inside.
 */
window.initPhotoSlider = function (selector, options) {
    const t = (text) => (window.MichuI18n ? window.MichuI18n.t(text) : text);
    const el = typeof selector === 'string' ? document.querySelector(selector) : selector;
    if (!el || typeof Swiper === 'undefined') return null;

    const slideCount = el.querySelectorAll('.swiper-slide').length;
    const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const canSlide = slideCount > 1;

    return new Swiper(el, Object.assign({
        loop: canSlide,
        speed: 500,
        effect: reduceMotion ? 'fade' : 'slide',
        fadeEffect: { crossFade: true },
        grabCursor: canSlide,
        keyboard: { enabled: true, onlyInViewport: true },
        autoplay: canSlide
            ? { delay: 5000, disableOnInteraction: false, pauseOnMouseEnter: true }
            : false,
        pagination: { el: el.querySelector('.swiper-pagination'), clickable: true },
        navigation: { nextEl: el.querySelector('.swiper-button-next'), prevEl: el.querySelector('.swiper-button-prev') },
        a11y: { prevSlideMessage: t('Previous photo'), nextSlideMessage: t('Next photo'), paginationBulletMessage: t('Go to photo {{index}}') }
    }, options || {}));
};

/**
 * File inputs with data-max-bytes and/or data-extensions (".glb,.gltf"): the chosen file is checked at once,
 * not after a long upload. The message goes into the [data-file-error] element next to the input; the server
 * checks the same rules again.
 */
(function () {
    const t = (text, ...args) => (window.MichuI18n ? window.MichuI18n.t(text, ...args) : text);
    const selector = 'input[type=file][data-max-bytes], input[type=file][data-extensions]';

    function problem(input) {
        const file = input.files && input.files[0];
        if (!file) return '';
        const extensions = (input.dataset.extensions || '').split(',').map(e => e.trim().toLowerCase()).filter(Boolean);
        if (extensions.length && !extensions.some(e => file.name.toLowerCase().endsWith(e))) {
            return t('"{0}" is not an allowed file type ({1}).', file.name, extensions.join(', '));
        }
        const max = Number(input.dataset.maxBytes || 0);
        if (max && file.size > max) {
            return t('"{0}" is larger than {1} MB.', file.name, Math.round(max / (1024 * 1024)));
        }
        return '';
    }

    function show(input, message) {
        input.classList.toggle('is-invalid', !!message);
        const box = input.parentElement && input.parentElement.querySelector('[data-file-error]');
        if (box) box.textContent = message;
    }

    document.addEventListener('change', function (e) {
        const input = e.target.closest && e.target.closest(selector);
        if (input) show(input, problem(input));
    });

    // Capture phase: runs before the page loader and form validation, so a bad file stops the submit.
    document.addEventListener('submit', function (e) {
        const bad = Array.from(e.target.querySelectorAll(selector)).find(problem);
        if (!bad) return;
        e.preventDefault();
        show(bad, problem(bad));
        bad.focus();
    }, true);
})();

/**
 * Language picker (Pages/Shared/_LanguagePicker.cshtml): switches as soon as a language is chosen.
 * /language/{code} remembers the choice and reloads the current page in that language.
 */
(function () {
    const pickers = () => document.querySelectorAll('select[data-language-picker]');

    document.addEventListener('change', function (e) {
        const picker = e.target.closest('select[data-language-picker]');
        if (!picker) return;
        const current = picker.querySelector('option[selected]');
        if (current && current.value === picker.value) return;
        picker.disabled = true; // one switch at a time while the page reloads
        const returnUrl = picker.dataset.returnUrl || (location.pathname + location.search);
        location.href = '/language/' + encodeURIComponent(picker.value) + '?returnUrl=' + encodeURIComponent(returnUrl);
    });

    // Back/forward cache can bring the page back showing the language that was just picked (and disabled);
    // reset the picker to the language the page is actually in.
    window.addEventListener('pageshow', function () {
        pickers().forEach(function (picker) {
            const current = picker.querySelector('option[selected]');
            if (current) picker.value = current.value;
            picker.disabled = false;
        });
    });
})();
