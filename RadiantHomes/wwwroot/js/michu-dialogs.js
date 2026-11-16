/*
 * Michu Homes — standard dialogs (built on SweetAlert2, loaded before this file).
 *
 *   Confirm before an action   MichuDialog.confirm({...}) → Promise<boolean>
 *                              or declaratively: data-confirm="Text" on a <form>, a submit <button>, or an <a>
 *                                data-confirm-title   question shown as the title   (default "Are you sure?")
 *                                data-confirm-ok      label of the action button     (default "Confirm")
 *                                data-confirm-variant "danger" | "primary" | "accent" (default "primary")
 *   Result of an action        MichuDialog.toast("Saved.", "success" | "error" | "warning" | "info")
 *                              (server side: TempData["Success"] / ["Error"] / ["Warning"] / ["Info"] —
 *                               rendered by Pages/Shared/_FlashMessages.cshtml on the next page)
 *   Blocking message           MichuDialog.alert({ title, text, icon })
 *
 * Button order everywhere: [Cancel] [Action] — the action on the right, like .mh-form-actions.
 */
(function () {
    'use strict';

    const t = (text) => (window.MichuI18n ? window.MichuI18n.t(text) : text);

    if (!window.Swal) {
        console.warn('MichuDialog: SweetAlert2 is not loaded.');
        return;
    }

    const ICON_COLORS = {
        success: '#1E8E5A',
        error: '#C93A2E',
        warning: '#E5700A',
        info: '#2F6FB3',
        question: '#092B66'
    };

    const ACTION_CLASS = {
        danger: 'btn btn-danger',
        primary: 'btn btn-primary',
        accent: 'btn btn-accent'
    };

    const base = Swal.mixin({
        buttonsStyling: false,
        reverseButtons: true, // Cancel left, action right
        showClass: { popup: 'mh-swal-show' },
        hideClass: { popup: 'mh-swal-hide' },
        customClass: {
            popup: 'mh-swal',
            title: 'mh-swal-title',
            htmlContainer: 'mh-swal-text',
            actions: 'mh-swal-actions',
            confirmButton: 'btn btn-primary',
            cancelButton: 'btn btn-secondary',
            closeButton: 'mh-swal-close'
        }
    });

    const toastBase = Swal.mixin({
        toast: true,
        position: 'top-end',
        showConfirmButton: false,
        showCloseButton: true,
        closeButtonAriaLabel: t('Close'),
        customClass: { popup: 'mh-toast', title: 'mh-toast-title', closeButton: 'mh-swal-close' },
        didOpen: (el) => {
            el.addEventListener('mouseenter', Swal.stopTimer);
            el.addEventListener('mouseleave', Swal.resumeTimer);
        }
    });

    const MichuDialog = {
        /** Ask before doing something. Resolves true when the user confirms. */
        confirm(options = {}) {
            const variant = options.variant || 'primary';
            const icon = options.icon || (variant === 'danger' ? 'warning' : 'question');
            return base.fire({
                icon,
                iconColor: ICON_COLORS[icon],
                titleText: options.title || t('Are you sure?'),
                text: options.text || '',
                showCancelButton: true,
                confirmButtonText: options.confirmText || t('Confirm'),
                cancelButtonText: options.cancelText || t('Cancel'),
                focusCancel: variant === 'danger',
                customClass: {
                    popup: 'mh-swal',
                    title: 'mh-swal-title',
                    htmlContainer: 'mh-swal-text',
                    actions: 'mh-swal-actions',
                    confirmButton: ACTION_CLASS[variant] || ACTION_CLASS.primary,
                    cancelButton: 'btn btn-secondary'
                }
            }).then(result => result.isConfirmed === true);
        },

        /** A message the user has to acknowledge. */
        alert(options = {}) {
            const icon = options.icon || 'info';
            return base.fire({
                icon,
                iconColor: ICON_COLORS[icon],
                titleText: options.title || '',
                text: options.text || '',
                confirmButtonText: options.confirmText || t('OK')
            });
        },

        /** Non-blocking feedback. Success/info disappear by themselves; warnings/errors stay until closed. */
        toast(message, type = 'success') {
            if (!message) return Promise.resolve();
            const sticky = type === 'error' || type === 'warning';
            return toastBase.fire({
                icon: type,
                iconColor: ICON_COLORS[type],
                titleText: message,
                timer: sticky ? undefined : 4500,
                timerProgressBar: !sticky
            });
        },

        /** Show several queued messages (from the server) one after another. */
        async flash(messages) {
            for (const m of messages || []) {
                await MichuDialog.toast(m.text, m.type);
            }
        }
    };

    window.MichuDialog = MichuDialog;

    // ---- Declarative confirmations: data-confirm on forms, submit buttons and links ----
    function optionsFrom(el) {
        return {
            title: el.getAttribute('data-confirm-title') || undefined,
            text: el.getAttribute('data-confirm') || '',
            confirmText: el.getAttribute('data-confirm-ok') || undefined,
            variant: el.getAttribute('data-confirm-variant') || undefined
        };
    }

    // Capture phase: runs before validation, the page loader and any page handlers.
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (!(form instanceof HTMLFormElement)) return;

        if (form.dataset.mhConfirmed === 'true') {
            delete form.dataset.mhConfirmed;
            return; // already confirmed — let it through
        }

        const submitter = e.submitter;
        const source = submitter && submitter.hasAttribute('data-confirm') ? submitter
            : form.hasAttribute('data-confirm') ? form : null;
        if (!source) return;

        // Let client-side validation speak first; only confirm a form that can actually be submitted.
        if (window.jQuery && jQuery(form).data('validator') && !jQuery(form).valid()) return;

        e.preventDefault();
        e.stopImmediatePropagation();
        MichuDialog.confirm(optionsFrom(source)).then(ok => {
            if (!ok) return;
            form.dataset.mhConfirmed = 'true';
            if (submitter && typeof form.requestSubmit === 'function') form.requestSubmit(submitter);
            else if (typeof form.requestSubmit === 'function') form.requestSubmit();
            else form.submit();
        });
    }, true);

    document.addEventListener('click', function (e) {
        const link = e.target.closest('a[data-confirm]');
        if (!link || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        MichuDialog.confirm(optionsFrom(link)).then(ok => {
            if (ok) window.location.href = link.href;
        });
    }, true);
})();
