// Befehle der Grundlagen DWG/DXF, PDF und Bild (Modul 1b, Auftrag B3): Unterlage einfügen und kalibrieren.
// Die Plangrafik der Unterlagen steht in GrundlagenCad.cs (DWG/DXF) und GrundlagenBild.cs (PDF/Bild); gelesen wird
// DWG/DXF in CadLeser.cs, PDF-Seiten rendert PdfSeite (BildDatei.cs).

namespace Hlks.Kern;

public static class GrundlagenBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new UnterlageEinfuegen());
        r.Hinzufuegen(new UnterlageKalibrieren());
    }
}
