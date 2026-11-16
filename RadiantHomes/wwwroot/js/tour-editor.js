// Michu Homes — rooms of an apartment's 3D walk-through (Pages/ApartmentHouses/Tour.cshtml).
// The owner walks through the model (wwwroot/js/model-view.js) and saves spots as named rooms. The list lives in
// the hidden RoomsJson field until "Save rooms" posts it (ApartmentModel3D.ParseRooms checks it again).
(function () {
    'use strict';

    const t = (text, ...args) => (window.MichuI18n ? window.MichuI18n.t(text, ...args) : text);
    const form = document.querySelector('[data-tour-editor]');
    const stage = document.querySelector('[data-model-stage]');
    if (!form || !stage) return;

    const list = form.querySelector('[data-tour-list]');
    const empty = form.querySelector('[data-tour-empty]');
    const json = form.querySelector('[data-tour-json]');
    const nameInput = form.querySelector('[data-tour-name]');
    const nameError = form.querySelector('[data-tour-name-error]');
    const addButton = form.querySelector('[data-tour-add-spot]');
    const help = form.querySelector('[data-tour-help]');
    const insideButton = form.querySelector('[data-tour-inside]');
    const overviewButton = form.querySelector('[data-tour-outside]');
    const dirtyNote = form.querySelector('[data-tour-dirty]');

    let rooms = [];
    try { rooms = JSON.parse(json.value || '[]'); } catch (e) { rooms = []; }
    let viewer = null;
    let dirty = false;

    const round = (n) => Math.round(n * 1000) / 1000;
    const snapshot = (view) => ({ x: round(view.x), y: round(view.y), z: round(view.z), theta: round(view.theta), phi: round(view.phi) });

    function changed() {
        dirty = true;
        dirtyNote.hidden = false;
        json.value = JSON.stringify(rooms);
        if (viewer) viewer.setRooms(rooms);
    }

    function iconButton(action, icon, label, extraClass) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-sm ' + (extraClass || 'btn-outline-secondary');
        button.dataset.act = action;
        button.title = label;
        button.setAttribute('aria-label', label);
        const i = document.createElement('i');
        i.className = 'bi ' + icon;
        i.setAttribute('aria-hidden', 'true');
        button.appendChild(i);
        return button;
    }

    function render(focus) {
        list.textContent = '';
        empty.hidden = rooms.length > 0;
        rooms.forEach((room, index) => {
            const row = document.createElement('li');
            row.className = 'mh-tour-row';
            row.dataset.index = String(index);

            const label = document.createElement('label');
            label.className = 'visually-hidden';
            label.htmlFor = `tourRoom${index}`;
            label.textContent = t('Room name');
            const input = document.createElement('input');
            input.id = `tourRoom${index}`;
            input.className = 'form-control form-control-sm';
            input.maxLength = 60;
            input.value = room.name;
            input.addEventListener('input', () => { room.name = input.value; changed(); });

            const actions = document.createElement('div');
            actions.className = 'mh-tour-row-actions';
            actions.append(
                iconButton('visit', 'bi-eye', t('Visit this room'), 'btn-outline-primary'),
                iconButton('here', 'bi-crosshair', t('Use the spot where you stand now')),
                iconButton('up', 'bi-arrow-up', t('Move up')),
                iconButton('down', 'bi-arrow-down', t('Move down')),
                iconButton('delete', 'bi-trash', t('Delete room'), 'btn-outline-danger'));
            actions.querySelector('[data-act="up"]').disabled = index === 0;
            actions.querySelector('[data-act="down"]').disabled = index === rooms.length - 1;

            row.append(label, input, actions);
            list.appendChild(row);
        });
        if (focus) {
            const target = list.querySelector(focus);
            if (target) target.focus();
        }
    }

    list.addEventListener('click', (e) => {
        const button = e.target.closest('button[data-act]');
        if (!button || !viewer) return;
        const index = Number(button.closest('[data-index]').dataset.index);
        switch (button.dataset.act) {
            case 'visit':
                viewer.enterRoom(index);
                break;
            case 'here': {
                const view = viewer.view();
                if (!view) { help.textContent = t('Go inside first: rooms are saved where you stand.'); return; }
                Object.assign(rooms[index], snapshot(view));
                changed();
                help.textContent = t('Room "{0}" now starts where you stand.', rooms[index].name);
                break;
            }
            case 'up':
            case 'down': {
                const to = button.dataset.act === 'up' ? index - 1 : index + 1;
                [rooms[index], rooms[to]] = [rooms[to], rooms[index]];
                changed();
                render(`[data-index="${to}"] [data-act="${button.dataset.act}"]:not(:disabled), [data-index="${to}"] input`);
                break;
            }
            case 'delete':
                rooms.splice(index, 1);
                changed();
                render(`[data-index="${Math.min(index, rooms.length - 1)}"] input`);
                help.textContent = t('Room deleted. Press "Save rooms" to keep the change.');
                break;
        }
    });

    addButton.addEventListener('click', () => {
        const view = viewer && viewer.view();
        if (!view) { help.textContent = t('Go inside first: rooms are saved where you stand.'); return; }
        const name = nameInput.value.trim();
        if (!name) {
            nameInput.classList.add('is-invalid');
            nameError.textContent = t('Give the room a name.');
            nameInput.focus();
            return;
        }
        nameInput.classList.remove('is-invalid');
        nameError.textContent = '';
        rooms.push(Object.assign({ name }, snapshot(view)));
        nameInput.value = '';
        changed();
        render();
        viewer.enterRoom(rooms.length - 1); // mark it as the current room
        help.textContent = t('Room "{0}" added. Walk to the next room, or press "Save rooms".', name);
    });

    nameInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); addButton.click(); } // don't submit the whole form
    });

    insideButton.addEventListener('click', () => viewer && viewer.goInside());
    overviewButton.addEventListener('click', () => viewer && viewer.overview());

    stage.addEventListener('michu:viewer-ready', (e) => {
        viewer = e.detail;
        viewer.setRooms(rooms);
        insideButton.disabled = false;
        overviewButton.disabled = false;
        viewer.onChange((v) => {
            const inside = v.mode === 'inside';
            addButton.disabled = !inside;
            insideButton.hidden = inside;
            overviewButton.hidden = !inside;
            help.textContent = inside
                ? t('Tap the floor to walk; drag to look around. Face the best view of the room, then add it.')
                : t('Go inside first: rooms are saved where you stand.');
        });
    });

    form.addEventListener('submit', () => { dirty = false; });
    window.addEventListener('beforeunload', (e) => {
        if (!dirty) return;
        e.preventDefault();
        e.returnValue = '';
    });

    render();
})();
