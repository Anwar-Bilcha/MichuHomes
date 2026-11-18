/*
 * Video call with an agent (Pages/LiveVideoMeet/_CallFrame.cshtml): loads the JaaS iframe API, starts the call
 * with the server-signed token and keeps the status line and the panels (loading, error, ended) in step with it.
 * All texts are in the page (translated there); this file only shows or hides them.
 * Leaving the call tells the server (data-end-url), so the call log is closed; the customer's page also polls the
 * call's status (data-status-url) and ends the wait when an agent declines the call or nobody answers in time.
 */
(function () {
    'use strict';
    var root = document.querySelector('[data-call]');
    if (!root) return;

    var isCustomer = root.getAttribute('data-role') === 'customer';
    var frameHost = root.querySelector('[data-call-frame]');
    var slowNote = root.querySelector('[data-call-slow]');
    var api = null, ended = false, joined = false, others = 0, slowTimer = 0, startTimer = 0, statusTimer = 0, told = false;
    var endUrl = root.getAttribute('data-end-url');
    var statusUrl = root.getAttribute('data-status-url');
    var audioOnly = root.getAttribute('data-audio-only') === 'true';
    var tokenInput = root.querySelector('[data-call-token] input[name="__RequestVerificationToken"]');

    // This side left: the server closes the call log (once; keepalive survives closing the tab).
    function tellLeft() {
        if (told || !endUrl || !tokenInput) return;
        told = true;
        var body = new FormData();
        body.append('__RequestVerificationToken', tokenInput.value);
        try {
            fetch(endUrl, { method: 'POST', body: body, credentials: 'same-origin', keepalive: true }).catch(function () { /* offline */ });
        } catch (e) { /* very old browser */ }
    }

    function showDevice(kind) {
        var box = root.querySelector('[data-call-device]');
        if (!box) return;
        box.querySelectorAll('[data-call-device-kind]').forEach(function (el) { el.hidden = el.getAttribute('data-call-device-kind') !== kind; });
        box.hidden = false;
    }

    // Customer: a declined or missed call ends the wait with its own panel.
    function closedBy(state, reason) {
        if (ended) return;
        ended = true;
        told = true; // the server already closed it
        clearTimeout(slowTimer);
        clearInterval(statusTimer);
        if (slowNote) slowNote.hidden = true;
        if (api) { try { api.dispose(); } catch (e) { /* already gone */ } }
        var reasonEl = root.querySelector('[data-call-reason]');
        if (reasonEl) reasonEl.textContent = reason || '';
        status('ended');
        panel(state);
    }

    function pollStatus() {
        if (!statusUrl || ended || document.hidden) return;
        fetch(statusUrl, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (s) {
                if (!s) return;
                if (s.status === 'Rejected') closedBy('declined', s.reason);
                else if (s.status === 'Missed') closedBy('missed', s.reason);
            })
            .catch(function () { /* offline: try again next time */ });
    }

    function status(name) {
        root.querySelectorAll('[data-call-status]').forEach(function (el) { el.hidden = el.getAttribute('data-call-status') !== name; });
        root.setAttribute('data-state', name);
    }
    function panel(name) {
        root.querySelectorAll('[data-call-panel]').forEach(function (el) { el.hidden = el.getAttribute('data-call-panel') !== name; });
    }

    // The call fills the screen below the site's top bar (phones and desktops alike).
    function fit() {
        var top = root.getBoundingClientRect().top + window.scrollY;
        root.style.height = Math.max(440, window.innerHeight - top - 12) + 'px';
    }

    function fail() {
        if (ended) return;
        ended = true;
        clearTimeout(startTimer);
        if (api) { try { api.dispose(); } catch (e) { /* already gone */ } }
        status('error');
        panel('error');
    }

    function refresh() {
        if (ended || !joined) return;
        if (api) {
            try { others = Math.max(0, api.getNumberOfParticipants() - 1); } catch (e) { /* keep the event count */ }
        }
        status(others > 0 ? 'joined' : (root.getAttribute('data-state') === 'joined' ? 'alone' : 'waiting'));
        if (others > 0 && slowNote) slowNote.hidden = true;
    }

    function end() {
        if (ended) return;
        ended = true;
        tellLeft();
        clearTimeout(slowTimer);
        clearInterval(statusTimer);
        if (slowNote) slowNote.hidden = true;
        try { api.dispose(); } catch (e) { /* already gone */ }
        status('ended');
        panel('ended');
    }

    function start() {
        if (typeof window.JitsiMeetExternalAPI !== 'function') { fail(); return; }
        api = new window.JitsiMeetExternalAPI(root.getAttribute('data-domain'), {
            roomName: root.getAttribute('data-room'),
            jwt: root.getAttribute('data-jwt'),
            parentNode: frameHost,
            width: '100%',
            height: '100%',
            configOverwrite: {
                subject: 'Michu Homes',            // instead of the technical room name
                localSubject: 'Michu Homes',
                prejoinConfig: { enabled: true },
                disableDeepLinking: true,          // phones stay in the browser instead of "get the app"
                disableInviteFunctions: true,      // the room is private to the customer and the agent
                enableWelcomePage: false,
                enableClosePage: false,
                startAudioOnly: audioOnly,         // "Audio only": no camera at all (weak connection or no camera)
                startWithVideoMuted: audioOnly
            },
            interfaceConfigOverwrite: { MOBILE_APP_PROMO: false, SHOW_PROMOTIONAL_CLOSE_PAGE: false }
        });

        api.getIFrame().addEventListener('load', function () {
            clearTimeout(startTimer);
            if (!ended && !joined) { panel(null); status('prejoin'); }
        });
        api.addListener('videoConferenceJoined', function () {
            joined = true;
            panel(null);
            refresh();
            if (isCustomer && slowNote) {
                slowTimer = setTimeout(function () { if (!ended && others === 0) slowNote.hidden = false; }, 3 * 60 * 1000);
            }
        });
        api.addListener('participantJoined', function () { others++; refresh(); });
        api.addListener('participantLeft', function () { others = Math.max(0, others - 1); refresh(); });
        api.addListener('videoConferenceLeft', end);
        api.addListener('readyToClose', end);
        api.addListener('errorOccurred', function (e) { if (e && e.error && e.error.isFatal) fail(); });
        api.addListener('cameraError', function () { if (!audioOnly) showDevice('camera'); });
        api.addListener('micError', function () { showDevice('mic'); });
    }

    fit();
    window.addEventListener('resize', fit);
    window.addEventListener('pagehide', function () { if (joined && !ended) tellLeft(); });
    if (isCustomer && statusUrl) statusTimer = setInterval(pollStatus, 10000);
    status('loading');
    panel('loading');

    // The video service must answer within 30 seconds.
    startTimer = setTimeout(fail, 30000);
    var script = document.createElement('script');
    script.src = root.getAttribute('data-script');
    script.async = true;
    script.onload = start;
    script.onerror = fail;
    document.head.appendChild(script);
})();
