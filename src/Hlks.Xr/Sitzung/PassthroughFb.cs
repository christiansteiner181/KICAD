// Quest App P4b (Modul XR, Passthrough für «Echter Tisch»): die Datenstrukturen der OpenXR-Erweiterung XR_FB_passthrough, wie die App sie über die OpenXR-Schnittstelle
// von StereoKit 0.3.11 (Backend.OpenXR.RequestExt, GetFunction, AddCompositionLayer) an die Laufzeit gibt. Ohne StereoKit, damit Lage und Grösse der Strukturen am PC
// geprüft werden können (Tests PassthroughFbTests). Muster: das Beispiel «PassthroughFBExt» im Repository von StereoKit (Examples/StereoKitTest/Tools, MIT),
// nachgebaut ohne Download; Werte nach der OpenXR-Spezifikation, Erweiterung XR_FB_passthrough (Nummer 119, daher Strukturtypen 1000118001 … 1000118003).
//
// Speicherbild (64 Bit, little endian – Quest: ARM64, PC: x64): XrStructureType ist ein 32-Bit-Enum, gefolgt von 4 Byte Füllung vor dem Zeiger «next»; hier als
// ein ulong geführt (unten der Typ, oben null). XrFlags64, XrSpace und die Handles sind 64 Bit. Die Funktionen nehmen die Info-Strukturen als Zeiger (ref).

using System.Runtime.InteropServices;

namespace Hlks.Xr.Sitzung;

public static class PassthroughFb
{
    public const string Erweiterung = "XR_FB_passthrough";

    public const ulong TypPassthroughCreateInfo = 1000118001;
    public const ulong TypPassthroughLayerCreateInfo = 1000118002;
    public const ulong TypCompositionLayerPassthrough = 1000118003;

    /// <summary>XR_PASSTHROUGH_IS_RUNNING_AT_CREATION_BIT_FB.</summary>
    public const ulong LaeuftAbErzeugung = 0x1;
    /// <summary>XR_PASSTHROUGH_LAYER_PURPOSE_RECONSTRUCTION_FB.</summary>
    public const uint ZweckRekonstruktion = 0;
    /// <summary>XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT: die Szene von StereoKit wird mit ihrem Alpha über das Kamerabild gelegt.</summary>
    public const ulong EbeneAlphaMischen = 0x2;
    /// <summary>XR_SUCCESS.</summary>
    public const int Erfolg = 0;

    /// <summary>Reihenfolge der Ebene beim Zusammensetzen: vor (unter) der Ebene von StereoKit (0).</summary>
    public const int Reihenfolge = -1;

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateInfo
    {
        public ulong Typ;
        public IntPtr Next;
        public ulong Flags;
        public static CreateInfo Neu(ulong flags) => new() { Typ = TypPassthroughCreateInfo, Next = IntPtr.Zero, Flags = flags };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LayerCreateInfo
    {
        public ulong Typ;
        public IntPtr Next;
        public ulong Passthrough;
        public ulong Flags;
        public uint Zweck;
        public uint Fuellung;
        public static LayerCreateInfo Neu(ulong passthrough, ulong flags, uint zweck = ZweckRekonstruktion) =>
            new() { Typ = TypPassthroughLayerCreateInfo, Next = IntPtr.Zero, Passthrough = passthrough, Flags = flags, Zweck = zweck };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CompositionLayer
    {
        public ulong Typ;
        public IntPtr Next;
        public ulong Flags;
        public ulong Space;
        public ulong Layer;
        public static CompositionLayer Neu(ulong layer) =>
            new() { Typ = TypCompositionLayerPassthrough, Next = IntPtr.Zero, Flags = EbeneAlphaMischen, Space = 0, Layer = layer };
    }

    // Funktionen der Erweiterung (xrGetInstanceProcAddr über Backend.OpenXR.GetFunction). XrSession und die Handles sind 64 Bit.
    public delegate int ErzeugenFn(ulong session, ref CreateInfo info, out ulong passthrough);
    public delegate int EbeneErzeugenFn(ulong session, ref LayerCreateInfo info, out ulong ebene);
    public delegate int HandleFn(ulong handle);

    public const string FnErzeugen = "xrCreatePassthroughFB";
    public const string FnZerstoeren = "xrDestroyPassthroughFB";
    public const string FnStarten = "xrPassthroughStartFB";
    public const string FnPausieren = "xrPassthroughPauseFB";
    public const string FnEbeneErzeugen = "xrCreatePassthroughLayerFB";
    public const string FnEbeneZerstoeren = "xrDestroyPassthroughLayerFB";
    public const string FnEbenePausieren = "xrPassthroughLayerPauseFB";
    public const string FnEbeneFortsetzen = "xrPassthroughLayerResumeFB";
}

/// <summary>
/// Zustand des Schalters «Echter Tisch» (rein): verfügbar (Erweiterung geladen und Passthrough angelegt), ein, wartet aufs Antippen, Tisch festgelegt. Die App zeigt den Schalter
/// grau mit Hinweis, wenn Passthrough nicht verfügbar ist.
/// </summary>
public sealed class EchterTisch
{
    public bool Verfuegbar { get; private set; }
    public string Grund { get; private set; } = "Passthrough noch nicht geprüft.";
    public bool Ein { get; private set; }
    /// <summary>Ein, aber die Platte ist noch nicht angetippt (das Modell ist ausgeblendet, die Anzeige bittet ums Antippen).</summary>
    public bool WartetAufTisch => Ein && Tisch is null;
    public TischTreffer? Tisch { get; private set; }

    public void VerfuegbarSetzen(bool ja, string grund) { Verfuegbar = ja; Grund = grund; if (!ja) Ein = false; }

    /// <summary>Schalter: ein nur, wenn verfügbar; ein setzt den Tisch zurück (neu antippen). Rückgabe: der neue Zustand.</summary>
    public bool Schalten(bool ein)
    {
        Ein = ein && Verfuegbar;
        Tisch = null;
        return Ein;
    }

    /// <summary>Neu antippen (Knopf in der Box), nur wenn ein.</summary>
    public void NeuAntippen() { if (Ein) Tisch = null; }

    public void TischSetzen(TischTreffer t) { if (Ein) Tisch = t; }
}
