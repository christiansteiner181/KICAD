// Befehle des IFC-Übertrags (Modul 1b). Die Abfrage ifc_pruefen steht in Abfragen.Liste und Tuer.

namespace Hlks.Kern;

public static class ImportBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new IfcUebernehmen());
    }
}
