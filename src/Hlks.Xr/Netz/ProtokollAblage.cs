// Quest App P5b (Modul XR, Interface-Labor): Messprotokolle, die ohne Verbindung zum PC entstanden, bleiben im privaten Speicher der App, bis «willkommen» kommt
// (dann sendet die App sie nach). So geht ein Lauf ohne PC (bzw. bei einer Trennung) auch über einen Neustart der App nicht verloren.
// Ablage: <ordner>/labor/messprotokolle_warten.jsonl – je Zeile eine fertige Nachricht «messprotokoll» (KanalText.Messprotokoll), höchstens Max Zeilen (die
// ältesten fallen weg). Zeilen, die keine Nachricht «messprotokoll» sind, werden beim Lesen übergangen.

using System.Text;
using System.Text.Json;

namespace Hlks.Xr.Netz;

public sealed class ProtokollAblage(string ordner)
{
    /// <summary>Höchstens so viele Messprotokolle warten (Annahme P5b; ein Labortag hat weit weniger Läufe).</summary>
    public const int Max = 50;
    /// <summary>Längste Zeile (Zeichen); längere werden nicht abgelegt (der Server nimmt Nachrichten bis 64 KB).</summary>
    public const int ZeileMax = 16_000;

    public string Datei => Path.Combine(ordner, "labor", "messprotokolle_warten.jsonl");

    static bool IstMessprotokoll(string zeile)
    {
        if (zeile.Length == 0 || zeile.Length > ZeileMax) return false;
        try
        {
            using var d = JsonDocument.Parse(zeile);
            return KanalText.Lesen(zeile) is { Typ: "messprotokoll" } && d.RootElement.TryGetProperty("aufgabe", out var a) && a.ValueKind == JsonValueKind.String;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Die wartenden Messprotokolle (älteste zuerst); leer ohne Datei oder bei einem Lesefehler.</summary>
    public List<string> Lesen()
    {
        try { return File.Exists(Datei) ? [.. File.ReadAllLines(Datei, Encoding.UTF8).Where(IstMessprotokoll).TakeLast(Max)] : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Legt die Liste ab (die neuesten Max); eine leere Liste löscht die Datei.</summary>
    public void Speichern(IEnumerable<string> protokolle)
    {
        var liste = protokolle.Where(IstMessprotokoll).TakeLast(Max).ToList();
        if (liste.Count == 0) { if (File.Exists(Datei)) File.Delete(Datei); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(Datei)!);
        var tmp = Datei + ".tmp";
        File.WriteAllLines(tmp, liste, new UTF8Encoding(false));
        File.Move(tmp, Datei, overwrite: true);
    }
}
