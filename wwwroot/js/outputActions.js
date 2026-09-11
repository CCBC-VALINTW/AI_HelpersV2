// Ported from V1's GovService form, which wrote a ClipboardItem carrying BOTH text/html and
// text/plain (its copyToClip function) - the HTML flavour is the whole point: it's what makes a
// paste into Word, Outlook or Teams keep the document's headings, tables and stylesheet formatting
// instead of arriving as unformatted text. Callers pass the same fully-styled document the preview
// iframe renders, so what lands in the paste matches what was on screen.
//
// Returns which flavour actually reached the clipboard rather than a bare true/false: write() with
// a ClipboardItem needs a secure context and reasonably current browser support (Firefox only
// gained it in 127), and a silent downgrade to plain text drops every bit of formatting this
// button exists to preserve - better to say so than let someone find out after pasting.
export async function copyToClipboard(html, text) {
    const { html: pasteHtml, converted, failed } = await rasterizeSvgs(html);

    if (navigator.clipboard && typeof ClipboardItem !== 'undefined') {
        try {
            await navigator.clipboard.write([new ClipboardItem({
                'text/html': new Blob([pasteHtml], { type: 'text/html' }),
                'text/plain': new Blob([text], { type: 'text/plain' })
            })]);
            return { flavour: 'rich', svgsConverted: converted, svgsFailed: failed };
        } catch (err) {
            console.error('Rich clipboard write failed, trying plain text:', err);
        }
    }

    try {
        await navigator.clipboard.writeText(text);
        return { flavour: 'plain', svgsConverted: 0, svgsFailed: 0 };
    } catch (err) {
        console.error('Clipboard write failed:', err);
        return { flavour: 'failed', svgsConverted: 0, svgsFailed: 0 };
    }
}

// Word reads an <img>'s width/height attributes as CSS pixels and lays them out at 96 DPI, so a
// chart declaring viewBox="0 0 1200 600" arrives 12.5 inches wide. An A4 portrait page with the
// default 2.54cm margins only has about 6.3 inches of text width, so anything past ~600px overspills
// the page - which is exactly what happened on real dashboard output. These cap the size the <img>
// ASKS to be displayed at; the aspect ratio is preserved and a graphic already smaller than the cap
// is never scaled up.
const MAX_PASTE_WIDTH_PX = 600;
const MAX_PASTE_HEIGHT_PX = 900;

// Rasterised at 2x the size it will be DISPLAYED at (not 2x the SVG's own coordinate system) - keeps
// it crisp when Word scales it for print without paying for pixels no one sees: a 1200-wide chart
// shown at 600 needs 1200 real pixels, not 2400. Capped per side so an absurd viewBox can't turn
// into a multi-megabyte base64 string on the clipboard.
const SVG_RASTER_SCALE = 2;
const SVG_RASTER_MAX_PX = 2400;

/// Word (and Outlook) simply ignore inline <svg> markup, whether it arrives via a paste or via the
/// docx converter - so a Helper's charts, which on real data are almost always inline SVG with no
/// <canvas> or <img> anywhere, vanish while all the surrounding text survives. Swapping each one for
/// a PNG <img> first is what makes them come across. Used by the clipboard copy and by the Word
/// export, which has to rasterise here in the browser because there's no canvas server-side.
///
/// Runs entirely against an inert DOMParser document, and each SVG is rendered by loading it into an
/// Image as image/svg+xml - a mode in which browsers refuse to run scripts or fetch external
/// resources inside the SVG at all. So model-generated markup is never inserted into this app's live
/// DOM (the thing the sandboxed output iframes exist to prevent) and the canvas never gets tainted.
///
/// A failure converting one graphic leaves that graphic's original SVG untouched rather than
/// abandoning the whole operation - a document that arrives with one chart missing beats one that
/// doesn't arrive at all - and is counted so the caller can say so out loud.
///
/// extraCss carries styling the markup itself doesn't include: the clipboard passes a whole styled
/// document whose own <style> blocks are found below, but the export passes a bare body fragment
/// with its stylesheet held separately, and without it a chart whose colours come from the
/// stylesheet would rasterise unstyled.
export async function rasterizeSvgs(html, extraCss = '') {
    let doc;
    try {
        doc = new DOMParser().parseFromString(html, 'text/html');
    } catch (err) {
        console.error('Could not parse output for SVG conversion, using it as-is:', err);
        return { html, converted: 0, failed: 0 };
    }

    // Only outermost SVGs - a nested one is rasterised as part of its parent, and replacing the
    // parent first would leave the inner node detached anyway.
    const svgs = [...doc.querySelectorAll('svg')].filter(svg => !svg.parentElement?.closest('svg'));
    if (svgs.length === 0) {
        return { html, converted: 0, failed: 0 };
    }

    // On screen the SVG sits inside .rendDoc and the selected stylesheet applies to it. Loaded as a
    // standalone image it inherits nothing from this document, so any styling the stylesheet was
    // providing (text colour, fonts, stroke widths) would silently disappear from the raster. Both
    // halves of the fix live in rasterizeSvgElement: the document's own <style> blocks get copied
    // into the SVG, and the SVG root is given the .rendDoc class so descendant selectors still match.
    const documentCss = [...doc.querySelectorAll('style')].map(s => s.textContent).concat(extraCss).join('\n');

    let converted = 0;
    let failed = 0;

    for (const svg of svgs) {
        try {
            svg.replaceWith(await rasterizeSvgElement(svg, documentCss, doc));
            converted++;
        } catch (err) {
            console.error('Could not convert an SVG to an image, leaving it as-is:', err);
            failed++;
        }
    }

    // Hands back the same shape it was given: a whole document stays a whole document (the
    // clipboard's HTML flavour needs its <head> and <style>), while a body fragment comes back as a
    // fragment - the export pipeline re-wraps what it receives, and returning <html><body> markup
    // there would nest one document inside another.
    if (/^\s*<(!doctype|html)[\s>]/i.test(html)) {
        return { html: (doc.doctype ? '<!DOCTYPE html>' : '') + doc.documentElement.outerHTML, converted, failed };
    }
    return { html: doc.body.innerHTML, converted, failed };
}

/// POSTs HTML to a server export endpoint and saves whatever file comes back.
///
/// The Word export can't be a plain link or navigation like the HTML one: the markup being converted
/// has to be rasterised in the browser first (see rasterizeSvgs), so it has to travel in a request
/// body rather than being read from the database server-side. Downloads via a blob URL and a
/// synthetic click, which is how a fetch response becomes a saved file.
export async function postHtmlForDownload(url, html, fileName) {
    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ html })
    });

    if (!response.ok) {
        console.error('Export request failed:', response.status, await response.text().catch(() => ''));
        return false;
    }

    const objectUrl = URL.createObjectURL(await response.blob());
    try {
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        return true;
    } finally {
        // Deferred - revoking synchronously can cancel the download the click just started.
        setTimeout(() => URL.revokeObjectURL(objectUrl), 30000);
    }
}

async function rasterizeSvgElement(svg, documentCss, doc) {
    const intrinsic = svgPixelSize(svg);
    const display = fitWithinPastePage(intrinsic);

    const rasterScale = Math.min(SVG_RASTER_SCALE, SVG_RASTER_MAX_PX / Math.max(display.width, display.height));
    const pixelWidth = Math.max(1, Math.round(display.width * rasterScale));
    const pixelHeight = Math.max(1, Math.round(display.height * rasterScale));

    const clone = svg.cloneNode(true);
    // Serialised standalone, so it needs the namespace and a real intrinsic size in its own right -
    // an SVG sized only by CSS (width: 100%) has neither once it's a separate image, and would
    // rasterise at the browser's default 300x150 replaced-element size instead.
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    // A viewBox is what maps the drawing's own coordinates onto whatever width/height it's given, so
    // setting the size below scales the CONTENT. Without one, changing width/height just changes how
    // much of the canvas is visible - the drawing would be cropped or padded instead of scaled - so
    // one is synthesised from the intrinsic size for the SVGs that don't declare it.
    if (!clone.getAttribute('viewBox')) {
        clone.setAttribute('viewBox', `0 0 ${intrinsic.width} ${intrinsic.height}`);
    }
    // Sized to the final pixel dimensions so the vector is rendered natively at that resolution,
    // rather than rasterised small and then stretched by drawImage.
    clone.setAttribute('width', String(pixelWidth));
    clone.setAttribute('height', String(pixelHeight));
    if (documentCss.trim()) {
        clone.classList.add('rendDoc');
        const style = doc.createElementNS('http://www.w3.org/2000/svg', 'style');
        style.textContent = documentCss;
        clone.insertBefore(style, clone.firstChild);
    }

    const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(clone)], { type: 'image/svg+xml' }));
    try {
        const image = await loadImage(url);
        const canvas = document.createElement('canvas');
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;

        const ctx = canvas.getContext('2d');
        // An SVG has no background of its own. Left transparent, a chart's dark text and axes land on
        // whatever colour the paste target happens to composite onto - black-on-dark in a themed Word
        // document. The preview shows it against the document's white page, so match that.
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(image, 0, 0);

        const img = doc.createElement('img');
        img.setAttribute('src', canvas.toDataURL('image/png'));
        img.setAttribute('width', String(display.width));
        img.setAttribute('height', String(display.height));
        // Word sizes from the attributes above and ignores this, but a paste into anything that does
        // respect CSS (a web editor, a mail client, Teams) then can't overflow a narrower column
        // either. The pair is deliberate - max-width alone would squash the aspect ratio.
        img.setAttribute('style', 'max-width:100%;height:auto');
        img.setAttribute('alt', svgAltText(svg));
        return img;
    } finally {
        URL.revokeObjectURL(url);
    }
}

/// Shrinks a graphic to fit the text area of an A4 portrait page, preserving its aspect ratio.
/// Deliberately one shared scale factor for both dimensions rather than clamping each independently,
/// which would distort anything wider or taller than the caps - and floored at 1 so a small chart is
/// left exactly as it is rather than being blown up to fill the width.
function fitWithinPastePage({ width, height }) {
    const fit = Math.min(1, MAX_PASTE_WIDTH_PX / width, MAX_PASTE_HEIGHT_PX / height);
    return {
        width: Math.max(1, Math.round(width * fit)),
        height: Math.max(1, Math.round(height * fit))
    };
}

function loadImage(url) {
    return new Promise((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve(image);
        image.onerror = () => reject(new Error('the SVG could not be loaded as an image'));
        image.src = url;
    });
}

/// Size has to come from the SVG's own attributes: the parsed document is never rendered, so
/// getBoundingClientRect would report zero for everything. viewBox is the most reliable source for
/// model-generated markup (width/height are often percentages, or absent entirely), and carries the
/// aspect ratio needed when only one of the two dimensions is given.
function svgPixelSize(svg) {
    const absolute = value => {
        if (!value || value.trim().endsWith('%')) return null;
        const parsed = parseFloat(value);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
    };

    let width = absolute(svg.getAttribute('width'));
    let height = absolute(svg.getAttribute('height'));

    const viewBox = (svg.getAttribute('viewBox') || '').split(/[\s,]+/).map(parseFloat).filter(Number.isFinite);
    if (viewBox.length === 4 && viewBox[2] > 0 && viewBox[3] > 0) {
        const [, , boxWidth, boxHeight] = viewBox;
        if (!width && !height) {
            width = boxWidth;
            height = boxHeight;
        } else if (!width) {
            width = height * (boxWidth / boxHeight);
        } else if (!height) {
            height = width * (boxHeight / boxWidth);
        }
    }

    return {
        width: Math.max(1, Math.round(width || 800)),
        height: Math.max(1, Math.round(height || 450))
    };
}

/// Keeps whatever accessible name the SVG already carried - a rasterised chart is opaque to a screen
/// reader, so dropping its <title> would make the pasted document less accessible than the original.
function svgAltText(svg) {
    const title = svg.querySelector('title');
    return (svg.getAttribute('aria-label') || (title ? title.textContent : '') || '').trim();
}

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
