using System.Globalization;
using Hlks.Xr.Sitzung;

namespace Hlks.Quest;

/// <summary>
/// Quest App P5b: Schnitt mit der flachen Hand am Sitzungstisch (Hlks.Xr.Sitzung.FlacheHand; Sitzungsraum.FlacheHandGeaendert) – Hinweise und Text im Protokoll.
/// XR-Labor L0: aus QuestApp.Labor.cs hierher, unverändert – gehört zur normalen Bedienung und bleibt in der KICAD-App (entwurf/labor-app.md).
/// </summary>
sealed partial class QuestApp
{
    void FlacheHandMelden(FlacheHandEreignis e, HandSchnitt? s)
    {
        if (e == FlacheHandEreignis.Beginnt)
            Hinweis(s?.Art == HandSchnittArt.Senkrecht ? "Vertikalschnitt folgt der Hand – Hand weg behält ihn." : "Schnitt folgt der flachen Hand – Hand weg behält ihn.");
        else if (e == FlacheHandEreignis.Behalten)
            Hinweis("Schnitt bleibt – «Tisch» › «Zurücksetzen» hebt ihn auf.");
    }

    string SchnittHandText() => sitzung.SchnittHand is not { } s ? "keiner"
        : s.Art == HandSchnittArt.Waagrecht
            ? string.Create(CultureInfo.InvariantCulture, $"waagrecht auf {s.Hoehe:0.00} m{(sitzung.SchnittFolgt ? ", folgt" : "")}")
            : string.Create(CultureInfo.InvariantCulture, $"senkrecht ({s.Normale.X:0.00}, {s.Normale.Z:0.00}){(sitzung.SchnittFolgt ? ", folgt" : "")}");
}
