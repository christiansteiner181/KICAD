using StereoKit;

namespace Hlks.Quest;

// Quest App, P0 Durchstich (modul-xr.md, «Prototyp: Interface-Labor»): leere App, die zeigt, was Brille und Laufzeit
// hergeben – Gerät, OpenXR-Laufzeit, Darstellung (deckend bzw. Passthrough möglich), Bildrate und erkannte Hände.
// Am PC: «--simulator» erzwingt den Simulator mit Maus-Händen; «--pruefen <bilder>» lässt so viele Bilder laufen, schreibt
// eine Zeile «P0 …» und beendet sich (automatische Prüfung ohne Brille).
class Program
{
	static void Main(string[] args)
	{
		if (!OperatingSystem.IsAndroid())
			Console.OutputEncoding = System.Text.Encoding.UTF8; // Prüfzeile mit Umlauten auch umgeleitet lesbar (auf Android nicht vorhanden)
		bool simulator   = args.Contains("--simulator");
		int  pruefBilder = PruefBilder(args);

		SKSettings einstellungen = new SKSettings
		{
			appName = "KICAD",
			mode    = simulator || pruefBilder > 0 ? AppMode.Simulator : AppMode.XR,
		};
		if (!SK.Initialize(einstellungen))
		{
			Console.Error.WriteLine("P0 Fehler: StereoKit startet nicht");
			Environment.ExitCode = 1;
			return;
		}

		Matrix   bodenLage     = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
		Material bodenMaterial = new Material("floor.hlsl");
		bodenMaterial.Transparency = Transparency.Blend;

		Pose  fensterPose = new Pose(0, 0, -0.5f, Quat.LookDir(0, 0, 1));
		Pose  wuerfelPose = new Pose(0.25f, -0.1f, -0.5f);
		Model wuerfel     = Model.FromMesh(Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.02f), Material.UI);

		int    bilder      = 0;
		double summeZeit   = 0;
		float  mittelZeit  = 0;
		double letzterStatus = 0;

		SK.Run(() =>
		{
			bilder++;
			summeZeit += Time.Stepf;
			mittelZeit = mittelZeit == 0 ? Time.Stepf : mittelZeit * 0.95f + Time.Stepf * 0.05f;

			if (Device.DisplayBlend == DisplayBlend.Opaque)
				Mesh.Cube.Draw(bodenMaterial, bodenLage);

			UI.WindowBegin("KICAD Quest – P0", ref fensterPose, new Vec2(0.32f, 0));
			UI.Label($"Gerät: {Device.Name}");
			UI.Label($"Laufzeit: {Device.Runtime}");
			UI.Label($"Darstellung: {Device.DisplayBlend}");
			UI.Label($"Bildrate: {(mittelZeit > 0 ? 1 / mittelZeit : 0):0} Hz");
			UI.Label($"Hände: links {(Input.Hand(Handed.Left).IsTracked ? "erkannt" : "–")}, rechts {(Input.Hand(Handed.Right).IsTracked ? "erkannt" : "–")}");
			UI.WindowEnd();

			UI.Handle("Würfel", ref wuerfelPose, wuerfel.Bounds);
			wuerfel.Draw(wuerfelPose.ToMatrix());

			// Statuszeile alle 5 s (auf der Brille im Protokoll, adb logcat): Messwerte für K1/K2 ohne Vorlesen.
			if (Time.Total - letzterStatus >= 5)
			{
				letzterStatus = Time.Total;
				Console.WriteLine($"P0 Status: Gerät={Device.Name}, Laufzeit={Device.Runtime}, Darstellung={Device.DisplayBlend}, " +
					$"Bildrate={(mittelZeit > 0 ? 1 / mittelZeit : 0):0} Hz, Hände links={Input.Hand(Handed.Left).IsTracked}, rechts={Input.Hand(Handed.Right).IsTracked}");
			}

			if (pruefBilder > 0 && bilder >= pruefBilder)
			{
				Console.WriteLine($"P0 ok: Bilder={bilder}, mittlere Bildzeit={summeZeit / bilder * 1000:0.0} ms, Gerät={Device.Name}, Laufzeit={Device.Runtime}, Darstellung={Device.DisplayBlend}");
				SK.Quit();
			}
		});
	}

	static int PruefBilder(string[] args)
	{
		int i = Array.IndexOf(args, "--pruefen");
		if (i < 0) return 0;
		return i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n > 0 ? n : 120;
	}
}
