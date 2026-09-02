namespace AiHelpers.Services;

/// <summary>
/// Lets any component request the feedback modal, or signal that a Helper run just completed,
/// without needing a direct reference to FeedbackModal itself - it lives once, globally, in
/// MainLayout (like BetaNoticeModal), but both NavMenu's "Feedback" link and HelperDetail.razor's
/// "a run just finished" signal need to reach it from elsewhere in the component tree.
///
/// Superseded design, 2026-09: this used to also drive an automatic post-run popup with server
/// and client-side cooldowns to keep it from nagging. Dropped entirely in favour of a pulsing
/// NavMenu hint instead (RunCompleted below) - a native &lt;dialog&gt; popping open mid-page-
/// state-change turned out to be genuinely fragile (see MainLayout's own @key comment for one
/// concrete cause), and an attention-grabbing-but-non-interruptive nav hint sidesteps that whole
/// class of problem while being explicitly less annoying across repeated runs, which was the
/// actual goal. ShowAsync (opening the modal) is now only ever reached via a deliberate click, so
/// there's no cooldown concept left to bypass.
///
/// A settable async delegate rather than a plain event for RequestAsync - HelperDetail.razor
/// used to need to genuinely AWAIT the modal being shown before proceeding (opening a new tab
/// immediately after would steal focus), and a multicast event has no clean way to be awaited by
/// its raiser. There's only ever one real subscriber in practice (MainLayout, the only thing that
/// renders the modal), so this doesn't need to support many. Scoped (one instance per circuit) so
/// one user's request/signal can never reach another's modal or nav menu.
/// </summary>
public interface IFeedbackPromptService
{
    Func<string?, int?, Task>? ShowHandler { get; set; }
    Task RequestAsync(string? helperName, int? helperDefinitionId);

    /// <summary>Fired whenever a Helper run completes, purely so NavMenu can start pulsing its
    /// Feedback link to draw attention to it - no payload, no cooldown, idempotent (pulsing twice
    /// looks the same as pulsing once).</summary>
    event Action? RunCompleted;
    void NotifyRunCompleted();
}
