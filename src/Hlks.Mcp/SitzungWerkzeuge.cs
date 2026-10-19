// Werkzeuge einer Sitzung des MCP-Servers (Modul 3c, Teil K), die nicht am Projekt hängen, sondern an diesem einen Lauf:
// kontext_bild liefert das Bild der Ansicht, das zur Frage in der KI-Palette mitgeschickt wurde, als MCP-Bildinhalt – damit Claude den
// Plan «sehen» kann (Leitprinzip «Bilder, damit die KI schauen kann»). Der Arbeitsplatz-Server legt das Bild im Arbeitsordner der KI ab
// (nie im Projekt) und startet den MCP-Server mit --kontext-bild <datei>. Ohne Bild: Hinweis «kein Bild zur Frage».
// Bewusst eine eigene Werkzeugklasse (nicht Werkzeuge): Werkzeuge bildet genau die Abfragen und Befehle des Kerns ab (Abdeckungstest).

using System.ComponentModel;
using Hlks.Kern;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

/// <summary>Bild der Ansicht zur aktuellen Frage (Pfad aus --kontext-bild; null = kein Bild).</summary>
public sealed record KontextBild(string? Datei);

[McpServerToolType]
public sealed class SitzungWerkzeuge(KontextBild bild)
{
    public const string KontextBildText = "Zeigt das Bild der aktiven Ansicht (Plan bzw. 3D), das zur Frage mitgeschickt wurde – so siehst du, was der Mensch " +
        "gerade sieht. Ohne Bild zur Frage kommt ein Hinweis. Das Bild ist ein Hilfsmittel; Werte und Masse immer aus dem Modell lesen (Abfragen).";
    /// <summary>Höchstgrösse des Bildes (wie die Grenze der Frage im Arbeitsplatz-Server).</summary>
    public const int MaxBytes = 4 * 1024 * 1024;

    [McpServerTool(Name = "kontext_bild"), Description(KontextBildText)]
    public CallToolResult KontextBildZeigen()
    {
        static CallToolResult Text(string t, bool fehler = false) => new() { IsError = fehler, Content = [new TextContentBlock { Text = t }] };
        if (string.IsNullOrWhiteSpace(bild.Datei) || !File.Exists(bild.Datei))
            return Text("Kein Bild zur Frage – der Mensch hat kein Bild der Ansicht mitgeschickt. Mit den Abfragen (z. B. projekt_ueberblick, element_lesen) arbeiten.");
        try
        {
            var bytes = File.ReadAllBytes(bild.Datei);
            if (bytes.Length == 0 || bytes.Length > MaxBytes) return Text($"Das Bild zur Frage ist leer oder zu gross ({bytes.Length} Bytes).", true);
            var mime = Art(bytes);
            if (mime is null) return Text("Das Bild zur Frage ist weder PNG noch JPEG.", true);
            return new CallToolResult
            {
                Content =
                [
                    new TextContentBlock { Text = Json.Text(new { hinweis = "Bild der aktiven Ansicht zur Frage", art = mime, bytes = bytes.Length }) },
                    ImageContentBlock.FromBytes(bytes, mime),
                ],
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Text("Das Bild zur Frage ist nicht lesbar: " + ex.Message, true); }
    }

    /// <summary>PNG oder JPEG an den ersten Bytes erkennen (sonst null).</summary>
    public static string? Art(byte[] b) =>
        b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A ? "image/png"
        : b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "image/jpeg"
        : null;
}
