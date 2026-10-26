using StereoKit;

namespace Hlks.Quest;

// Quest App (Modul XR), seit H+K XR E1 die App «H+K XR» für alle Werkzeuge (QuestApp.Werkzeuge.cs). P0: Durchstich (Gerät, Laufzeit, Hände); P4a: verbinden, Modell laden und zwischenspeichern, Begehen mit den Händen (QuestApp);
// P4b: Sitzungsraum mit Passthrough «Echter Tisch» und Ebenen der Fachmodule.
// Am PC: «--simulator» erzwingt den Simulator mit Maus-Händen; «--pruefen <bilder>» lässt so viele Bilder laufen, schreibt eine Zeile «P4 ok …» bzw.
// «P4 Fehler …» und beendet sich (automatische Prüfung ohne Brille; mit «--kopplung <datei>» mit Modell). Weitere Argumente: Optionen.cs.
class Program
{
	static void Main(string[] args)
	{
		if (!OperatingSystem.IsAndroid())
			Console.OutputEncoding = System.Text.Encoding.UTF8; // Prüfzeile mit Umlauten auch umgeleitet lesbar (auf Android nicht vorhanden)
		var optionen = Optionen.Lesen(args);

		SKSettings einstellungen = new SKSettings
		{
			appName = Hlks.Xr.Netz.XrApp.Name,   // H+K XR E1: eine App für alle Werkzeuge («H+K XR»; bis E1 je Marke «KICAD» bzw. «XR-Labor»)
			mode    = optionen.Simulator || optionen.PruefBilder > 0 ? AppMode.Simulator : AppMode.XR,
			origin  = OriginMode.Floor,   // Boden des Raums ist y = 0 (Rig auf OK Boden, VrLogik); ohne Unterstützung: lokal mit −1.5 m
		};
		// P4b: Shader der App (Assets/schnitt.hlsl, floor.hlsl) am PC neben der DLL suchen – sonst sucht StereoKit «Assets» im Arbeitsordner (quest_pruefen.ps1 startet
		// aus dem Repository); auf der Brille liegen sie in der APK unter «Assets» (Vorgabe).
		if (!OperatingSystem.IsAndroid()) einstellungen.assetsFolder = Path.Combine(AppContext.BaseDirectory, "Assets");
		// P4b: Passthrough (XR_FB_passthrough) für «Echter Tisch» im Sitzungsraum – die Erweiterung muss vor SK.Initialize angefordert werden (Passthrough.cs)
		if (einstellungen.mode == AppMode.XR) Passthrough.Anfordern();
		if (!SK.Initialize(einstellungen))
		{
			Console.Error.WriteLine("P4 Fehler: StereoKit startet nicht");
			Environment.ExitCode = 1;
			return;
		}

		var app = new QuestApp(optionen);
		app.Starten();
		SK.Run(app.Bild);
		app.Beenden();
		Environment.ExitCode = app.ExitCode;
	}
}
