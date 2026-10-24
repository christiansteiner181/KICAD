using Hlks.Xr.Bedienung;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Quest App V1a, Einhängepunkt für das Wegwischen (Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste … von rechts nach links»; Regeln in
/// Hlks.Xr.Bedienung.Wegwischen): alles, was die eine Geste aus dem Bild schieben kann, meldet sich hier an (QuestApp.WegwischZiele). Heute die App-Symbole; V1c hängt
/// den PC-Bildschirm ein. Welches Ziel gilt, wenn mehreres sichtbar ist, entscheidet Wegwischen.Ziel (was die Hand überstreicht, sonst das zuletzt Eingeblendete).
/// </summary>
interface IWegwischbar
{
    /// <summary>Sichtbar und wegwischbar: Kandidat (Id, Fläche im Raum der Brille, eingeblendet seit); sonst null.</summary>
    WegwischKandidat? Kandidat(XVec3 auge);

    /// <summary>Die Hand wischt gerade über dieses Ziel: Weg nach links (m; 0 = nicht mehr) – es läuft mit.</summary>
    void Mitlaufen(double wegM);

    /// <summary>Weggewischt (einmal): nach links aus dem Bild.</summary>
    void Wegwischen(double t);
}

/// <summary>Ein Ziel des Wegwischens aus drei Funktionen (für Ziele, die keine eigene Klasse haben, z. B. die Karte einer Prüfaufgabe).</summary>
sealed class WischZiel(Func<XVec3, WegwischKandidat?> kandidat, Action<double> mitlaufen, Action<double> wegwischen) : IWegwischbar
{
    public WegwischKandidat? Kandidat(XVec3 auge) => kandidat(auge);
    public void Mitlaufen(double wegM) => mitlaufen(wegM);
    public void Wegwischen(double t) => wegwischen(t);
}
