// "Open in new tab" for Helper output that contains its own <script> (e.g. interactive
// dashboards). Uses window.open('', '_blank') + writing content in, not a data: URL navigation -
// confirmed in testing that modern browsers block top-level navigation to data: URLs even from a
// direct click ("Download as HTML" still uses a data: URL, but the `download` attribute makes
// that a save action rather than a navigation, so it isn't affected).
//
// V1's GovService form used this same window.open pattern (proven to reliably avoid popup
// blockers, since it runs synchronously inside the click handler) - but V1 wrote the output
// directly into the new tab's own document, which inherits this app's origin and would give any
// <script> in the output the same access to this app's session as a same-origin page. Reusing
// the sandboxed-iframe pattern instead: the outer tab is just our own trusted shell (one iframe),
// the model's actual output lives inside a sandbox="allow-scripts" iframe - scripts run, but in a
// genuinely opaque origin with no path back to this app. Same isolation guarantee as the embedded
// preview, just in its own tab.
export function openInNewTab(html) {
    const newTab = window.open('', '_blank');
    if (!newTab) {
        alert('Your browser blocked this pop-up. Please allow pop-ups for this site and try again.');
        return;
    }

    newTab.document.title = 'Helper output';
    newTab.document.body.style.margin = '0';

    // Content goes in via the srcdoc attribute, set before the frame loads - NOT via
    // contentWindow.document.write() after the fact. sandbox="allow-scripts" without
    // allow-same-origin makes the frame's content opaque-origin to everyone, including the parent
    // that created it, so reaching into contentWindow.document throws a cross-origin
    // SecurityError even for our own script (confirmed in testing) - that's the sandbox actually
    // working, not a bug to work around. Setting srcdoc is just an attribute on our own
    // same-origin element, so it isn't affected.
    const iframe = newTab.document.createElement('iframe');
    iframe.setAttribute('sandbox', 'allow-scripts');
    iframe.style.cssText = 'width:100%;height:100vh;border:0;display:block';
    iframe.srcdoc = html;
    newTab.document.body.appendChild(iframe);
}
