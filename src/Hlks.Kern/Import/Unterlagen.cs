// Unterlagen im Projekt (Modul 1b): fremde Dateien (IFC, DWG/DXF, PDF, Bilder) liegen unverändert im Ordner
// unterlagen der Projektablage, Name = Prüfsumme (SHA-256, 12 Zeichen) + «_» + ursprünglicher Name. Gleicher Inhalt =
// gleicher Name: Ablegen ist darum wiederholbar und überschreibt nie etwas. Abgeleitete Dateien (Seitenbild, Netz) liegen
// daneben mit einem Zusatz und werden einmal erzeugt; zwei Arbeitsplätze, die gleichzeitig erzeugen, schreiben
// denselben Inhalt (der zweite verwirft seinen).

using System.Security.Cryptography;

namespace Hlks.Kern;

public static class Unterlagen
{
    /// <summary>Ordner der Unterlagen; ohne Ablage (Tests, Simulator) ein Temp-Ordner je Projekt.</summary>
    public static string Ordner(Projekt p)
    {
        var o = p.Speicher?.UnterlagenOrdner;
        if (o is not null)
        {
            if (!Directory.Exists(o))
                throw new HlksFehler($"Der Ordner unterlagen fehlt in {p.Speicher!.Ort}.", "Netzwerk/VPN prüfen; sonst den Ordner «unterlagen» im Projektordner anlegen.");
            return o;
        }
        var t = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "unterlagen", p.Kopf.Id);
        Directory.CreateDirectory(t);
        return t;
    }

    /// <summary>Legt eine Datei als Unterlage ab (oder findet sie wieder) und gibt Name und Prüfsumme zurück. Steht
    /// quelle schon im Unterlagenordner (Name aus einer früheren Ablage), wird nur nachgeschlagen.</summary>
    public static (string Name, string Stand) Ablegen(Projekt p, string quelle)
    {
        var ordner = Ordner(p);
        var imOrdner = Path.Combine(ordner, Path.GetFileName(quelle));
        if (!File.Exists(quelle) && File.Exists(imOrdner)) quelle = imOrdner;
        if (!File.Exists(quelle)) throw new HlksFehler($"Datei {quelle} gibt es nicht.", "Pfad prüfen (Netzlaufwerk verbunden?) oder die Datei hochladen.");
        string stand;
        using (var s = File.OpenRead(quelle)) stand = Convert.ToHexString(SHA256.HashData(s))[..12].ToLowerInvariant();
        var original = Path.GetFileName(quelle);
        if (original.Length > 13 && original[12] == '_' && original[..12] == stand) original = original[13..];   // schon abgelegt
        var name = $"{stand}_{Sauber(original)}";
        var ziel = Path.Combine(ordner, name);
        if (!File.Exists(ziel))
        {
            var temp = Path.Combine(ordner, $".{name}.{Guid.NewGuid():N}.tmp");
            File.Copy(quelle, temp);
            try { File.Move(temp, ziel, overwrite: false); }
            catch (IOException) when (File.Exists(ziel)) { File.Delete(temp); }   // gleichzeitig von einem anderen Gerät abgelegt
        }
        return (name, stand);
    }

    /// <summary>Voller Pfad einer abgelegten Unterlage.</summary>
    public static string Pfad(Projekt p, string name)
    {
        var pfad = Path.Combine(Ordner(p), Path.GetFileName(name));
        if (!File.Exists(pfad)) throw new HlksFehler($"Unterlage {name} fehlt im Ordner unterlagen.", "Datei erneut einfügen (sie wird unter demselben Namen abgelegt).");
        return pfad;
    }

    /// <summary>Pfad einer abgeleiteten Datei (z. B. «.seite1.png», «.netz.bin»). Fehlt sie, erzeugt erzeugen sie in einer
    /// Temp-Datei, die dann ohne Überschreiben an ihren Platz kommt.</summary>
    public static string Ableitung(Projekt p, string name, string zusatz, Action<string> erzeugen)
    {
        var ordner = Ordner(p);
        var ziel = Path.Combine(ordner, Path.GetFileName(name) + zusatz);
        if (File.Exists(ziel)) return ziel;
        var temp = Path.Combine(ordner, $".{Path.GetFileName(name)}{zusatz}.{Guid.NewGuid():N}.tmp");
        try
        {
            erzeugen(temp);
            try { File.Move(temp, ziel, overwrite: false); }
            catch (IOException) when (File.Exists(ziel)) { }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return ziel;
    }

    /// <summary>Ursprünglicher Dateiname ohne Pfad und ohne Prüfsummen-Präfix der Ablage («3f2a…_Plan.ifc» → «Plan.ifc»).
    /// Daran erkennt der Import eine neue Fassung derselben Unterlage (Befund C1-1).</summary>
    public static string OriginalName(string pfadOderName)
    {
        var n = Path.GetFileName(pfadOderName);
        return n.Length > 13 && n[12] == '_' && n[..12].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? n[13..] : n;
    }

    static string Sauber(string name)
    {
        var ungueltig = Path.GetInvalidFileNameChars();
        var s = new string(name.Select(c => ungueltig.Contains(c) ? '_' : c).ToArray()).Trim().TrimStart('.');
        return s.Length == 0 ? "unterlage" : s;
    }
}
