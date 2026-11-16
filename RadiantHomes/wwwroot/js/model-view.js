// Michu Homes — apartment "3D live view" (Pages/ApartmentHouses/_ApartmentModelViewer.cshtml).
//
// Outside ("overview"): the model turns on a stage; drag to turn it, pinch/scroll to zoom.
// Inside (when rooms are set up): the camera stands at eye height in a room; drag to look around, tap the floor
// to walk there, or pick a room. The viewer library and the model are only downloaded on "View in 3D".
//
// The rooms editor (ApartmentHouses/Tour, wwwroot/js/tour-editor.js) drives the same viewer through
// stage.michuViewer (see the api object below), available after the "michu:viewer-ready" event.
(function () {
    'use strict';

    const t = (text, ...args) => (window.MichuI18n ? window.MichuI18n.t(text, ...args) : text);
    const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const EYE_HEIGHT = 1.6;      // metres above the floor
    const INSIDE_RADIUS = 0.1;   // the camera orbits this close to the eye point, so dragging looks around
    const FLIGHT_MS = 1400;      // time to fly in/out before the camera limits are tightened/relaxed
    let library = null;

    // One import for the whole page; resolves once <model-viewer> is a working element.
    function loadLibrary(src) {
        if (!library) {
            library = import(src).then(() => customElements.whenDefined('model-viewer'));
            library.catch(() => { library = null; }); // allow a retry
        }
        return library;
    }

    // Screen-reader texts of the viewer itself, in the page's language.
    function a11yTexts() {
        const view = { front: 'Front view', back: 'Back view', left: 'Left side view', right: 'Right side view' };
        const texts = { 'interaction-prompt': t('Use the mouse, touch or arrow keys to turn the model.') };
        for (const [side, label] of Object.entries(view)) {
            texts[side] = t(label);
            texts['upper-' + side] = t(label + ' from above');
            texts['lower-' + side] = t(label + ' from below');
        }
        return texts;
    }

    const fullscreenEnabled = () => document.fullscreenEnabled || document.webkitFullscreenEnabled;
    const fullscreenElement = () => document.fullscreenElement || document.webkitFullscreenElement;

    function setup(stage) {
        const card = stage.closest('.mh-model-card');
        const viewer = stage.querySelector('model-viewer');
        const cover = stage.querySelector('[data-model-cover]');
        const start = stage.querySelector('[data-model-start]');
        const progress = stage.querySelector('[data-model-progress]');
        const bar = progress.querySelector('.progress-bar');
        const meter = progress.querySelector('[role="progressbar"]');
        const error = stage.querySelector('[data-model-error]');
        const tip = stage.querySelector('[data-tour-tip]');
        const toolbar = card.querySelector('[data-model-toolbar]');
        const hint = card.querySelector('[data-model-hint]');
        const status = card.querySelector('[data-model-status]');
        const rotate = card.querySelector('[data-model-action="rotate"]');
        const reset = card.querySelector('[data-model-action="reset"]');
        const fullscreen = card.querySelector('[data-model-action="fullscreen"]');
        const roomsNav = card.querySelector('[data-tour-rooms]');
        const dataBlock = card.querySelector('[data-tour-data]');
        const tour = stage.dataset.tour === 'true';
        // Walk-throughs look down into the rooms from outside ("dollhouse"); a plain model is seen from the front.
        const overviewOrbit = tour ? '0deg 50deg 105%' : '0deg 75deg 105%';

        let rooms = dataBlock ? JSON.parse(dataBlock.textContent || '[]') : [];
        // Display names: the room buttons are rendered (and translated) on the server.
        let labels = rooms.map((room, i) => {
            const label = roomsNav && roomsNav.querySelector(`[data-tour-room="${i}"] [data-room-label]`);
            return label ? label.textContent.trim() : room.name;
        });
        let mode = 'overview';
        let currentRoom = -1;
        let spot = null;          // where the visitor stands inside: { x, y (floor), z }
        let settleTimer = 0;
        let tipShown = false;
        const changeListeners = [];

        // ---------- loading ----------

        const setProgress = (fraction) => {
            const percent = Math.round(Math.min(1, Math.max(0, fraction)) * 100);
            bar.style.width = percent + '%';
            meter.setAttribute('aria-valuenow', String(percent));
        };

        const fail = () => {
            progress.hidden = true;
            error.hidden = false;
            start.disabled = false;
            start.hidden = false;
            status.textContent = '';
        };

        // Keyboard users continue in the viewer once the start button is gone. Its arrow-key handling lives on a
        // focusable inner element, which only becomes focusable when the model is revealed (just after "load").
        let wantsFocus = false;
        const focusViewer = () => {
            if (!wantsFocus) return;
            const input = viewer.shadowRoot && viewer.shadowRoot.querySelector('[tabindex="0"]');
            (input || viewer).focus({ preventScroll: true });
            if (viewer.shadowRoot && viewer.shadowRoot.activeElement) wantsFocus = false;
        };

        // The viewer's own "wiggle" hint (outside view only) is switched off once the visitor turns the model or
        // uses a toolbar button, so it doesn't move the camera after "Reset view".
        const stopHint = () => { viewer.interactionPrompt = 'none'; };
        viewer.addEventListener('camera-change', (e) => { if (e.detail && e.detail.source === 'user-interaction') stopHint(); });
        toolbar.addEventListener('click', stopHint);

        viewer.addEventListener('progress', (e) => setProgress(e.detail.totalProgress));
        viewer.addEventListener('error', fail);
        viewer.addEventListener('load', function () {
            cover.hidden = true;
            toolbar.hidden = false;
            if (roomsNav) roomsNav.hidden = false;
            status.textContent = t('3D model loaded.');
            wantsFocus = !stage.dataset.autoload || stage.dataset.autoload === 'false';
            focusViewer();
            if (tour) {
                viewer.disableTap = true; // taps are ours: walking
                buildHotspots();
            }
            stage.dispatchEvent(new CustomEvent('michu:viewer-ready', { bubbles: true, detail: api }));
            if (tour && rooms.length && stage.dataset.autoload !== 'true') {
                setTimeout(() => enterRoom(0), reduceMotion ? 0 : 500); // the tour starts inside
            }
        });
        viewer.addEventListener('poster-dismissed', focusViewer);

        async function begin() {
            start.disabled = true;
            error.hidden = true;
            progress.hidden = false;
            setProgress(0);
            status.textContent = t('Loading the 3D model…');
            try {
                await loadLibrary(stage.dataset.viewerSrc);
            } catch (e) {
                fail();
                return;
            }
            start.hidden = true;
            viewer.a11y = a11yTexts();
            viewer.autoRotate = !reduceMotion && !tour;
            rotate.setAttribute('aria-pressed', String(viewer.autoRotate));
            if (viewer.getAttribute('src') === stage.dataset.modelSrc) {
                viewer.setAttribute('src', ''); // retry after an error: load it again
            }
            viewer.setAttribute('src', stage.dataset.modelSrc);
        }
        start.addEventListener('click', begin);

        // ---------- outside / inside ----------

        function setMode(next) {
            mode = next;
            card.dataset.mode = next;
            hint.textContent = next === 'inside'
                ? t('Drag to look around. Tap the floor to walk there.')
                : t('Drag to turn the model; pinch or scroll to zoom.');
            rotate.disabled = next === 'inside';
            if (roomsNav) {
                roomsNav.querySelectorAll('[data-tour-room]').forEach((b) =>
                    b.setAttribute('aria-pressed', String(next === 'inside' && Number(b.dataset.tourRoom) === currentRoom)));
                roomsNav.querySelector('[data-tour-overview]').setAttribute('aria-pressed', String(next === 'overview'));
            }
            changeListeners.forEach((listener) => listener(api));
        }

        function settle(action) {
            clearTimeout(settleTimer);
            if (reduceMotion) {
                viewer.jumpCameraToGoal();
                action();
            } else {
                settleTimer = setTimeout(action, FLIGHT_MS);
            }
        }

        /**
         * Stand at a floor point (model coordinates, metres) looking in the given orbit direction.
         * freshView: back to the normal (unzoomed) view — entering a room or "Reset view"; walking keeps the zoom.
         */
        function standAt(x, floorY, z, theta, phi, roomIndex, freshView) {
            const wasInside = mode === 'inside';
            spot = { x, y: floorY, z };
            currentRoom = roomIndex;
            viewer.autoRotate = false;
            rotate.setAttribute('aria-pressed', 'false');
            viewer.disablePan = true;
            if (!wasInside) {
                // Allow the camera close to its target first (changing a limit jumps the camera to its goal).
                viewer.minCameraOrbit = `auto 20deg ${INSIDE_RADIUS}m`;
                viewer.minFieldOfView = '30deg';
                viewer.maxFieldOfView = '95deg';
            }
            if (!wasInside || freshView) viewer.fieldOfView = '75deg';
            viewer.cameraTarget = `${x}m ${floorY + EYE_HEIGHT}m ${z}m`;
            viewer.cameraOrbit = `${theta}rad ${phi}rad ${INSIDE_RADIUS}m`;
            if (!wasInside) {
                // Once there, lock the distance: zooming then only narrows or widens the view (no walking through walls).
                settle(() => { viewer.maxCameraOrbit = `auto 160deg ${INSIDE_RADIUS}m`; });
            } else if (reduceMotion) {
                viewer.jumpCameraToGoal();
            }
            setMode('inside');
            if (!tipShown && tip) {
                tipShown = true;
                tip.hidden = false;
                setTimeout(() => { tip.hidden = true; }, 5000);
            }
        }

        function enterRoom(index) {
            const room = rooms[index];
            if (!room) return;
            standAt(room.x, room.y, room.z, room.theta, room.phi, index, true);
            status.textContent = labels[index] || room.name;
        }

        /** Inside at the middle of the model (the editor's "Go inside"). */
        function goInside() {
            const c = viewer.getBoundingBoxCenter();
            const d = viewer.getDimensions();
            standAt(c.x, c.y - d.y / 2, c.z, viewer.getCameraOrbit().theta, Math.PI / 2, -1, true);
        }

        function walkTo(x, floorY, z) {
            const orbit = viewer.getCameraOrbit();
            // Coming from outside: look straight ahead instead of down at the floor.
            standAt(x, floorY, z, orbit.theta, mode === 'inside' ? orbit.phi : Math.PI / 2, -1, false);
        }

        function overview() {
            currentRoom = -1;
            spot = null;
            viewer.maxCameraOrbit = 'auto auto auto';
            viewer.cameraTarget = 'auto auto auto';
            viewer.cameraOrbit = overviewOrbit;
            viewer.fieldOfView = 'auto';
            // Relax the inside limits once the camera is back out.
            settle(() => {
                viewer.minCameraOrbit = 'auto auto auto';
                viewer.minFieldOfView = 'auto';
                viewer.maxFieldOfView = 'auto';
                viewer.disablePan = false;
            });
            setMode('overview');
            status.textContent = t('Overview');
        }

        // ---------- walking: a tap (not a drag) on the floor ----------

        const marker = document.createElement('div');
        marker.className = 'mh-move-marker';
        marker.slot = 'hotspot-move';
        marker.dataset.position = '0m 0m 0m';
        marker.dataset.normal = '0m 1m 0m';
        marker.setAttribute('aria-hidden', 'true');

        let press = null;
        viewer.addEventListener('pointerdown', (e) => {
            press = { x: e.clientX, y: e.clientY, time: performance.now(), id: e.pointerId };
        });
        viewer.addEventListener('pointerup', (e) => {
            if (!tour || !press || e.pointerId !== press.id) return;
            const isTap = Math.hypot(e.clientX - press.x, e.clientY - press.y) < 8 && performance.now() - press.time < 600;
            press = null;
            if (!isTap) return;
            const hit = viewer.positionAndNormalFromPoint(e.clientX, e.clientY);
            if (!hit) return;
            let to = { x: hit.position.x, y: hit.position.y, z: hit.position.z };
            if (hit.normal.y < 0.7) {
                // A wall or a piece of furniture: inside, walk up to it on the same floor and stop a little in front.
                const dx = to.x - (spot ? spot.x : 0), dz = to.z - (spot ? spot.z : 0);
                const distance = Math.hypot(dx, dz);
                if (mode !== 'inside' || !spot || distance < 0.9) {
                    status.textContent = t('Tap the floor to walk there.');
                    return;
                }
                const keep = (distance - 0.6) / distance;
                to = { x: spot.x + dx * keep, y: spot.y, z: spot.z + dz * keep };
            }
            if (!marker.isConnected) viewer.appendChild(marker);
            viewer.updateHotspot({ name: 'hotspot-move', position: `${to.x}m ${to.y}m ${to.z}m` });
            marker.classList.remove('is-shown');
            void marker.offsetWidth; // restart the animation
            marker.classList.add('is-shown');
            walkTo(to.x, to.y, to.z);
        });

        // ---------- room labels in the overview ----------

        function buildHotspots() {
            viewer.querySelectorAll('.mh-room-hotspot').forEach((h) => h.remove());
            rooms.forEach((room, i) => {
                const label = document.createElement('button');
                label.type = 'button';
                label.className = 'mh-room-hotspot';
                label.slot = `hotspot-room-${i}`;
                label.dataset.position = `${room.x}m ${room.y + 1.2}m ${room.z}m`;
                label.dataset.normal = '0m 1m 0m';
                label.textContent = labels[i] || room.name;
                label.addEventListener('click', () => enterRoom(i));
                viewer.appendChild(label);
            });
        }

        if (roomsNav) {
            roomsNav.addEventListener('click', (e) => {
                const button = e.target.closest('button');
                if (!button) return;
                if (button.hasAttribute('data-tour-overview')) overview();
                else enterRoom(Number(button.dataset.tourRoom));
            });
        }

        // ---------- toolbar ----------

        rotate.addEventListener('click', function () {
            viewer.autoRotate = !viewer.autoRotate;
            rotate.setAttribute('aria-pressed', String(viewer.autoRotate));
        });

        reset.addEventListener('click', function () {
            if (mode === 'inside') {
                if (currentRoom >= 0) enterRoom(currentRoom);
                else if (spot) standAt(spot.x, spot.y, spot.z, viewer.getCameraOrbit().theta, Math.PI / 2, -1, true);
                return;
            }
            viewer.cameraOrbit = overviewOrbit;
            viewer.cameraTarget = 'auto auto auto';
            viewer.fieldOfView = 'auto';
            if (viewer.resetTurntableRotation) viewer.resetTurntableRotation();
        });

        if (!fullscreenEnabled()) {
            fullscreen.hidden = true; // e.g. Safari on iPhone
        } else {
            const label = fullscreen.querySelector('span');
            fullscreen.addEventListener('click', function () {
                if (fullscreenElement() === card) {
                    (document.exitFullscreen || document.webkitExitFullscreen).call(document);
                } else {
                    (card.requestFullscreen || card.webkitRequestFullscreen).call(card);
                }
            });
            const sync = () => {
                const on = fullscreenElement() === card;
                label.textContent = on ? t('Exit full screen') : t('Full screen');
                fullscreen.querySelector('i').className = on ? 'bi bi-fullscreen-exit' : 'bi bi-arrows-fullscreen';
            };
            document.addEventListener('fullscreenchange', sync);
            document.addEventListener('webkitfullscreenchange', sync);
        }

        // ---------- api for the rooms editor ----------

        const api = {
            get mode() { return mode; },
            get room() { return currentRoom; },
            /** Where the visitor stands and looks now (null outside). */
            view() {
                if (mode !== 'inside' || !spot) return null;
                const orbit = viewer.getCameraOrbit();
                return { x: spot.x, y: spot.y, z: spot.z, theta: orbit.theta, phi: orbit.phi };
            },
            setRooms(list, names) {
                rooms = list;
                labels = names || list.map((r) => r.name);
                buildHotspots();
            },
            enterRoom,
            goInside,
            overview,
            onChange(listener) { changeListeners.push(listener); },
        };
        stage.michuViewer = api;

        if (stage.dataset.autoload === 'true') begin();
    }

    document.querySelectorAll('[data-model-stage]').forEach(setup);
})();
