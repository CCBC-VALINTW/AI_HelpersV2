namespace AiHelpers.Services;

/// <summary>
/// The app's own release history, shared between the About page (which renders the whole list as
/// plain-English release notes) and the nav pane (which shows just the current version, so it's
/// obvious at a glance which release an environment is actually running - dev, tester and
/// production otherwise look identical).
/// </summary>
public static class ReleaseHistory
{
    public sealed record ReleaseNote(string Version, DateTime ReleasedAt, IReadOnlyList<string> Highlights);

    /// <summary>The newest entry - the version the running app reports as its own.</summary>
    public static ReleaseNote Current => Releases[0];

    // Deliberately hand-curated in plain English, not generated from git history - a real commit
    // log ("Fix Data Protection cert generation to force CNG key storage") means nothing to a
    // non-technical reader. Add a new entry at the TOP of this list (newest first) whenever a
    // meaningful batch of user-facing work is ready to call a "release" - there's no build-number
    // or CI tie-in, this is just a summary someone chooses to write, matching what this page asks
    // for. ReleasedAt is a real timestamp (the last commit actually included in that version, at
    // the time this entry was written), not a live clock.
    public static readonly IReadOnlyList<ReleaseNote> Releases =
    [
        new("1.18", new DateTime(2026, 9, 10, 12, 0, 0),
        [
            "The version you're running is now shown at the bottom of the left-hand menu and links to these release notes - so it's clear which release each environment is on, rather than development, tester and live all looking alike.",
            "Removed a scrollbar that appeared on the left-hand menu even when there was nothing to scroll."
        ]),
        new("1.17", new DateTime(2026, 9, 10, 10, 0, 0),
        [
            "Brought back V1's \"Copy to clipboard\" button, on both a Helper's result and the document editor - it copies the document with its formatting, so pasting into Word, Outlook or Teams keeps the headings, tables and styling instead of arriving as plain text. Copying from the document editor takes whatever is currently on screen, including edits you haven't saved yet.",
            "Charts and other graphics in a Helper's output are now converted to pictures as part of that copy, so they come across into Word too - previously they were dropped from the paste entirely, because Word ignores the kind of drawing instructions Helpers produce. They're also scaled to fit within an A4 page's margins rather than arriving oversized and spilling off the edge, and a graphic already smaller than that is left at its own size. Anything a chart's own script draws after the page loads still can't be copied this way; use the PDF export for those.",
            "Fixed the browser tab title getting stuck on whichever page you opened first, so it now follows you as you move around the app. A Helper's page is also titled with the Helper's own name, so a tab, a bookmark, or a link shared with a colleague says which Helper it is - as do a saved run and the Helper editor."
        ]),
        new("1.16", new DateTime(2026, 9, 9, 11, 0, 0),
        [
            "The development environment now visibly marks itself as such - the left-hand navigation pane turns red with a notice explaining it's the active development environment and may be unstable, with a link across to the production site. Never shown outside development."
        ]),
        new("1.15", new DateTime(2026, 9, 9, 9, 30, 0),
        [
            "LLM model costs in Admin are now entered and stored as USD per 1,000,000 tokens instead of per 1,000 - matching how providers publish pricing today. Every existing model's cost was automatically converted to the equivalent per-million rate, so nothing changes about what a run actually costs."
        ]),
        new("1.14", new DateTime(2026, 9, 8, 17, 30, 0),
        [
            "Added a separate \"example output document\" upload next to the Output format field - upload an example of the finished document you want and the AI will describe its layout (which sections appear, in what order, and what data belongs in each), which is then sent alongside your own Output format instruction on every run to make output match that layout more consistently. Your Output format text is never overwritten, and the example document itself is only read once during analysis, never sent on a real run.",
            "Added an opt-in \"Optimise for token usage\" switch for Knowledge documents - instead of sending the full document on every run, a one-off AI pass distils it into a condensed version that's used instead, cutting the ongoing cost for larger reference documents. Off by default; nothing changes for a Helper until its owner turns it on."
        ]),
        new("1.13", new DateTime(2026, 9, 8, 11, 0, 0),
        [
            "Added an \"Analyze for efficiency\" button on a Data Source's Test query results, once its estimated token cost is large enough to be worth checking - asks the AI for specific suggestions on cutting how much data it needs to send (e.g. summarising instead of listing every row) without losing what a report built from it would actually need. A real, billed AI call, so it only ever runs when you click it."
        ]),
        new("1.12", new DateTime(2026, 9, 8, 9, 30, 0),
        [
            "Corrected the token-usage estimate shown when testing a Data Source - it was assuming a similar ratio of characters-to-tokens as everyday written English, which understates the cost of dense, numeric/structured query results by a wide margin. Still a rough estimate, not a real token count, but noticeably closer to what a run actually gets billed."
        ]),
        new("1.11", new DateTime(2026, 9, 7, 15, 35, 0),
        [
            "A Data Source can now optionally be shared, so other Helpers can attach the same live data feed instead of it being set up again from scratch - off by default, so anything Helper-specific or holding restricted data stays private unless you choose to share it.",
            "A shared Data Source can only be edited directly by the Helper it was originally built for - any other Helper that changes it gets its own independent copy instead, so borrowing one can never affect the original or any other Helper relying on it.",
            "Admins can review every Data Source that's been defined, which Helper originally owns it, and which Helpers currently use it, under Admin → Data Sources."
        ]),
        new("1.10", new DateTime(2026, 9, 7, 13, 25, 0),
        [
            "Fixed an issue where a Helper's output could occasionally fail to appear in Recent Runs if you navigated to another page before it finished generating.",
            "Fixed a related error that could occasionally end your whole session (every open tab) if you navigated away from a running Helper before it finished."
        ]),
        new("1.9", new DateTime(2026, 9, 7, 12, 37, 0),
        [
            "Added an \"Edit this Helper\" shortcut on a Helper's run page, for anyone who's allowed to edit it - no more finding it again in the Helper Editor's own list.",
            "Fixed a bug found on real data: Helpers connected to a Data Source with several distinct-looking columns weren't being tidied up (from the 1.8 release above) as effectively as they should have been.",
            "Fixed an occasional error when running a Helper right after editing it, which needed a page refresh to clear."
        ]),
        new("1.8", new DateTime(2026, 9, 7, 11, 37, 0),
        [
            "Live data connected to a Helper is now automatically tidied up before it's sent to the AI, cutting out repeated information - so more data fits in before hitting a response size limit, and running Helpers with a Data Source is a little cheaper. Switchable off per Data Source if ever needed, on by default."
        ]),
        new("1.7", new DateTime(2026, 9, 7, 11, 16, 0),
        [
            "Fixed your monthly spend allowance (shown at the top of every page) reading noticeably lower than your real spend, which also meant the spending limit wasn't being enforced as strictly as it should have been."
        ]),
        new("1.6", new DateTime(2026, 9, 4, 15, 36, 0),
        [
            "The app now automatically follows your browser or computer's light/dark mode setting - no need to change anything, it just matches your usual preference.",
            "Admins can now set a monthly AI spending limit for individual users."
        ]),
        new("1.5", new DateTime(2026, 9, 4, 14, 53, 0),
        [
            "Added a new Admin area for authorised staff, covering who else has admin access, the AI models available to Helpers, document stylesheets, and a report of AI running costs (by user, by Helper, and over time)."
        ]),
        new("1.4", new DateTime(2026, 9, 4, 9, 15, 0),
        [
            "Fixed an issue where longer AI-generated responses could sometimes get cut off partway through.",
            "Small visual polish to the Helpers page."
        ]),
        new("1.3", new DateTime(2026, 9, 3, 18, 0, 0),
        [
            "Helpers can now be connected (by an administrator) to live council data, so their answers can automatically include up-to-date information.",
            "Added a safety net that keeps a temporary copy of anything a Helper generates for 7 days, in case you navigate away before saving it."
        ]),
        new("1.2", new DateTime(2026, 9, 2, 18, 0, 0),
        [
            "Helpers can now ask you a few quick questions before running, so they can tailor their output to your specific situation.",
            "Helpers can now read the content of a web page you give them, not just uploaded files.",
            "Added a running total of your monthly AI usage allowance.",
            "Improved colour contrast throughout the app to make it easier to read.",
            "Numerous smaller fixes to document editing and page reliability."
        ]),
        new("1.1", new DateTime(2026, 8, 28, 18, 0, 0),
        [
            "Added a Helper Editor, so authorised staff can create and adjust their own Helpers.",
            "You can now save documents a Helper has generated, and export them as Word, PDF, or a webpage.",
            "Added a dedicated editor for polishing a document before saving it."
        ]),
        new("1.0", new DateTime(2026, 8, 26, 18, 0, 0),
        [
            "First working version: sign in securely with your council account, browse the available Helpers, and generate real AI-assisted documents.",
            "Applied Conwy's corporate colours and branding throughout."
        ])
    ];
}
