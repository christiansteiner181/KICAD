// Gemeinsame Hilfen der REST-Endpunkte (StartEndpunkte, BenutzerEndpunkte): JSON-Antwort, Körper lesen, Fehler in Statuscodes übersetzen.
// Fachliche Fehler: 400 mit { fehler, vorschlag }; «kein Projekt offen»: 409; Ablage/Netz weg: 503.

using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public static class EndpunktHilfe
{
    public static IResult Antwort(object o, int code = 200) => Results.Text(Json.Text(o), "application/json; charset=utf-8", statusCode: code);

    public static async Task<JsonObject> KoerperLesen(HttpRequest r)
    {
        using var leser = new StreamReader(r.Body, Encoding.UTF8);
        var text = await leser.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        try { return JsonNode.Parse(text) as JsonObject ?? throw new HlksFehler("Parameter müssen ein JSON-Objekt sein."); }
        catch (System.Text.Json.JsonException ex) { throw new HlksFehler("Parameter sind kein gültiges JSON: " + ex.Message); }
    }

    public static async Task<IResult> Gesichert(Func<Task<IResult>> arbeit)
    {
        try { return await arbeit(); }
        catch (HlksFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, f.Message.StartsWith("Es ist kein Projekt offen") ? 409 : 400); }
        catch (HkXrFehler f) { return Antwort(new { fehler = f.Message, vorschlag = f.Vorschlag }, 400); }   // Fehler aus HK_XR (BrillenDienst im Quest-Dialog)
        catch (TimeoutException) { return Antwort(new { fehler = "Der Zugriff hat zu lange gedauert.", vorschlag = "Netzwerk/VPN prüfen und erneut versuchen." }, 503); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Antwort(new { fehler = "Zugriff nicht möglich: " + ex.Message, vorschlag = "Netzwerk/VPN und Rechte prüfen." }, 503); }
    }
}
