// Structured click-to-edit document editor - replaces DocumentEditor.razor's earlier full-document
// TinyMCE surface. Ported from Will's team's standalone reference tool
// (S:\Common Corporate\AI Helper output  editor\Conwy_HTML_Document_Editor_v2_2.html): a live
// preview of the document renders in an iframe; hovering a text-bearing element highlights it;
// clicking it seeds a small rich-text box in a side panel with that element's innerHTML only;
// "Apply" writes the edited HTML back into just that element, never touching the surrounding
// document structure.
//
// Architecture note (deliberate deviation from the reference tool): the reference iframe used
// sandbox="allow-same-origin allow-scripts" and drove the preview via direct frame.contentDocument
// access. That flag combination is a documented sandbox-escape pattern (MDN: a sandboxed frame with
// both flags can request its own sandbox's removal), which matters here because the framed content
// is LLM-generated and passes through this editor's own "edit" surface. This module instead uses
// sandbox="allow-scripts" alone (no allow-same-origin): the framed document keeps its own unique,
// opaque ("null") origin, so `frame.contentDocument` is genuinely inaccessible from here (confirmed
// empirically while building this - accessing it returns null rather than throwing), and the parent
// and the framed document instead talk over `postMessage`, with a small bootstrap script injected
// into the framed document itself doing the hover/click detection and DOM edits on this side's
// instructions. This keeps scripts able to run inside the preview (so e.g. a `<details>`/`<summary>`
// disclosure or any other embedded interactivity in the source document still behaves normally while
// being edited) while removing the specific escape vector.
//
// The `event.source === iframe.contentWindow` check (not `event.origin` string matching) is what
// authenticates a message as actually coming from this editor's own iframe - an opaque-origin
// frame's messages always carry `event.origin === "null"`, which is not a usable identity check
// (also confirmed empirically: passing the literal string "null" as a postMessage targetOrigin
// throws a SyntaxError in Chromium, so '*' is used for both directions here instead).
//
// PDF export (see printDocument() below) reuses this exact same live preview via the browser's own
// print-to-PDF rather than a separate server-side HTML->PDF reconstruction, so what gets exported is
// genuinely what's on screen. It works the same postMessage way as everything else here for the same
// reason: `iframe.contentWindow.print()` called from this side throws a SecurityError (`print` isn't
// among the handful of properties exposed on a cross-origin WindowProxy), so instead a `print`
// message is posted INTO the frame and its own bootstrap script calls `window.print()` on itself.
// That also requires `allow-modals` alongside `allow-scripts` in the iframe's sandbox - without it,
// a sandboxed frame's own window.print() is a silent no-op (confirmed empirically: no exception, no
// beforeprint/afterprint, no dialog). allow-modals only unlocks modal dialogs (print/alert/confirm/
// prompt); it does not restore allow-same-origin or weaken the opaque-origin isolation above.
//
// Whatever HTML this module hands back to Blazor on Save/getHtml is only ever a body-level
// fragment (matching GeneratedDocument.HtmlContent's existing convention, and what
// wwwroot/js/tinymceEditor.js's own getHtml already returns) - never a full <html> document. The
// caller (DocumentEditor.razor) re-sanitizes it through HtmlContentSanitizer before persisting,
// exactly as it already did for the TinyMCE editor this replaces.

const EDITABLE_SELECTOR = 'h1,h2,h3,h4,h5,h6,p,span,a,li,td,th,label,button,dt,dd,figcaption,caption,blockquote,cite,em,strong,b,i,u,small,sub,sup,summary,legend,option';
// Selectable but NOT text-editable via the box below - container/structural elements where the
// meaningful action is deleting the whole thing, not hand-editing their raw markup (a table's
// internal structure is far too easy to break by editing it as a flat contenteditable blob). A
// text-less <div> (a pure layout/flex/grid wrapper) is handled separately in findSelectable,
// since a <div> can go either way depending on whether it happens to have direct text of its own.
const STRUCTURAL_SELECTOR = 'table,thead,tbody,tfoot,tr,ul,ol,section,article,header,footer,nav,aside,figure,form,fieldset,dl';
const HOVER_CLASS = 'conwy-editor-highlight';
const SELECTED_CLASS = 'conwy-editor-selected';
const BOOTSTRAP_STYLE_ID = 'conwy-editor-injected-style';
const BOOTSTRAP_SCRIPT_ID = 'conwy-editor-injected-script';
const MSG = 'conwy-structured-editor';

const instances = new Map();

/// <summary>
/// `container` is a single, otherwise-empty element Blazor renders and hands us via @ref (the same
/// shape as tinymceEditor.js's `element` param) - this module owns everything under it, building the
/// preview iframe and the side edit panel itself, so there are no per-keystroke/per-click round
/// trips back through the Blazor circuit for something that's entirely a client-side interaction.
/// Resolves once the framed document's bootstrap script has confirmed it's attached and ready (or,
/// failing that, after a short timeout - see the ready/readyTimeout race below).
/// </summary>
export function createEditor(id, container, initialHtml) {
    destroyEditor(id);

    const state = {
        container,
        iframe: null,
        panel: {},
        ready: null,
        readyResolve: null,
        selectedPath: null,
        originalHtml: '',
        undoStack: [], // { path, tag, oldHtml, newHtml }
        pending: new Map(), // requestId -> { resolve, reject }
        nextRequestId: 1,
        onMessage: null,
    };
    instances.set(id, state);

    injectSharedStyles();
    buildPanel(state);

    const readyPromise = new Promise((resolve) => { state.readyResolve = resolve; });
    const readyTimeout = new Promise((resolve) => {
        setTimeout(() => {
            if (state.readyResolve) {
                console.warn('structuredEditor: preview frame did not report ready within 5s; continuing anyway.');
                resolve();
            }
        }, 5000);
    });
    state.ready = Promise.race([readyPromise, readyTimeout]);

    state.onMessage = (event) => handleMessage(id, state, event);
    window.addEventListener('message', state.onMessage);

    loadDocument(state, initialHtml || '');

    return state.ready;
}

/// <summary>
/// Round-trips to the framed document for its current body HTML (a fragment - see module header).
/// Only content that has actually gone through "Apply Change" is reflected here; unapplied text
/// sitting in the side panel's edit box is deliberately not included, matching the reference tool
/// (its own applyChange() is the only thing that ever mutates the previewed document).
/// </summary>
export async function getHtml(id) {
    const state = instances.get(id);
    if (!state || !state.iframe) return '';

    try {
        return await requestFromFrame(state, 'getHtml', {}, 2500);
    } catch (err) {
        console.warn('structuredEditor: getHtml request to preview frame timed out or failed; returning last-known content.', err);
        return state.lastKnownHtml || '';
    }
}

/// <summary>
/// Triggers the browser's native print flow against the SAME content already rendered in the live
/// preview iframe, so PDF export is genuinely WYSIWYG rather than a separately-reconstructed
/// document (see DocumentExportService.ToDocx/ToHtml, which still use HtmlBlockParser + a renderer
/// for their own formats - PDF used to as well, via PdfRenderer, until this replaced it).
///
/// Posts a `print` message INTO the iframe rather than calling `.print()` on it directly from here.
/// Both were tried empirically while building this: `state.iframe.contentWindow.print()` throws a
/// SecurityError in Chromium - `print` is not among the small set of properties (window, close,
/// closed, focus, blur, postMessage, location, ...) exposed on a cross-origin WindowProxy, so it
/// doesn't matter that Window.print() itself carries no same-origin requirement in the spec; the
/// property read to get at it is what's blocked. Posting the message instead lets the framed
/// document's own bootstrap script (running same-realm inside the frame - see BOOTSTRAP_SOURCE) call
/// `window.print()` on itself, which is unrestricted. This also requires the iframe's `sandbox` to
/// include `allow-modals` (see loadDocument) - confirmed empirically that window.print() inside a
/// sandboxed frame without it is a silent no-op.
///
/// Fire-and-forget: no response is awaited (there's nothing to resolve - see the bootstrap's own
/// `print` case).
/// </summary>
export function printDocument(id) {
    const state = instances.get(id);
    if (!state || !state.iframe) return;
    state.iframe.contentWindow.postMessage({ __ns: MSG, type: 'print' }, '*');
}

export function destroyEditor(id) {
    const state = instances.get(id);
    if (!state) return;

    if (state.onMessage) window.removeEventListener('message', state.onMessage);
    for (const { reject } of state.pending.values()) {
        try { reject(new Error('Editor destroyed')); } catch { /* no-op */ }
    }
    state.pending.clear();

    if (state.container) state.container.innerHTML = '';
    instances.delete(id);
}

// ---------------------------------------------------------------------------------------------
// Preview iframe setup
// ---------------------------------------------------------------------------------------------

function loadDocument(state, bodyHtml) {
    state.lastKnownHtml = bodyHtml;
    state.selectedPath = null;
    state.undoStack = [];
    updateUndoButton(state);
    showNoSelection(state);

    const iframe = document.createElement('iframe');
    iframe.className = 'structured-editor-frame';
    iframe.title = 'Document preview - click any text to edit it';
    // allow-scripts, deliberately no allow-same-origin - see module header comment. allow-modals is
    // also required (confirmed empirically - see printDocument()) for the framed document's own
    // window.print() call to do anything at all: without it, a sandboxed frame silently no-ops the
    // call (no exception, no beforeprint/afterprint event, no dialog). allow-modals only permits
    // modal dialogs (alert/confirm/prompt/print) - it does not restore allow-same-origin or grant
    // any DOM/script access back to this frame, so the opaque-origin isolation this module relies on
    // is unaffected.
    iframe.setAttribute('sandbox', 'allow-scripts allow-modals');
    iframe.srcdoc = buildFramedDocument(bodyHtml);

    state.panel.previewHost.innerHTML = '';
    state.panel.previewHost.appendChild(iframe);
    state.iframe = iframe;
}

function buildFramedDocument(bodyHtml) {
    return `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style id="${BOOTSTRAP_STYLE_ID}">
  body { font-family: Calibri, Arial, sans-serif; margin: 1rem; color: #212529; }
  /* Hover/selection highlighting is this editor's own UI chrome, not part of the document being
     authored - scoped to screen so printDocument()'s window.print() (and a user's own Ctrl+P
     inside the frame) never bakes an outline/highlight into the PDF, even if an element happens to
     still carry HOVER_CLASS/SELECTED_CLASS at print time. */
  @media screen {
    .${HOVER_CLASS} { outline: 2px solid #7c76b7 !important; outline-offset: 2px !important; background-color: rgba(124, 118, 183, 0.08) !important; cursor: pointer !important; }
    .${SELECTED_CLASS} { outline: 3px solid #00927e !important; outline-offset: 2px !important; background-color: rgba(0, 146, 126, 0.08) !important; }
  }
</style>
</head>
<body>${bodyHtml}
<script id="${BOOTSTRAP_SCRIPT_ID}">${BOOTSTRAP_SOURCE}</script>
</body>
</html>`;
}

// Injected verbatim into the framed (opaque-origin) document as a <script> tag. Cannot share scope
// with the rest of this module - it runs in a completely separate JS realm on the other side of the
// postMessage boundary, so it's kept self-contained and only talks back via postMessage.
const BOOTSTRAP_SOURCE = `
(function () {
  var EDITABLE_SELECTOR = ${JSON.stringify(EDITABLE_SELECTOR)};
  var STRUCTURAL_SELECTOR = ${JSON.stringify(STRUCTURAL_SELECTOR)};
  var HOVER_CLASS = ${JSON.stringify(HOVER_CLASS)};
  var SELECTED_CLASS = ${JSON.stringify(SELECTED_CLASS)};
  var MSG = ${JSON.stringify(MSG)};
  var SCRIPT_ID = ${JSON.stringify(BOOTSTRAP_SCRIPT_ID)};

  var selectedElement = null;
  var hoveredElement = null;

  function hasDirectText(el) {
    var nodes = el.childNodes;
    for (var i = 0; i < nodes.length; i++) {
      var n = nodes[i];
      if (n.nodeType === Node.TEXT_NODE && n.textContent.trim().length > 0) return true;
    }
    return false;
  }

  // Walks from the actual event target up to the closest selectable element, returning
  // { el, editable } or null. "editable" marks whether the box below should show its text-editing
  // toolbar (true - matches EDITABLE_SELECTOR, or a <div> with direct text of its own) or just a
  // "delete this" option (false - STRUCTURAL_SELECTOR tags, or a <div> with no direct text - a
  // pure layout/flex/grid wrapper). For everything except DIV this is a simple tag-list match.
  // DIVs only qualify on click when the event's own target IS the div itself (mirrors the
  // reference tool's per-element 'e.target === el' guard, so a click on some non-qualifying nested
  // node - e.g. an icon - inside a div doesn't silently select the whole div, text-bearing or
  // not); for hover, a div qualifies as soon as it's reached while walking up, same as the
  // reference tool's div mouseover handler. STRUCTURAL_SELECTOR tags are deliberately NOT given
  // the same click-target guard as DIV - in practice a click inside e.g. a table almost always
  // resolves to an inner <td>/<th> (already in EDITABLE_SELECTOR, checked first) long before the
  // walk ever reaches the <table> itself, so the extra restriction wouldn't meaningfully change
  // behaviour there and keeping it consistent with EDITABLE_SELECTOR's own unguarded matching is
  // simpler.
  function findSelectable(target, isClick) {
    var el = target;
    while (el && el.nodeType === 1) {
      if (el.matches && el.matches(EDITABLE_SELECTOR)) return { el: el, editable: true };
      if (el.tagName === 'DIV') {
        if (!isClick || el === target) return { el: el, editable: hasDirectText(el) };
      } else if (el.matches && el.matches(STRUCTURAL_SELECTOR)) {
        return { el: el, editable: false };
      }
      el = el.parentElement;
    }
    return null;
  }

  function clearHover() {
    if (hoveredElement) {
      hoveredElement.classList.remove(HOVER_CLASS);
      hoveredElement = null;
    }
  }

  document.addEventListener('mouseover', function (e) {
    var found = findSelectable(e.target, false);
    var target = found ? found.el : null;
    if (target === hoveredElement) return;
    clearHover();
    if (target && target !== selectedElement) {
      target.classList.add(HOVER_CLASS);
      hoveredElement = target;
    }
  });

  document.addEventListener('mouseout', function (e) {
    if (!e.relatedTarget || !document.documentElement.contains(e.relatedTarget)) {
      clearHover();
    }
  });

  document.addEventListener('click', function (e) {
    var found = findSelectable(e.target, true);
    if (!found) return;
    e.preventDefault();
    e.stopPropagation();
    selectElement(found.el, found.editable);
  });

  function computePath(el) {
    var path = [];
    var node = el;
    while (node && node !== document.body) {
      var parent = node.parentElement;
      if (!parent) break;
      var idx = Array.prototype.indexOf.call(parent.children, node);
      path.unshift(idx);
      node = parent;
    }
    return path;
  }

  function resolvePath(path) {
    var node = document.body;
    for (var i = 0; i < path.length; i++) {
      var idx = path[i];
      if (!node || !node.children || idx < 0 || idx >= node.children.length) return null;
      node = node.children[idx];
    }
    return node;
  }

  function selectElement(el, editable) {
    if (selectedElement) selectedElement.classList.remove(SELECTED_CLASS);
    clearHover();
    selectedElement = el;
    el.classList.remove(HOVER_CLASS);
    el.classList.add(SELECTED_CLASS);
    post({
      type: 'selected',
      path: computePath(el),
      tag: el.tagName.toLowerCase(),
      html: el.innerHTML,
      editable: editable,
      // Only meaningful when tag is 'a' - lets the panel offer "edit this link's URL" when the
      // SELECTED element is itself a link, not just when a link happens to be nested inside a
      // larger selected block (see insertLink() on the other side of this message).
      href: el.tagName === 'A' ? el.getAttribute('href') : null,
    });
  }

  function extractCleanBodyHtml() {
    // Only document.body is cloned/returned - the injected <style id="STYLE_ID"> lives in <head>
    // and is never part of this clone in the first place. The <script id="SCRIPT_ID">, however,
    // was appended inside <body> (see buildFramedDocument) and does need stripping here.
    var clone = document.body.cloneNode(true);
    var script = clone.querySelector('#' + SCRIPT_ID);
    if (script) script.remove();
    // Query every element that currently HAS a class attribute, not just ones still carrying our
    // marker classes: classList.remove() (used by selectElement()/clearHover() as selection moves
    // around) leaves a now-empty class="" attribute behind rather than removing it, so an element
    // that was hovered/selected earlier and has since been deselected would otherwise slip past a
    // narrower '.HOVER_CLASS, .SELECTED_CLASS' query (confirmed empirically while building this -
    // it was leaving stray class="" attributes on previously-selected elements in the saved HTML).
    var withClass = clone.querySelectorAll('[class]');
    for (var i = 0; i < withClass.length; i++) {
      var n = withClass[i];
      n.classList.remove(HOVER_CLASS, SELECTED_CLASS);
      if (n.classList.length === 0) n.removeAttribute('class');
    }
    return clone.innerHTML;
  }

  window.addEventListener('message', function (event) {
    var data = event.data;
    if (!data || data.__ns !== MSG || event.source !== window.parent) return;

    if (data.type === 'apply') {
      var target = resolvePath(data.path);
      var ok = false;
      if (target) {
        // Swapping innerHTML only replaces target's descendants, so the selectedElement
        // reference (and its SELECTED_CLASS) stays valid whether or not target is the
        // currently selected element - no re-selection bookkeeping needed here.
        if (data.html !== undefined) target.innerHTML = data.html;
        // attrs is how insertLink() edits the SELECTED element's own href when the selected
        // element is itself a link - that's an attribute on target, not something expressible
        // via an innerHTML swap of its contents.
        if (data.attrs) {
          for (var attrName in data.attrs) {
            var attrValue = data.attrs[attrName];
            if (attrValue === null) target.removeAttribute(attrName);
            else target.setAttribute(attrName, attrValue);
          }
        }
        ok = true;
      }
      post({ type: 'applied', requestId: data.requestId, ok: ok });
    } else if (data.type === 'delete') {
      var delTarget = resolvePath(data.path);
      if (!delTarget || !delTarget.parentElement) {
        post({ type: 'deleted', requestId: data.requestId, ok: false });
      } else {
        var delParent = delTarget.parentElement;
        // Captured BEFORE removal - enough to reinsert an identical copy at the same position
        // later (restore doesn't reuse the original node, just its outerHTML - simpler than
        // trying to keep a detached node reference alive across an arbitrary number of other
        // edits that might happen before Undo is eventually clicked).
        var delParentPath = computePath(delParent);
        var delIndex = Array.prototype.indexOf.call(delParent.children, delTarget);
        var delOuterHtml = delTarget.outerHTML;
        if (selectedElement === delTarget) selectedElement = null;
        delTarget.remove();
        post({ type: 'deleted', requestId: data.requestId, ok: true, parentPath: delParentPath, index: delIndex, outerHtml: delOuterHtml });
      }
    } else if (data.type === 'restore') {
      var restoreParent = resolvePath(data.parentPath);
      var restoreOk = false;
      if (restoreParent) {
        var refNode = restoreParent.children[data.index] || null;
        var wrapper = document.createElement('div');
        wrapper.innerHTML = data.outerHtml;
        var restoredEl = wrapper.firstElementChild;
        if (restoredEl) {
          restoreParent.insertBefore(restoredEl, refNode);
          restoreOk = true;
        }
      }
      post({ type: 'restored', requestId: data.requestId, ok: restoreOk });
    } else if (data.type === 'getHtml') {
      post({ type: 'html', requestId: data.requestId, html: extractCleanBodyHtml() });
    } else if (data.type === 'print') {
      // window.print() here is this frame's OWN window - printing the live preview document
      // itself, not the parent page. No response is posted back: this call blocks this frame's
      // script execution until the user dismisses the print UI (confirmed empirically), so there
      // is nothing to usefully await from the parent side anyway - see printDocument() below.
      window.print();
    }
  });

  function post(message) {
    message.__ns = MSG;
    window.parent.postMessage(message, '*');
  }

  post({ type: 'ready' });
})();
`;

// ---------------------------------------------------------------------------------------------
// Parent <-> frame messaging
// ---------------------------------------------------------------------------------------------

function handleMessage(id, state, event) {
    const data = event.data;
    if (!data || data.__ns !== MSG || !state.iframe || event.source !== state.iframe.contentWindow) return;

    switch (data.type) {
        case 'ready':
            if (state.readyResolve) { state.readyResolve(); state.readyResolve = null; }
            break;
        case 'selected':
            onElementSelected(state, data);
            break;
        case 'applied':
        case 'html':
        case 'deleted':
        case 'restored':
            resolvePending(state, data);
            break;
    }
}

function resolvePending(state, data) {
    const entry = state.pending.get(data.requestId);
    if (!entry) return;
    state.pending.delete(data.requestId);
    entry.resolve(data);
}

function requestFromFrame(state, type, extra, timeoutMs) {
    return new Promise((resolve, reject) => {
        const requestId = state.nextRequestId++;
        const timer = setTimeout(() => {
            state.pending.delete(requestId);
            reject(new Error(`structuredEditor: "${type}" request timed out`));
        }, timeoutMs || 3000);

        state.pending.set(requestId, {
            resolve: (data) => {
                clearTimeout(timer);
                if (type === 'getHtml') state.lastKnownHtml = data.html;
                resolve(type === 'getHtml' ? data.html : data);
            },
        });

        state.iframe.contentWindow.postMessage({ __ns: MSG, type, requestId, ...extra }, '*');
    });
}

// ---------------------------------------------------------------------------------------------
// Side panel (lives in the parent document - not sandboxed, same trust level as the TinyMCE
// editor it replaces; HtmlContentSanitizer is still applied server-side to whatever ultimately
// gets saved, exactly as before).
// ---------------------------------------------------------------------------------------------

function buildPanel(state) {
    const root = document.createElement('div');
    root.className = 'structured-editor-root';
    root.innerHTML = `
        <div class="structured-editor-preview-pane">
            <div class="structured-editor-pane-label">Live preview - click any text to edit it</div>
            <div class="structured-editor-preview-host"></div>
        </div>
        <div class="structured-editor-side-pane">
            <div class="structured-editor-pane-label">Selected element</div>
            <div class="structured-editor-empty" data-role="empty">
                <p>Click any text element in the preview to edit it.</p>
                <p class="structured-editor-hint">Only that element's own content changes - the rest of the document's layout is left alone.</p>
            </div>
            <div class="structured-editor-edit" data-role="edit" hidden>
                <div class="structured-editor-selected-tag" data-role="tag"></div>
                <div data-role="text-edit-section">
                    <div class="structured-editor-toolbar" role="toolbar" aria-label="Formatting">
                        <button type="button" data-cmd="bold" title="Bold"><b>B</b></button>
                        <button type="button" data-cmd="italic" title="Italic"><i>I</i></button>
                        <button type="button" data-cmd="underline" title="Underline"><u>U</u></button>
                        <span class="structured-editor-toolbar-sep"></span>
                        <button type="button" data-cmd="insertUnorderedList" title="Bullet list">&bull; List</button>
                        <button type="button" data-cmd="insertOrderedList" title="Numbered list">1. List</button>
                        <span class="structured-editor-toolbar-sep"></span>
                        <button type="button" data-action="link" title="Insert link">Link</button>
                        <button type="button" data-cmd="removeFormat" title="Remove formatting">Clear</button>
                    </div>
                    <div class="structured-editor-box" data-role="box" contenteditable="true"></div>
                    <div class="structured-editor-actions">
                        <button type="button" class="btn btn-primary btn-sm" data-action="apply">Apply change</button>
                        <button type="button" class="btn btn-outline-secondary btn-sm" data-action="revert">Revert</button>
                    </div>
                </div>
                <p data-role="structural-hint" class="structured-editor-hint" hidden>This element has no text of its own to edit here - e.g. a table, a list, or a layout container. You can still delete it as a whole below.</p>
                <div class="structured-editor-actions">
                    <button type="button" class="btn btn-outline-danger btn-sm" data-action="delete">Delete element</button>
                </div>
            </div>
            <div class="structured-editor-footer">
                <button type="button" class="btn btn-outline-secondary btn-sm" data-action="undo" disabled>Undo last change</button>
                <span class="structured-editor-status" data-role="status"></span>
            </div>
        </div>
    `;
    state.container.innerHTML = '';
    state.container.appendChild(root);

    state.panel = {
        root,
        previewHost: root.querySelector('.structured-editor-preview-host'),
        empty: root.querySelector('[data-role="empty"]'),
        edit: root.querySelector('[data-role="edit"]'),
        tag: root.querySelector('[data-role="tag"]'),
        textEditSection: root.querySelector('[data-role="text-edit-section"]'),
        structuralHint: root.querySelector('[data-role="structural-hint"]'),
        box: root.querySelector('[data-role="box"]'),
        undoBtn: root.querySelector('[data-action="undo"]'),
        status: root.querySelector('[data-role="status"]'),
    };

    root.querySelectorAll('[data-cmd]').forEach((btn) => {
        btn.addEventListener('mousedown', (e) => e.preventDefault());
        btn.addEventListener('click', () => execCmd(state, btn.getAttribute('data-cmd')));
    });
    root.querySelector('[data-action="link"]').addEventListener('mousedown', (e) => e.preventDefault());
    root.querySelector('[data-action="link"]').addEventListener('click', () => insertLink(state));
    root.querySelector('[data-action="apply"]').addEventListener('click', () => applyChange(state));
    root.querySelector('[data-action="revert"]').addEventListener('click', () => revertChange(state));
    root.querySelector('[data-action="delete"]').addEventListener('click', () => deleteElement(state));
    state.panel.undoBtn.addEventListener('click', () => undoLastChange(state));

    state.panel.box.addEventListener('paste', (e) => handlePaste(state, e));
}

function onElementSelected(state, data) {
    state.selectedPath = data.path;
    state.originalHtml = data.html;
    state.selectedTag = data.tag;
    state.selectedHref = data.href;
    state.selectedEditable = data.editable;

    state.panel.empty.hidden = true;
    state.panel.edit.hidden = false;
    state.panel.tag.textContent = `<${data.tag}>`;
    state.panel.textEditSection.hidden = !data.editable;
    state.panel.structuralHint.hidden = data.editable;

    if (data.editable) {
        state.panel.box.innerHTML = data.html;
        normalizeLists(state.panel.box);
        state.panel.box.focus();
        setStatus(state, `Editing <${data.tag}>`);
    } else {
        // No text-editing surface shown for this one (see textEditSection above) - nothing to
        // seed into the box, Delete is the only available action.
        setStatus(state, `Selected <${data.tag}> - no text of its own to edit`);
    }
}

function showNoSelection(state) {
    state.panel.empty.hidden = false;
    state.panel.edit.hidden = true;
}

function execCmd(state, command) {
    state.panel.box.focus();
    document.execCommand(command, false, null);
    if (command === 'insertUnorderedList' || command === 'insertOrderedList') {
        normalizeLists(state.panel.box);
    }
}

// execCommand's list insertion leaves list styling entirely up to whatever CSS the destination
// page happens to define (often none, once it lands inside this app's own DOM) - reapplying
// explicit inline styles here is what keeps a bullet/numbered list actually looking like one
// regardless of the surrounding document's own styles. Ported directly from the reference tool.
function normalizeLists(container) {
    container.querySelectorAll('ul, ol').forEach((list) => {
        list.style.margin = '0 0 8px 0';
        list.style.paddingLeft = '24px';
        list.style.listStylePosition = 'outside';
        list.style.listStyleType = list.tagName.toLowerCase() === 'ol' ? 'decimal' : 'disc';
    });
    container.querySelectorAll('li').forEach((item) => {
        item.style.margin = '2px 0';
        item.style.removeProperty('list-style');
        item.style.removeProperty('list-style-type');
    });
}

// document.execCommand('createLink', ...) always wraps a NEW <a> around the selection - it never
// edits an existing one, even when the selection/cursor is already inside a link. Confirmed
// empirically: using it on already-linked text nests a second <a> inside the first rather than
// changing the original's href, which is why editing an existing link never worked as expected.
// Detecting the enclosing <a> first and setting its href directly sidesteps execCommand entirely
// for that case, rather than trying to work around its behaviour after the fact.
function findAncestorLink(node, boundary) {
    while (node && node !== boundary) {
        if (node.nodeType === Node.ELEMENT_NODE && node.tagName === 'A') return node;
        node = node.parentNode;
    }
    return null;
}

// Two distinct "editing an existing link" cases, confirmed empirically to both occur in practice
// (not just theoretically): (1) a link nested INSIDE a larger selected block - e.g. a <p> with
// some linked text among plain text - where the cursor/selection sits inside the <a> within the
// edit box's own content; and (2) the SELECTED element itself IS the link (the user clicked
// directly on a link, which qualifies as its own selectable element same as a <p> or <li> would).
// Case 2 needed real diagnosis: the edit box only ever holds the selected element's INNER content
// (el.innerHTML, never el itself), so a link selected this way has no <a> ancestor anywhere
// inside the box at all - case 1's DOM search correctly finds nothing, because there's genuinely
// nothing to find there. The href being edited in that case lives on the selected element itself,
// which is why it needs its own path via the 'apply' message's `attrs`, not a change to the box's
// innerHTML that a later "Apply change" click would pick up.
function insertLink(state) {
    // Read the selection BEFORE focus() - calling focus() on an element that doesn't already have
    // true DOM focus can reset the cursor to a default position in some browsers, which would make
    // this look at the wrong place regardless of where the user actually clicked.
    const selection = window.getSelection();
    const nestedLink = selection && selection.rangeCount > 0
        ? findAncestorLink(selection.getRangeAt(0).commonAncestorContainer, state.panel.box)
        : null;
    state.panel.box.focus();

    if (nestedLink) {
        const url = window.prompt('Edit the URL for this link:', nestedLink.getAttribute('href') || '');
        if (url) nestedLink.setAttribute('href', url);
        return;
    }

    if (state.selectedTag === 'a') {
        editSelectedElementLink(state);
        return;
    }

    const url = window.prompt('Enter the URL for this link:', 'https://');
    if (url) document.execCommand('createLink', false, url);
}

async function editSelectedElementLink(state) {
    const url = window.prompt('Edit the URL for this link:', state.selectedHref || '');
    if (!url || url === state.selectedHref) return;

    const result = await requestFromFrame(state, 'apply', { path: state.selectedPath, attrs: { href: url } });
    if (!result.ok) {
        setStatus(state, 'Could not update the link - the element could not be located.');
        return;
    }
    state.selectedHref = url;
    setStatus(state, 'Link URL updated.');
}

// ---------------------------------------------------------------------------------------------
// Paste sanitization - ported from the reference tool's sanitizePastedHTML. contenteditable can
// carry arbitrary HTML in from the clipboard regardless of what the surrounding page allows, so
// this is a separate guard from (not a substitute for) HtmlContentSanitizer.Sanitize(), which is
// still applied server-side to whatever the Save button ultimately submits.
// ---------------------------------------------------------------------------------------------

const PASTE_ALLOWED_TAGS = new Set(['B', 'STRONG', 'I', 'EM', 'U', 'UL', 'OL', 'LI', 'A', 'BR', 'P']);
const PASTE_REMOVE_ENTIRELY = new Set(['SCRIPT', 'STYLE', 'META', 'LINK', 'IMG', 'SVG', 'HEAD', 'TITLE', 'O:P']);

function handlePaste(state, e) {
    e.preventDefault();
    const clipboardData = e.clipboardData || window.clipboardData;
    const html = clipboardData.getData('text/html');
    let insertHtml;

    if (html) {
        insertHtml = sanitizePastedHtml(html);
    } else {
        const text = clipboardData.getData('text/plain') || '';
        insertHtml = escapeHtml(text).replace(/\n/g, '<br>');
    }

    document.execCommand('insertHTML', false, insertHtml);
    normalizeLists(state.panel.box);
}

function sanitizePastedHtml(html) {
    const container = document.createElement('div');
    container.innerHTML = html;
    cleanPastedNode(container);
    return container.innerHTML;
}

function cleanPastedNode(node) {
    Array.from(node.childNodes).forEach((child) => {
        if (child.nodeType === Node.ELEMENT_NODE) {
            const tag = child.tagName;

            if (PASTE_REMOVE_ENTIRELY.has(tag)) {
                child.remove();
                return;
            }

            cleanPastedNode(child);

            if (!PASTE_ALLOWED_TAGS.has(tag)) {
                while (child.firstChild) node.insertBefore(child.firstChild, child);
                node.removeChild(child);
            } else {
                Array.from(child.attributes).forEach((attr) => {
                    if (tag === 'A' && attr.name === 'href') return;
                    child.removeAttribute(attr.name);
                });
            }
        } else if (child.nodeType !== Node.TEXT_NODE) {
            child.remove();
        }
    });
}

function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

// ---------------------------------------------------------------------------------------------
// Apply / Revert / Undo
// ---------------------------------------------------------------------------------------------

async function applyChange(state) {
    // The Apply button lives inside textEditSection, hidden entirely for a structural (non-
    // editable) selection - this guard is defensive backstop only, not the primary gate.
    if (!state.selectedPath || !state.selectedEditable) return;

    normalizeLists(state.panel.box);
    const newHtml = state.panel.box.innerHTML;
    if (newHtml === state.originalHtml) return;

    const path = state.selectedPath;
    const tag = state.panel.tag.textContent;
    const oldHtml = state.originalHtml;

    const result = await requestFromFrame(state, 'apply', { path, html: newHtml });
    if (!result.ok) {
        setStatus(state, 'Could not apply change - the element could not be located.');
        return;
    }

    state.undoStack.push({ type: 'edit', path, tag, oldHtml, newHtml });
    updateUndoButton(state);
    state.originalHtml = newHtml;
    setStatus(state, `Change applied to ${tag} (${state.undoStack.length} change${state.undoStack.length === 1 ? '' : 's'} so far)`);

    // Keep the cached fragment fresh so a Save immediately after Apply has an up to date fallback
    // even if a later getHtml round trip were ever to time out.
    requestFromFrame(state, 'getHtml', {}).catch(() => { /* best-effort cache refresh only */ });
}

function revertChange(state) {
    if (!state.selectedPath || !state.selectedEditable) return;
    state.panel.box.innerHTML = state.originalHtml;
}

/// <summary>
/// Removes the whole selected element from the document, not just its content - a genuinely
/// different operation from Apply (which only ever swaps an element's innerHTML), so it needs its
/// own 'delete'/'restore' postMessage pair rather than reusing 'apply'. Undoable like everything
/// else here: the frame captures the element's parent path, sibling index, and full outerHTML
/// before removing it, enough to reinsert it at exactly the same position later. Confirming first
/// since, unlike a text edit sitting in the box waiting for Apply, this takes effect immediately.
/// </summary>
async function deleteElement(state) {
    if (!state.selectedPath) return;
    if (!window.confirm(`Delete this <${state.selectedTag}> element? This can be undone from "Undo last change".`)) return;

    const path = state.selectedPath;
    const tag = state.selectedTag;

    const result = await requestFromFrame(state, 'delete', { path });
    if (!result.ok) {
        setStatus(state, 'Could not delete - the element could not be located.');
        return;
    }

    state.undoStack.push({ type: 'delete', tag, parentPath: result.parentPath, index: result.index, outerHtml: result.outerHtml });
    updateUndoButton(state);

    // Nothing sensible to keep selected - the element the panel was editing no longer exists.
    state.selectedPath = null;
    state.selectedTag = null;
    state.selectedHref = null;
    state.selectedEditable = null;
    showNoSelection(state);
    setStatus(state, `Deleted <${tag}> (${state.undoStack.length} change${state.undoStack.length === 1 ? '' : 's'} so far)`);

    requestFromFrame(state, 'getHtml', {}).catch(() => { /* best-effort cache refresh only */ });
}

/// <summary>
/// A real undo stack, not the reference tool's own "reload the original document and drop every
/// other change" approach (its own comment flags that as a known weak point). Undo here pops just
/// the most recent entry and asks the frame to restore that ONE element's prior HTML by path -
/// every earlier applied change to every other element is left completely untouched.
/// </summary>
async function undoLastChange(state) {
    if (state.undoStack.length === 0) return;

    const entry = state.undoStack.pop();
    updateUndoButton(state);

    if (entry.type === 'delete') {
        const result = await requestFromFrame(state, 'restore', {
            parentPath: entry.parentPath, index: entry.index, outerHtml: entry.outerHtml,
        });
        if (!result.ok) {
            setStatus(state, 'Could not undo the delete - the original parent could not be located.');
            return;
        }
        setStatus(state, `Restored deleted <${entry.tag}> (${state.undoStack.length} change${state.undoStack.length === 1 ? '' : 's'} remaining)`);
        requestFromFrame(state, 'getHtml', {}).catch(() => { /* best-effort cache refresh only */ });
        return;
    }

    const result = await requestFromFrame(state, 'apply', { path: entry.path, html: entry.oldHtml });
    if (!result.ok) {
        setStatus(state, 'Could not undo - the element could not be located.');
        return;
    }

    // If the element that was just undone is still the one selected, refresh the edit box so
    // Revert (and the "unchanged, so Apply is a no-op" check) reflect the restored content rather
    // than the change that was just undone.
    if (state.selectedPath && pathsEqual(state.selectedPath, entry.path)) {
        state.originalHtml = entry.oldHtml;
        state.panel.box.innerHTML = entry.oldHtml;
    }

    setStatus(state, `Undid last change to ${entry.tag} (${state.undoStack.length} change${state.undoStack.length === 1 ? '' : 's'} remaining)`);
    requestFromFrame(state, 'getHtml', {}).catch(() => { /* best-effort cache refresh only */ });
}

function pathsEqual(a, b) {
    return a.length === b.length && a.every((v, i) => v === b[i]);
}

function updateUndoButton(state) {
    state.panel.undoBtn.disabled = state.undoStack.length === 0;
}

function setStatus(state, text) {
    state.panel.status.textContent = text;
}

// ---------------------------------------------------------------------------------------------
// Shared styling - injected once into the host page's <head>. The panel markup above is built by
// this module (not rendered by DocumentEditor.razor's own markup), so it falls outside Blazor's
// CSS-isolation scope the same way TinyMCE's own generated UI does (see DocumentEditor.razor.css) -
// this is this widget's equivalent of TinyMCE bringing its own skin stylesheet.
// ---------------------------------------------------------------------------------------------

function injectSharedStyles() {
    if (document.getElementById('structured-editor-shared-styles')) return;

    const style = document.createElement('style');
    style.id = 'structured-editor-shared-styles';
    style.textContent = `
.structured-editor-root {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(280px, 360px);
    gap: 0;
    border: 1px solid #dee2e6;
    border-radius: 0.375rem;
    overflow: hidden;
    height: 600px;
}
.structured-editor-preview-pane, .structured-editor-side-pane {
    display: flex;
    flex-direction: column;
    min-height: 0;
}
.structured-editor-preview-pane {
    border-right: 1px solid #dee2e6;
}
.structured-editor-pane-label {
    background-color: #f8f9fa;
    border-bottom: 1px solid #dee2e6;
    padding: 0.5rem 0.75rem;
    font-weight: 600;
    font-size: 0.8rem;
    color: #565f90;
}
.structured-editor-preview-host {
    flex: 1;
    min-height: 0;
    background-color: #fff;
}
.structured-editor-frame {
    width: 100%;
    height: 100%;
    border: none;
    display: block;
}
.structured-editor-side-pane {
    background-color: #fff;
}
.structured-editor-empty, .structured-editor-edit {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    padding: 0.9rem;
    overflow-y: auto;
}
.structured-editor-empty {
    color: #6c757d;
    justify-content: center;
    text-align: center;
    align-items: center;
}
.structured-editor-empty .structured-editor-hint {
    font-size: 0.8rem;
    color: #adb5bd;
}
.structured-editor-edit .structured-editor-hint {
    font-size: 0.82rem;
    color: #6c757d;
    margin: 0.3rem 0 0.6rem;
}
.structured-editor-selected-tag {
    font-family: Consolas, "Courier New", monospace;
    font-size: 0.8rem;
    color: #00927e;
    margin-bottom: 0.4rem;
}
.structured-editor-toolbar {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    flex-wrap: wrap;
    background-color: #f0f0f0;
    border: 1px solid #ccc;
    border-bottom: none;
    border-radius: 0.25rem 0.25rem 0 0;
    padding: 0.35rem 0.4rem;
}
.structured-editor-toolbar button {
    background-color: white;
    border: 1px solid #ccc;
    border-radius: 3px;
    padding: 0.25rem 0.55rem;
    font-size: 0.8rem;
    cursor: pointer;
    color: #333;
}
.structured-editor-toolbar button:hover {
    background-color: #ede9fb;
    border-color: #7c76b7;
}
.structured-editor-toolbar-sep {
    width: 1px;
    height: 18px;
    background-color: #ccc;
    margin: 0 0.2rem;
}
.structured-editor-box {
    flex: 1;
    min-height: 140px;
    border: 1px solid #ccc;
    border-top: none;
    border-radius: 0 0 0.25rem 0.25rem;
    padding: 0.6rem;
    font-size: 0.9rem;
    line-height: 1.5;
    overflow-y: auto;
    background-color: #fff;
}
.structured-editor-box:focus {
    outline: none;
    border-color: #7c76b7;
    box-shadow: 0 0 0 0.2rem rgba(124, 118, 183, 0.15);
}
.structured-editor-box ul, .structured-editor-box ol {
    margin: 0 0 8px 0;
    padding-left: 24px;
}
.structured-editor-box a {
    color: #00927e;
}
.structured-editor-actions {
    margin-top: 0.6rem;
    display: flex;
    gap: 0.5rem;
}
.structured-editor-footer {
    border-top: 1px solid #dee2e6;
    padding: 0.5rem 0.9rem;
    display: flex;
    align-items: center;
    gap: 0.6rem;
    font-size: 0.8rem;
    color: #6c757d;
}
`;
    document.head.appendChild(style);
}
