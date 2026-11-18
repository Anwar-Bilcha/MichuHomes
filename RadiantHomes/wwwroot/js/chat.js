/*
 * Chat conversation (Portal/Messages/Conversation, Staff/Inbox/Conversation). The page renders the messages and a
 * normal form that posts a message, so it works without JavaScript. This file adds live delivery through the
 * SignalR hub (/hubs/chat): messages appear on both sides without reloading, are marked read when seen, and a
 * message is sent once even after a double click or a retry (the browser's ClientMessageId). If the live
 * connection fails, the page asks for new messages every 10 seconds instead.
 */
(function () {
    'use strict';
    var root = document.querySelector('[data-chat]');
    if (!root) return;

    var conversationId = root.getAttribute('data-conversation');
    var meIsCustomer = root.getAttribute('data-me') === 'customer';
    var pollUrl = root.getAttribute('data-poll-url');
    var list = root.querySelector('[data-chat-list]');
    var form = root.querySelector('[data-chat-form]');
    var input = form ? form.querySelector('textarea') : null;
    var clientIdField = form ? form.querySelector('[name="ClientMessageId"]') : null;
    var errorBox = root.querySelector('[data-chat-error]');
    var texts = { read: root.getAttribute('data-text-read') || 'Read', sent: root.getAttribute('data-text-sent') || 'Sent' };
    var connection = null, live = false, pollTimer = 0, sending = false;

    function uuid() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
        });
    }
    function lastId() {
        var items = list.querySelectorAll('[data-id]');
        return items.length ? parseInt(items[items.length - 1].getAttribute('data-id'), 10) || 0 : 0;
    }
    function scrollDown() { list.scrollTop = list.scrollHeight; }
    function showError(text) { if (errorBox) { errorBox.textContent = text || ''; errorBox.hidden = !text; } }
    function setLive(on) { live = on; root.setAttribute('data-live', on ? 'on' : 'off'); }
    function time(iso) {
        var d = new Date(iso.endsWith && !/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso + 'Z' : iso);
        return isNaN(d) ? '' : d.toLocaleString([], { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
    }

    function add(m) {
        if (!m || list.querySelector('[data-id="' + m.id + '"]')) return false;
        var pending = list.querySelector('[data-client="' + m.clientMessageId + '"]:not([data-id])');
        var mine = m.fromCustomer === meIsCustomer;
        var li = pending || document.createElement('li');
        li.className = 'mh-msg' + (mine ? ' mh-msg-mine' : '');
        li.setAttribute('data-id', m.id);
        li.setAttribute('data-client', m.clientMessageId);
        li.setAttribute('data-from-customer', m.fromCustomer ? 'true' : 'false');
        li.setAttribute('data-read-state', m.read ? 'read' : 'sent');
        li.innerHTML = '';
        var body = document.createElement('div');
        body.className = 'mh-msg-body';
        body.textContent = m.body;
        var meta = document.createElement('div');
        meta.className = 'mh-msg-meta';
        var who = document.createElement('span');
        who.textContent = m.senderName + ' · ' + time(m.sentOn);
        meta.appendChild(who);
        if (mine) {
            var tick = document.createElement('i');
            tick.className = 'bi bi-check2-all';
            tick.setAttribute('data-read', '');
            tick.setAttribute('aria-label', m.read ? texts.read : texts.sent);
            meta.appendChild(tick);
        }
        li.appendChild(body);
        li.appendChild(meta);
        if (!pending) list.appendChild(li);
        var empty = list.querySelector('[data-chat-empty]');
        if (empty) empty.remove();
        scrollDown();
        return !mine;
    }

    function markRead() {
        if (document.hidden) return;
        if (live && connection) connection.invoke('MarkRead', conversationId).catch(function () { /* next time */ });
    }

    function catchUp() {
        if (!pollUrl) return Promise.resolve();
        return fetch(pollUrl + '&after=' + lastId(), { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (messages) {
                var fromOther = false;
                (messages || []).forEach(function (m) { if (add(m)) fromOther = true; });
                if (fromOther && !live) { /* read receipts are sent when the page next posts or reloads */ }
            })
            .catch(function () { /* offline */ });
    }

    function startPolling() {
        if (pollTimer || !pollUrl) return;
        pollTimer = setInterval(function () { if (!document.hidden) catchUp(); }, 10000);
    }
    function stopPolling() { clearInterval(pollTimer); pollTimer = 0; }

    // Sending: live through the hub when connected; otherwise the form posts normally.
    if (form && input) {
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); form.requestSubmit ? form.requestSubmit() : form.submit(); }
        });
        form.addEventListener('submit', function (e) {
            if (!live || !connection) return; // normal post
            e.preventDefault();
            var text = input.value.trim();
            if (!text || sending) return;
            var clientId = clientIdField.value || uuid();
            sending = true;
            showError('');
            connection.invoke('Send', conversationId, text, clientId)
                .then(function (result) {
                    if (result && result.ok) {
                        add(result.message);
                        input.value = '';
                        clientIdField.value = uuid();
                    } else {
                        showError(result && result.error);
                    }
                })
                .catch(function () {
                    // Connection lost while sending: the same ClientMessageId posted again is stored only once.
                    form.submit();
                })
                .then(function () { sending = false; input.focus(); });
        });
    }

    scrollDown();
    if (typeof window.signalR === 'undefined') { startPolling(); return; }

    connection = new window.signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .build();

    connection.on('message', function (m) {
        if (m.conversationId !== conversationId) return;
        if (add(m)) markRead();
    });
    connection.on('read', function (e) {
        if (!e || e.conversationId !== conversationId || e.byCustomer === meIsCustomer) return;
        list.querySelectorAll('.mh-msg-mine').forEach(function (li) {
            li.setAttribute('data-read-state', 'read');
            var tick = li.querySelector('[data-read]');
            if (tick) tick.setAttribute('aria-label', texts.read);
        });
    });
    connection.onreconnecting(function () { setLive(false); startPolling(); });
    connection.onreconnected(function () {
        connection.invoke('Join', conversationId).then(function (ok) {
            if (ok) { setLive(true); stopPolling(); catchUp().then(markRead); }
        });
    });
    connection.onclose(function () { setLive(false); startPolling(); });

    connection.start()
        .then(function () { return connection.invoke('Join', conversationId); })
        .then(function (ok) {
            if (!ok) { startPolling(); return; }
            setLive(true);
            markRead();
        })
        .catch(function () { setLive(false); startPolling(); });

    document.addEventListener('visibilitychange', function () { if (!document.hidden) { catchUp(); markRead(); } });
})();
