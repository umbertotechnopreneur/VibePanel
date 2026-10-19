// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E
// Decode the engine's JPEG unchanged. Large frame data never enters the eval bridge.
window.vibePanelPreview = (() => {
    let busy = false, generation = 0, shown = false, fadeTimer;
    const stats = { frames: 0, decodedBitmaps: 0, maximumInFlight: 0 };
    const canvases = () => document.getElementById('vp-preview')?.querySelectorAll('canvas');
    const clearFade = () => {
        clearTimeout(fadeTimer);
        const previous = canvases()?.[0];
        if (previous) { previous.width = previous.height = 1; previous.style.opacity = '0'; }
    };
    function clear() {
        generation++;
        shown = false;
        clearFade();
        for (const canvas of canvases() || []) {
            canvas.width = canvas.height = 1;
            canvas.style.transition = 'none';
            canvas.style.opacity = '0';
        }
    }
    async function show(message) {
        const reply = { type: 'vp.preview.done', version: 1, id: message.id };
        if (busy) { window.external.sendMessage(JSON.stringify({ ...reply, error: 'busy' })); return; }
        busy = true;
        stats.maximumInFlight = Math.max(stats.maximumInFlight, 1);
        const token = generation;
        let bitmap;
        try {
            if (message.version !== 1 || typeof message.jpeg !== 'string' || message.jpeg.length > 600000) throw new Error('Invalid frame.');
            const binary = atob(message.jpeg);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < bytes.length; i++) bytes[i] = binary.charCodeAt(i);
            bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/jpeg' }));
            stats.decodedBitmaps++;
            if (token !== generation) throw new Error('Frame cleared.');
            if (bitmap.width < 128 || bitmap.height < 128 || bitmap.width > 4096 || bitmap.height > 4096 || bitmap.width * bitmap.height > 8388608) throw new Error('Invalid size.');
            const [previous, current] = canvases() || [];
            if (!current) throw new Error('Preview unavailable.');
            if (message.transition && shown) {
                clearFade();
                previous.width = current.width; previous.height = current.height;
                previous.getContext('2d', { alpha: false, willReadFrequently: true }).drawImage(current, 0, 0);
                previous.style.opacity = '1';
                current.style.transition = 'none';
                current.style.opacity = '0';
                void current.offsetWidth;
                current.style.transition = 'opacity 350ms ease';
                fadeTimer = setTimeout(clearFade, 350);
            }
            if (current.width !== bitmap.width) current.width = bitmap.width;
            if (current.height !== bitmap.height) current.height = bitmap.height;
            // Prefer a software backing for these small JPEG copies. A GPU canvas kept
            // hundreds of MB of upload resources cached during the fast preview check.
            current.getContext('2d', { alpha: false, willReadFrequently: true }).drawImage(bitmap, 0, 0);
            current.style.opacity = '1';
            shown = true;
            reply.width = bitmap.width; reply.height = bitmap.height;
            stats.frames++;
        } catch (_) { reply.error = 'decode'; }
        finally {
            if (bitmap) { bitmap.close(); stats.decodedBitmaps--; }
            busy = false;
            window.external.sendMessage(JSON.stringify(reply));
        }
    }
    // OpenSilver registers its receiver after this file. Keep its UI messages intact.
    const receive = window.external.receiveMessage.bind(window.external);
    window.external.receiveMessage = callback => receive(text => {
        const message = JSON.parse(text);
        if (message.type === 'vp.preview') { void show(message); return; }
        if (message.type === 'vp.preview.clear') { clear(); return; }
        callback(text);
    });
    return { stats };
})();
