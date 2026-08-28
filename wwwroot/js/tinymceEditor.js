// Plain JS, not run through a bundler - TinyMCE (vendored at wwwroot/lib/tinymce, see
// client/copy-tinymce.mjs) loads its own skins/icons/plugins dynamically at runtime from
// base_url below, so it isn't esbuild-bundle friendly the way the TipTap module it replaced was.
// Self-hosted/GPL distribution - no tiny.cloud, no API key, nothing here has a live network
// dependency beyond this app's own origin.

let tinyMcePromise = null;

function loadTinyMce() {
    if (window.tinymce) return Promise.resolve();
    if (!tinyMcePromise) {
        tinyMcePromise = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '/lib/tinymce/tinymce.min.js';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load TinyMCE'));
            document.head.appendChild(script);
        });
    }
    return tinyMcePromise;
}

const editors = new Map();

export async function createEditor(id, element, initialHtml, placeholderText, minHeight) {
    await loadTinyMce();
    await destroyEditor(id);

    const [editor] = await window.tinymce.init({
        target: element,
        base_url: '/lib/tinymce',
        suffix: '.min',
        // TinyMCE 6+ requires an explicit self-declaration even for self-hosted/GPL use -
        // without this it just shows "disabled" rather than actually enforcing a paid key.
        license_key: 'gpl',
        menubar: false,
        branding: false,
        promotion: false,
        placeholder: placeholderText || '',
        plugins: 'lists table link autolink wordcount',
        toolbar: 'undo redo | blocks | bold italic underline | forecolor backcolor | bullist numlist | link table | removeformat',
        // Fixed height with TinyMCE's own internal scrolling, not the autoresize plugin - that
        // grew to fit all content instead of scrolling, which blew out the page layout.
        height: minHeight || 200,
        setup: (ed) => {
            ed.on('init', () => {
                if (initialHtml) ed.setContent(initialHtml);
            });
        },
    });

    editors.set(id, editor);
}

export function getHtml(id) {
    return editors.get(id)?.getContent() ?? '';
}

export function setContent(id, html) {
    editors.get(id)?.setContent(html || '');
}

export async function destroyEditor(id) {
    const editor = editors.get(id);
    if (editor) {
        editor.remove();
        editors.delete(id);
    }
}
