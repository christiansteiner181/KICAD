// Zweiter Zugang zu KICAD im Büronetz – nur für die Quest App (Modul XR, Quest App P3; Entscheid Christian 23.10.2026: «Sofort WLAN mit Kopplung und
// TLS … eigener Port, HTTPS, nur gekoppelte Brillen, nur /api/xr/…. Alles andere bleibt auf localhost.»).
//
// AUFBAU
//   Der bisherige Server (Program.cs) bleibt unverändert: nur http://localhost:<Port>, Herkunftsschutz vor /api, /hilfe, /vr.
//   XrNetzDienst startet daneben einen EIGENEN, kleinen Kestrel-Server – nur wenn die Benutzereinstellung xr.zugang an ist (Vorgabe aus) und dieses KICAD
//   auf dem Desktop-Port 5130 läuft (Abnahme- und Testserver öffnen nie etwas im Netz):
//     - HTTPS auf allen IPv4-Adressen, Port 5140 (Einstellung xr.port), TLS 1.2 oder 1.3, Zertifikat von KICAD (XrZertifikat.cs), nur HTTP/1.1.
//     - Er kennt NUR die Endpunkte des Hauptservers mit der Marke XrImNetz() und einem Muster unter /api/xr/ (XrPforte.Freigegeben) – keine Oberfläche,
//       keine Dateien, kein anderes /api. Alles andere ergibt 404.
//     - Vor jedem Endpunkt steht die Pforte (XrPforte.Pruefen, EINE Stelle für Pfadsperre und Schlüssel), danach die WebSocket-Middleware.
//     - Grenzen: 32 Verbindungen, 64 KB Körper, 16 KB Kopfzeilen, Kopfzeilen in 10 s, je IP 20 Anfragen/s (Vorrat 60), 10 Fehlversuche je 10 min.
//   Daneben die Suche (UDP 5141, Einstellung xr.suchport; XrSuche.cs). Ein- und Ausschalten wirkt ohne Neustart (POST /api/xr/zugang, sonst alle 10 s
//   nachgelesen). Ist ein Port belegt, bleibt der Netzzugang aus und GET /api/xr/zugang meldet es; der localhost-Server läuft weiter.
//
// PFORTE (XrPforte.Pruefen), in dieser Reihenfolge:
//   1. Pfad nicht unter /api/xr                                    → 404
//   2. Absender gesperrt (Fehlversuche) oder zu viele Anfragen     → 429
//   3. Schlüssel fehlt oder falsch (ausser Endpunkte mit XrImNetz(ohneSchluessel: true): nur «kopplung/anfrage» und «kopplung/abholen») → 401
//   4. Endpunkt unbekannt oder ohne Marke XrImNetz (z. B. die PC-Endpunkte /api/xr/zugang, /teilnehmer, /stellschraube, /kopplung/paket) → 404
//   5. Körper grösser als 64 KB                                    → 413 (ohne Längenangabe begrenzt Kestrel beim Lesen, ebenfalls 413)
//   6. Körper nicht application/json                               → 415
//   Die geprüfte Kopplung steht danach im Feature XrAnmeldung (nur die Pforte setzt es).
//
// Neue Endpunkte für die Brille (z. B. GLB je Geschoss): unter /api/xr/ anlegen und mit .XrImNetz() markieren; ohne Marke bleiben sie nur auf localhost.

using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hlks.Web;

/// <summary>Marke: der Endpunkt ist über den Netzzugang erreichbar. OhneSchluessel nur für die beiden Schritte der Kopplung (Weg B).</summary>
public sealed record XrNetzFreigabe(bool OhneSchluessel = false);

/// <summary>Die von der Pforte geprüfte Kopplung einer Anfrage über den Netzzugang.</summary>
public sealed record XrAnmeldung(XrKopplung Kopplung);

public static class XrPforte
{
    /// <summary>Gibt den Endpunkt für den Netzzugang frei (Marke XrNetzFreigabe).</summary>
    public static TBuilder XrImNetz<TBuilder>(this TBuilder builder, bool ohneSchluessel = false) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new XrNetzFreigabe(ohneSchluessel));

    /// <summary>Pfadsperre: nur /api/xr und darunter (ganze Segmente, ohne Rücksicht auf Gross-/Kleinschreibung).</summary>
    public static bool PfadErlaubt(PathString pfad) => pfad.StartsWithSegments("/api/xr", StringComparison.OrdinalIgnoreCase);

    /// <summary>Die Endpunkte, die der Netzzugang kennt: Marke XrNetzFreigabe und Muster unter /api/xr/.</summary>
    public static IReadOnlyList<Endpoint> Freigegeben(IEnumerable<Endpoint> alle) =>
        [.. alle.OfType<RouteEndpoint>().Where(e => e.Metadata.GetMetadata<XrNetzFreigabe>() is not null && PfadErlaubt(new PathString(MusterText(e))))];

    static string MusterText(RouteEndpoint e)
    {
        var t = e.RoutePattern.RawText ?? "";
        return t.StartsWith('/') ? t : "/" + t;
    }

    /// <summary>Die Kopplung einer Anfrage: über den Netzzugang von der Pforte gesetzt; auf localhost hier mit derselben Prüfung (XrKopplungen.Pruefen).</summary>
    public static XrKopplung? Anmeldung(HttpContext ctx, XrKopplungen kopplungen) =>
        ctx.Features.Get<XrAnmeldung>()?.Kopplung ?? kopplungen.Pruefen(ctx.Request.Headers.Authorization.ToString());

    /// <summary>EINE Stelle für Pfadsperre und Schlüssel im Netzzugang. null = durchlassen, sonst (Status, Fehler, Vorschlag).</summary>
    public static (int Code, string Fehler, string? Vorschlag)? Pruefen(HttpContext ctx, XrKopplungen kopplungen, XrBremse anfragen, XrBremse fehlversuche, long koerperMax)
    {
        var r = ctx.Request;
        var absender = ctx.Connection.RemoteIpAddress?.ToString() ?? "unbekannt";
        if (!PfadErlaubt(r.Path)) return (404, "Nicht gefunden.", null);
        if (!fehlversuche.Frei(absender)) return (429, "Zu viele Fehlversuche von dieser Adresse.", "Später erneut versuchen; die Brille neu koppeln, wenn der Schlüssel nicht mehr gilt.");
        if (!anfragen.Nehmen(absender)) return (429, "Zu viele Anfragen.", "Langsamer anfragen.");
        var freigabe = ctx.GetEndpoint()?.Metadata.GetMetadata<XrNetzFreigabe>();
        if (freigabe is not { OhneSchluessel: true })
        {
            var kopplung = kopplungen.Pruefen(r.Headers.Authorization.ToString());
            if (kopplung is null)
            {
                fehlversuche.Nehmen(absender);
                return (401, "Schlüssel fehlt oder gilt nicht.", "Die Brille mit KICAD koppeln (Kopplungspaket oder Code am PC).");
            }
            ctx.Features.Set(new XrAnmeldung(kopplung));
        }
        if (freigabe is null) return (404, "Nicht gefunden.", null);
        if (r.ContentLength > koerperMax) return (413, "Die Anfrage ist zu gross.", null);   // ohne Längenangabe begrenzt Kestrel (MaxRequestBodySize)
        if (Herkunftsschutz.Aendernd(r.Method) && !Herkunftsschutz.InhaltstypErlaubt(r))
            return (415, "Inhaltstyp nicht erlaubt – als application/json senden.", null);
        return null;
    }
}

/// <summary>Startet und beendet den Netzzugang (HTTPS) und die Suche (UDP) nach der Einstellung xr.zugang.</summary>
public sealed class XrNetzDienst : IHostedService
{
    readonly IServiceProvider dienste;
    readonly XrEinstellungen einstellungen;
    readonly XrHauptPort haupt;
    readonly BenutzerEinstellungen benutzer;
    readonly XrZertifikat zertifikat;
    readonly XrKopplungen kopplungen;
    readonly XrKanal kanal;
    readonly IHostApplicationLifetime lebenszeit;
    readonly XrBremse anfragen, fehlversuche;
    readonly SemaphoreSlim sperre = new(1, 1);
    WebApplication? server;
    XrSuche? suche;
    Timer? takt;
    bool beendet;
    int? port, suchPort;
    string? fehler, sucheFehler;
    DateTimeOffset? seit;

    public XrNetzDienst(IServiceProvider dienste, XrEinstellungen einstellungen, XrHauptPort haupt, ProjektDienst projekt, XrZertifikat zertifikat,
        XrKopplungen kopplungen, XrKanal kanal, IHostApplicationLifetime lebenszeit)
    {
        this.dienste = dienste;
        this.einstellungen = einstellungen;
        this.haupt = haupt;
        benutzer = projekt.Benutzer;
        this.zertifikat = zertifikat;
        this.kopplungen = kopplungen;
        this.kanal = kanal;
        this.lebenszeit = lebenszeit;
        anfragen = new XrBremse(einstellungen.AnfragenJeSekunde, einstellungen.AnfragenVorrat, einstellungen.Jetzt);
        fehlversuche = new XrBremse(einstellungen.FehlversucheMax / Math.Max(1, einstellungen.FehlversucheFenster.TotalSeconds), einstellungen.FehlversucheMax, einstellungen.Jetzt);
    }

    /// <summary>Läuft der Netzzugang gerade?</summary>
    public bool Aktiv => server is not null;

    // ---------------------------------------------------------------- Lebenslauf

    public Task StartAsync(CancellationToken abbruch)
    {
        // erst nach dem Start des Hauptservers: dann stehen dessen Endpunkte fest
        lebenszeit.ApplicationStarted.Register(() => _ = Task.Run(() => Abgleichen()));
        takt = new Timer(_ => _ = Abgleichen(), null, einstellungen.PruefAbstand, einstellungen.PruefAbstand);   // Änderung an einem anderen Rechner oder in den Einstellungen
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken abbruch)
    {
        if (takt is not null) await takt.DisposeAsync();
        await sperre.WaitAsync(CancellationToken.None);
        try { beendet = true; await Stoppen(); }
        finally { sperre.Release(); }
    }

    /// <summary>Bringt den Netzzugang in den Zustand der Einstellung (starten bzw. beenden).</summary>
    public async Task Abgleichen()
    {
        await sperre.WaitAsync();
        try
        {
            if (beendet) return;
            var hindernis = Hindernis(out var soll);
            if (!soll || hindernis is not null)
            {
                await Stoppen();
                fehler = hindernis;
            }
            else if (server is null) await Starten();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException)
        {
            fehler = "Der XR-Zugang liess sich nicht abgleichen: " + ex.Message;
        }
        finally { sperre.Release(); }
    }

    /// <summary>Einstellung xr.zugang setzen und sofort abgleichen (POST /api/xr/zugang, nur localhost). Wirft HlksFehler, wenn die Einstellung nicht gespeichert wird.</summary>
    public async Task Schalten(bool an)
    {
        benutzer.Setzen(XrEinstellungen.ZugangSchluessel, JsonValue.Create(an));
        await Abgleichen();
    }

    public bool EinstellungAn() => benutzer.Hole(XrEinstellungen.ZugangSchluessel) is JsonValue v && v.TryGetValue<bool>(out var an) && an;

    int PortAusEinstellung(int? fest, string schluessel, int vorgabe) =>
        fest ?? (benutzer.Hole(schluessel) is JsonValue v && v.TryGetValue<int>(out var p) && p is >= 1024 and <= 65535 ? p : vorgabe);

    int KonfigPort => PortAusEinstellung(einstellungen.Port, XrEinstellungen.PortSchluessel, XrEinstellungen.PortVorgabe);
    int KonfigSuchPort => PortAusEinstellung(einstellungen.SuchPort, XrEinstellungen.SuchPortSchluessel, XrEinstellungen.SuchPortVorgabe);

    /// <summary>Soll der Zugang laufen, und was steht dem entgegen (null = nichts)?</summary>
    string? Hindernis(out bool soll)
    {
        soll = EinstellungAn();
        if (!soll) return null;
        if (einstellungen.DesktopPort is { } d && haupt.Port != d)
            return $"Der XR-Zugang öffnet sich nur im KICAD auf Port {d} (Desktop). Dieses KICAD läuft auf Port {haupt.Port} (Test- oder Abnahmeserver).";
        if (KonfigPort != 0 && (KonfigPort == haupt.Port || KonfigPort == KonfigSuchPort))
            return $"Der Port {KonfigPort} des XR-Zugangs ist schon vergeben (Oberfläche oder Suche).";
        return null;
    }

    async Task Starten()
    {
        X509Certificate2 zert;
        try { zert = zertifikat.Holen(); }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            fehler = "Das Zertifikat des XR-Zugangs liess sich nicht laden oder erzeugen: " + ex.Message;
            return;
        }
        var p = KonfigPort;
        WebApplication? app = null;
        try
        {
            app = Bauen(zert, p);
            await app.StartAsync();
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or InvalidOperationException or AuthenticationException)
        {
            if (app is not null) await app.DisposeAsync();
            fehler = $"Der XR-Zugang liess sich auf Port {p} nicht öffnen (belegt?): {ex.Message}";
            return;
        }
        server = app;
        port = GebundenerPort(app) ?? p;
        seit = einstellungen.Jetzt();
        fehler = null;
        sucheFehler = null;
        var s = new XrSuche(kopplungen, Info, einstellungen);
        try
        {
            s.Starten(einstellungen.Adresse, KonfigSuchPort);
            suche = s;
            suchPort = s.Port;
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            s.Dispose();
            sucheFehler = $"Die Suche im Netz liess sich auf UDP-Port {KonfigSuchPort} nicht öffnen (belegt?): {ex.Message}. Die Brille findet den PC dann nur über die gespeicherte Adresse.";
        }
    }

    async Task Stoppen()
    {
        suche?.Dispose();
        suche = null;
        suchPort = null;
        sucheFehler = null;
        if (server is null) return;
        kanal.AlleTrennen();
        using (var frist = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            try { await server.StopAsync(frist.Token); }
            catch (OperationCanceledException) { /* Verbindungen werden abgebrochen */ }
        }
        await server.DisposeAsync();
        server = null;
        port = null;
        seit = null;
    }

    static int? GebundenerPort(WebApplication app)
    {
        var adressen = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var erste = adressen?.FirstOrDefault();
        return erste is not null && Uri.TryCreate(erste, UriKind.Absolute, out var u) ? u.Port : null;
    }

    // ---------------------------------------------------------------- Der Server im Netz

    WebApplication Bauen(X509Certificate2 zert, int p)
    {
        var b = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        b.Logging.SetMinimumLevel(LogLevel.Warning);
        // keine Adressen aus Umgebung oder Konfiguration (ASPNETCORE_URLS, HTTP_PORTS …): nur der eine Endpunkt unten
        b.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "");
        b.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, "");
        b.WebHost.UseSetting(WebHostDefaults.HttpsPortsKey, "");
        b.WebHost.ConfigureKestrel(k =>
        {
            k.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
            k.AddServerHeader = false;
            k.Limits.MaxConcurrentConnections = einstellungen.VerbindungenMax;
            k.Limits.MaxConcurrentUpgradedConnections = einstellungen.TeilnehmerMax * 2;
            k.Limits.MaxRequestBodySize = einstellungen.KoerperMax;
            k.Limits.MaxRequestHeaderCount = 32;
            k.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
            k.Limits.MaxRequestLineSize = 4 * 1024;
            k.Limits.MaxRequestBufferSize = 64 * 1024;
            k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            k.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
            k.Listen(einstellungen.Adresse, p, l =>
            {
                l.Protocols = HttpProtocols.Http1;
                l.UseHttps(new HttpsConnectionAdapterOptions
                {
                    ServerCertificate = zert,
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateMode = ClientCertificateMode.NoCertificate,
                    CheckCertificateRevocation = false,
                    HandshakeTimeout = TimeSpan.FromSeconds(10),
                });
            });
        });
        var app = b.Build();
        var hauptEndpunkte = dienste.GetRequiredService<EndpointDataSource>();
        app.UseRouting();   // wählt nur unter den freigegebenen Endpunkten (DataSources unten)
        app.Use(async (ctx, weiter) =>
        {
            if (XrPforte.Pruefen(ctx, kopplungen, anfragen, fehlversuche, einstellungen.KoerperMax) is { } nein)
            {
                ctx.Response.StatusCode = nein.Code;
                if (nein.Code == 401) ctx.Response.Headers.WWWAuthenticate = "Bearer";
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsync(Json.Text(new { fehler = nein.Fehler, vorschlag = nein.Vorschlag }));
                return;
            }
            await weiter(ctx);
        });
        // Die Endpunkte gehören zum Hauptserver: Dienste für die Anfrage aus dessen Container
        app.Use(async (ctx, weiter) =>
        {
            await using var bereich = dienste.CreateAsyncScope();
            ctx.RequestServices = bereich.ServiceProvider;
            await weiter(ctx);
        });
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15), KeepAliveTimeout = TimeSpan.FromSeconds(45) });
        ((IEndpointRouteBuilder)app).DataSources.Add(new DefaultEndpointDataSource(XrPforte.Freigegeben(hauptEndpunkte.Endpoints)));
        return app;
    }

    // ---------------------------------------------------------------- Auskunft

    /// <summary>Die IPv4-Adressen dieses PCs im Büronetz (echte LAN-/WLAN-Karten mit privater Adresse; ohne VPN, TAP, virtuelle Adapter).</summary>
    public IReadOnlyList<string> Adressen()
    {
        if (!einstellungen.Adresse.Equals(IPAddress.Any)) return [einstellungen.Adresse.ToString()];
        return [.. new QuestNetzEcht().Schnittstellen().Where(QuestDienst.EchteSchnittstelle).Select(s => s.Ip).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Angaben für Kopplungspaket und Suche (Port: der gebundene, sonst der eingestellte).</summary>
    public XrZugangInfo Info() => new(Environment.MachineName, Adressen(), port ?? KonfigPort, suchPort ?? KonfigSuchPort, zertifikat.Fingerabdruck);

    /// <summary>Zustand für GET /api/xr/zugang (nur localhost; enthält die Codes offener Anfragen, nie Schlüssel).</summary>
    public JsonObject Zustand()
    {
        string? fp = null, kurz = null, bis = null, zertFehler = null;
        try
        {
            fp = zertifikat.Fingerabdruck;
            kurz = XrZertifikat.Kurz(fp);
            bis = zertifikat.GueltigBis.ToString("o");
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException) { zertFehler = ex.Message; }
        var verbundene = kanal.TeilnehmerListe().Select(t => (string?)t?["kopplung"]?.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        return new JsonObject
        {
            ["an"] = EinstellungAn(),
            ["aktiv"] = server is not null,
            ["adresse"] = einstellungen.Adresse.ToString(),
            ["port"] = port ?? KonfigPort,
            ["suchPort"] = suchPort ?? KonfigSuchPort,
            ["sucheAktiv"] = suche is not null,
            ["seit"] = seit?.ToString("o"),
            ["pc"] = Environment.MachineName,
            ["ips"] = new JsonArray([.. Adressen().Select(i => (JsonNode)i)]),
            ["fingerabdruck"] = fp,
            ["fingerabdruckKurz"] = kurz,
            ["zertifikatBis"] = bis,
            ["zertifikatErsetzt"] = zertifikat.Ersetzt,
            ["fehler"] = fehler ?? (zertFehler is null ? null : "Zertifikat: " + zertFehler),
            ["hinweis"] = sucheFehler,
            ["kopplungen"] = new JsonArray([.. kopplungen.Liste().Select(k => (JsonNode)new JsonObject
            {
                ["id"] = k.Id, ["brille"] = k.Brille, ["name"] = k.Name, ["weg"] = k.Weg, ["angelegt"] = k.Angelegt.ToString("o"),
                ["zuletzt"] = kopplungen.Zuletzt(k.Id)?.ToString("o"), ["verbunden"] = verbundene.Contains(k.Id),
            })]),
            ["anfragen"] = new JsonArray([.. kopplungen.OffeneAnfragen().Select(a => (JsonNode)new JsonObject
            {
                ["anfrage"] = a.Anfrage, ["brille"] = a.Brille, ["name"] = a.Name, ["ip"] = a.Ip, ["code"] = a.Code, ["bis"] = a.Bis.ToString("o"),
                ["bestaetigt"] = a.Bestaetigt, ["abgelehnt"] = a.Abgelehnt, ["versuche"] = a.Versuche,
            })]),
            ["teilnehmer"] = kanal.Anzahl,
        };
    }
}

public static class XrZugangDienste
{
    /// <summary>Dienste des XR-Zugangs (Program.cs, eine Zeile). hauptPort: der localhost-Port dieses KICAD (für die Sperre auf den Desktop-Port).</summary>
    public static IServiceCollection AddXrZugang(this IServiceCollection s, int hauptPort)
    {
        s.TryAddSingleton(XrEinstellungen.Vorgabe());   // Tests registrieren eine eigene Fassung (ConfigureTestServices), die dann gilt
        s.AddSingleton(new XrHauptPort(hauptPort));
        s.TryAddSingleton(sp => new VrAuftragSpeicher(sp.GetRequiredService<ProjektDienst>().Benutzer));
        s.AddSingleton<XrZertifikat>();
        s.AddSingleton<XrKopplungen>();
        s.AddSingleton<XrKanal>();
        s.AddSingleton<XrNetzDienst>();
        s.AddHostedService(sp => sp.GetRequiredService<XrNetzDienst>());
        return s;
    }
}
