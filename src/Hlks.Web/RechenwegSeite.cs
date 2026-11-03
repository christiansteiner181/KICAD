// Seite /rechenweg/ – Register der technischen Auslegungen im Klartext (Christian 03.11.2026: «so dass der nutzer immer nachvollziehen kann
// wie was gerechnet wird»). Bei jedem Aufruf frisch aus kataloge/rechenregister.json (Rechenregister im Kern), damit sie nie veraltet;
// /rechenweg/#<id> springt zu einem Verfahren. Geöffnet über das Symbol «Wie wird gerechnet» in der Kopfleiste (hilfe-oberflaeche.js).

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web;

public static class RechenwegSeite
{
    public static WebApplication MapRechenweg(this WebApplication app, ProjektDienst dienst)
    {
        app.MapGet("/rechenweg/{**rest}", () => Results.Text(Html(dienst.Katalog), "text/html; charset=utf-8"));
        return app;
    }

    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static IEnumerable<string> L(JsonNode? n) => (n as JsonArray ?? []).Select(T).OfType<string>();

    /// <summary>Ganze Seite: Übersicht nach Gebiet, darunter je Verfahren alle Felder; Suche filtert im Browser.</summary>
    public static string Html(Katalog k)
    {
        var reg = Rechenregister.Laden(k);
        var verfahren = Rechenregister.Verfahren(k);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"de-CH\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<title>Wie wird gerechnet – KICAD</title><style>")
          .Append("body{font:14px/1.45 Segoe UI,Arial,sans-serif;margin:0;color:#1d2330;background:#fff}header{position:sticky;top:0;background:#1f2a3a;color:#fff;padding:10px 20px;display:flex;gap:16px;align-items:center}")
          .Append("header h1{font-size:17px;margin:0;font-weight:600}header input{flex:1;max-width:360px;padding:5px 8px;border-radius:4px;border:0}main{max-width:980px;margin:0 auto;padding:12px 20px 40px}")
          .Append("nav h2,section h2{font-size:15px;margin:18px 0 6px;color:#1f2a3a;border-bottom:1px solid #d5dbe3}nav ul{margin:0;padding-left:18px}section{margin:0 0 18px}")
          .Append("section h3{font-size:15px;margin:16px 0 4px}dl{display:grid;grid-template-columns:140px 1fr;gap:3px 12px;margin:0}dt{color:#5b6677}dd{margin:0}dd ul,dd ol{margin:0;padding-left:18px}")
          .Append("code{font:12px Consolas,monospace;color:#3b4a60}.leise{color:#5b6677}.versteckt{display:none}@media print{header input{display:none}header{position:static}}</style></head><body>")
          .Append("<header><h1>Wie wird gerechnet</h1><input id=\"suche\" type=\"search\" placeholder=\"Suchen (z. B. Spitzendurchfluss, Gefälle, SIA)\" autofocus>")
          .Append("<span class=\"leise\">Stand ").Append(E(T(reg["stand"]))).Append(" · ").Append(verfahren.Count).Append(" Verfahren</span></header><main>");
        if (T(reg["beschreibung"]) is { } be) sb.Append("<p class=\"leise\">").Append(E(be)).Append("</p>");
        var gebiete = verfahren.GroupBy(v => T(v["gebiet"]) ?? "Allgemein").ToList();
        sb.Append("<nav>");
        foreach (var g in gebiete)
        {
            sb.Append("<h2>").Append(E(g.Key)).Append("</h2><ul>");
            foreach (var v in g) sb.Append("<li data-v=\"").Append(E(T(v["id"]))).Append("\"><a href=\"#").Append(E(T(v["id"]))).Append("\">").Append(E(T(v["titel"]))).Append("</a></li>");
            sb.Append("</ul>");
        }
        sb.Append("</nav>");
        foreach (var g in gebiete)
        {
            sb.Append("<h2>").Append(E(g.Key)).Append("</h2>");
            foreach (var v in g)
            {
                sb.Append("<section id=\"").Append(E(T(v["id"]))).Append("\" data-v=\"").Append(E(T(v["id"]))).Append("\"><h3>").Append(E(T(v["titel"]))).Append("</h3><dl>");
                void Feld(string titel, string? text) { if (text is { Length: > 0 }) sb.Append("<dt>").Append(titel).Append("</dt><dd>").Append(E(text)).Append("</dd>"); }
                void Liste(string titel, IEnumerable<string> l, bool nummeriert = false)
                {
                    var a = l.ToList();
                    if (a.Count == 0) return;
                    var tag = nummeriert ? "ol" : "ul";
                    sb.Append("<dt>").Append(titel).Append("</dt><dd><").Append(tag).Append('>');
                    foreach (var x in a) sb.Append("<li>").Append(E(x)).Append("</li>");
                    sb.Append("</").Append(tag).Append("></dd>");
                }
                Feld("Zweck", T(v["zweck"]));
                Liste("Eingaben", L(v["eingaben"]));
                Liste("Rechenweg", L(v["rechenweg"]), true);
                Feld("Ergebnis", T(v["ergebnis"]));
                Liste("Quellen", L(v["quellen"]));
                Liste("Annahmen", L(v["annahmen"]));
                Liste("Grenzen", L(v["grenzen"]));
                var code = L(v["code"]).ToList();
                if (code.Count > 0) sb.Append("<dt>Code</dt><dd>").Append(string.Join("<br>", code.Select(c => "<code>" + E(c) + "</code>"))).Append("</dd>");
                if (T(v["abfrage"]) is { Length: > 0 } ab) sb.Append("<dt>Abfrage</dt><dd><code>").Append(E(ab)).Append("</code></dd>");
                sb.Append("<dt>Kennung</dt><dd><code>").Append(E(T(v["id"]))).Append("</code></dd></dl></section>");
            }
        }
        if (verfahren.Count == 0) sb.Append("<p>Das Register ist noch leer (kataloge/rechenregister.json fehlt).</p>");
        sb.Append("</main><script>const s=document.getElementById('suche');s.addEventListener('input',()=>{const q=s.value.trim().toLowerCase();")
          .Append("document.querySelectorAll('section[data-v]').forEach(x=>x.classList.toggle('versteckt',q!==''&&!x.textContent.toLowerCase().includes(q)));")
          .Append("document.querySelectorAll('nav li[data-v]').forEach(li=>{const z=document.getElementById(li.dataset.v);li.classList.toggle('versteckt',!!z&&z.classList.contains('versteckt'));});});</script></body></html>");
        return sb.ToString();
    }
}
