// "What's new" slides: shown only when the user asks for them (About → "What's new"), never by TsuFiles on its own
// (tsudev PRODUCT_POLICY 3.8: no self-opening dialogs).

using TsuFiles.App.Controls;

namespace TsuFiles.App.Services;

public sealed class WhatsNewService(WindowService windows)
{
    private IReadOnlyList<SlideDeck>? _decks;

    /// <summary>The assembly whose resources hold the decks.</summary>
    internal const string Assembly = "TsuFiles";

    /// <summary>Every deck TsuFiles ships, oldest first.</summary>
    public IReadOnlyList<SlideDeck> Decks => _decks ??= SlideDeck.LoadAll(Assembly);

    /// <summary>The version running now (three parts).</summary>
    internal Version CurrentVersion { get; set; } = SlideDeck.TryParseVersion(UpdateService.CurrentVersion) ?? new Version(0, 0, 0);

    /// <summary>The newest deck up to this version: what About → "What's new" shows. Null when there is none.</summary>
    public SlideDeck? Latest => SlideDeck.Newest(Decks, seen: null, CurrentVersion);

    /// <summary>Shows <paramref name="deck"/> (the latest one when null).</summary>
    public void Show(SlideDeck? deck = null)
    {
        deck ??= Latest;
        if (deck is not null)
            windows.ShowWhatsNew(deck);
    }
}
