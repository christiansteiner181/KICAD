using System.Runtime.InteropServices;
using StereoKit;
using Fb = Hlks.Xr.Sitzung.PassthroughFb;

namespace Hlks.Quest;

/// <summary>
/// Passthrough der Quest (OpenXR-Erweiterung XR_FB_passthrough) für den Schalter «Echter Tisch» im Sitzungsraum (P4b; Entscheid Christian 23.10.2026 «Beides, Passthrough
/// prüfen», «raum in VR zeigen. Es der raum erscheint auf dem tisch.»). StereoKit 0.3.11 schaltet Passthrough nicht selbst ein (Protokoll P0); die App macht es über die
/// OpenXR-Schnittstelle von StereoKit nach dem Muster «PassthroughFBExt» aus dem Repository von StereoKit (nachgebaut, Strukturen in Hlks.Xr.Sitzung.PassthroughFb):
///   1. vor SK.Initialize: Backend.OpenXR.RequestExt("XR_FB_passthrough") (<see cref="Anfordern"/>);
///   2. danach: Funktionen holen (GetFunction), Passthrough und Ebene anlegen – pausiert (<see cref="Anlegen"/>);
///   3. ein: Passthrough und Ebene starten; je Bild die Ebene unter die Szene legen (AddCompositionLayer, Reihenfolge −1, Alpha der Szene mischt); aus: pausieren.
///      Den Hintergrund des Renderers (durchsichtig, kein Himmel) setzt NICHT diese Klasse, sondern der Sitzungsraum über Hlks.Xr.Sitzung.HintergrundVerwalter – der
///      eine Ort, der sich den alten Zustand merkt (P4b-Nachtrag 23.10.2026; vorher merkten sich beide Klassen je den Zustand der anderen).
/// Ohne Erweiterung (Simulator am PC, andere Laufzeit) bleibt <see cref="Verfuegbar"/> falsch und die App zeigt den Schalter grau mit Hinweis.
/// Die Brille braucht im Manifest com.oculus.feature.PASSTHROUGH (vorhanden, required=false).
/// </summary>
sealed class Passthrough
{
    Fb.ErzeugenFn? erzeugen;
    Fb.EbeneErzeugenFn? ebeneErzeugen;
    Fb.HandleFn? zerstoeren, starten, pausieren, ebeneZerstoeren, ebenePausieren, ebeneFortsetzen;
    ulong passthrough, ebene;

    public bool Verfuegbar { get; private set; }
    public bool Ein { get; private set; }
    public string Grund { get; private set; } = "Passthrough noch nicht geprüft.";
    /// <summary>Bilder, in denen die Ebene übergeben wurde (Protokoll).</summary>
    public long Ebenen { get; private set; }

    /// <summary>Vor SK.Initialize aufrufen (nur im XR-Modus sinnvoll; im Simulator schadet es nicht).</summary>
    public static void Anfordern() => Backend.OpenXR.RequestExt(Fb.Erweiterung);

    /// <summary>Nach SK.Initialize: prüft die Erweiterung, holt die Funktionen und legt Passthrough und Ebene pausiert an.</summary>
    public void Anlegen()
    {
        if (Backend.XRType != BackendXRType.OpenXR) { Grund = $"Kein OpenXR ({Backend.XRType}) – Passthrough gibt es nur in der Brille."; return; }
        if (!Backend.OpenXR.ExtEnabled(Fb.Erweiterung)) { Grund = "Die Laufzeit bietet XR_FB_passthrough nicht an."; return; }
        try
        {
            erzeugen = Backend.OpenXR.GetFunction<Fb.ErzeugenFn>(Fb.FnErzeugen);
            ebeneErzeugen = Backend.OpenXR.GetFunction<Fb.EbeneErzeugenFn>(Fb.FnEbeneErzeugen);
            zerstoeren = Backend.OpenXR.GetFunction<Fb.HandleFn>(Fb.FnZerstoeren);
            starten = Backend.OpenXR.GetFunction<Fb.HandleFn>(Fb.FnStarten);
            pausieren = Backend.OpenXR.GetFunction<Fb.HandleFn>(Fb.FnPausieren);
            ebeneZerstoeren = Backend.OpenXR.GetFunction<Fb.HandleFn>(Fb.FnEbeneZerstoeren);
            ebenePausieren = Backend.OpenXR.GetFunction<Fb.HandleFn>(Fb.FnEbenePausieren);
            ebeneFortsetzen = Backend.OpenXR.GetFunction<Fb.HandleFn>(Fb.FnEbeneFortsetzen);
        }
        catch (Exception ex) when (ex is ArgumentException or MarshalDirectiveException or InvalidOperationException)
        {
            Grund = "Passthrough-Funktionen nicht ladbar: " + ex.Message;
            return;
        }
        if (erzeugen is null || ebeneErzeugen is null || zerstoeren is null || starten is null || pausieren is null || ebeneZerstoeren is null
            || ebenePausieren is null || ebeneFortsetzen is null)
        {
            Grund = "Die Laufzeit liefert nicht alle Funktionen von XR_FB_passthrough.";
            return;
        }
        var info = Fb.CreateInfo.Neu(0);
        var r = erzeugen(Backend.OpenXR.Session, ref info, out passthrough);
        if (r != Fb.Erfolg) { Grund = $"xrCreatePassthroughFB: {r}"; return; }
        var li = Fb.LayerCreateInfo.Neu(passthrough, 0);
        r = ebeneErzeugen(Backend.OpenXR.Session, ref li, out ebene);
        if (r != Fb.Erfolg) { Grund = $"xrCreatePassthroughLayerFB: {r}"; zerstoeren(passthrough); passthrough = 0; return; }
        Verfuegbar = true;
        Grund = "Passthrough bereit (XR_FB_passthrough).";
    }

    /// <summary>Schaltet Passthrough ein oder aus. Rückgabe: wirksamer Zustand.</summary>
    public bool Schalten(bool ein)
    {
        if (!Verfuegbar || ein == Ein) return Ein;
        if (ein)
        {
            var r = starten!(passthrough);
            if (r == Fb.Erfolg) r = ebeneFortsetzen!(ebene);
            if (r != Fb.Erfolg) { Grund = $"Passthrough startet nicht: {r}"; Log.Err(Grund); return Ein; }
            Ein = true;
        }
        else
        {
            ebenePausieren!(ebene);
            pausieren!(passthrough);
            Ein = false;
        }
        return Ein;
    }

    /// <summary>Je Bild: die Ebene des Kamerabilds unter die Szene legen (nur wenn ein).</summary>
    public void Bild()
    {
        if (!Ein) return;
        Backend.OpenXR.AddCompositionLayer(Fb.CompositionLayer.Neu(ebene), Fb.Reihenfolge);
        Ebenen++;
    }

    public void Beenden()
    {
        if (!Verfuegbar) return;
        if (Ein) Schalten(false);
        try { ebeneZerstoeren!(ebene); zerstoeren!(passthrough); } catch (Exception ex) when (ex is InvalidOperationException or AccessViolationException) { }
        Verfuegbar = false;
    }
}
