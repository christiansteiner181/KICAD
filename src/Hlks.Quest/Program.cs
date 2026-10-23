using StereoKit;

namespace Hlks.Quest;

// Quest App (Modul XR). P0: Durchstich (Gerät, Laufzeit, Hände); P4a: verbinden, Modell laden und zwischenspeichern, Begehen mit den Händen (QuestApp).
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
			appName = "KICAD",
			mode    = optionen.Simulator || optionen.PruefBilder > 0 ? AppMode.Simulator : AppMode.XR,
			origin  = OriginMode.Floor,   // Boden des Raums ist y = 0 (Rig auf OK Boden, VrLogik); ohne Unterstützung: lokal mit −1.5 m
		};
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
