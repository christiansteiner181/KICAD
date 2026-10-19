// Erzeugt die synthetischen Unterlagen des Browser-Tests «Familie aus Unterlagen» (Modul 2, C2). Alle Werte FIKTIV.
//   datenblatt_testventil.pdf  Seite 1: Titel; Tabelle Typ | DN | L | H (Einheiten [mm]) mit V-15 | 15 | 67 | 35, V-20 | 20 | 78 | 46,
//                              V-25 | 25 | 87 | 46; eine Masslinie «L» (Linie bei y = 500 von oben, x 100–300); ein Rechteck (schematisches Massbild)
//   massbild_testventil.dxf    Ansicht vorne von V-15 in mm: Rechteck 70 × 35 (Layer GEHAEUSE) und eine Bemassung von genau 70 mm (Layer MASSE)
//   Der Widerspruch ist Absicht: L von V-15 steht im PDF mit 67, im DXF mit 70 (Toleranz max(1 mm; 0.5 %)).
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using Hlks.Kern;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

var ziel = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
Directory.CreateDirectory(ziel);

// ---------- PDF
var doc = new PdfDocument();
var s1 = doc.AddPage(); s1.Width = XUnit.FromPoint(595); s1.Height = XUnit.FromPoint(842);
using (var g = XGraphics.FromPdfPage(s1))
{
    _ = new PdfZeichner(g, 297);   // richtet die Schriften ein
    var gross = new XFont("Arial", 14); var f = new XFont("Arial", 9);
    g.DrawString("Datenblatt Testventil", gross, XBrushes.Black, 50, 60);
    void T(string t, double x, double y) => g.DrawString(t, f, XBrushes.Black, x, y);
    string[] kopf = ["Typ", "DN", "L", "H"]; double[] xs = [50, 150, 220, 290];
    for (int i = 0; i < 4; i++) T(kopf[i], xs[i], 200);
    for (int i = 1; i < 4; i++) T("[mm]", xs[i], 212);
    string[][] z = [["V-15", "15", "67", "35"], ["V-20", "20", "78", "46"], ["V-25", "25", "87", "46"]];
    for (int r = 0; r < 3; r++)
        for (int i = 0; i < 4; i++) T(z[r][i], xs[i], 230 + 14 * r);
    var pen = new XPen(XColors.Black, 0.5);
    foreach (var y in new[] { 197.0, 227.0, 241.0, 255.0, 269.0 }) g.DrawLine(pen, 45, y, 340, y);
    g.DrawLine(pen, 100, 500, 300, 500);
    g.DrawLine(pen, 100, 480, 100, 505); g.DrawLine(pen, 300, 480, 300, 505);
    g.DrawPolygon(XBrushes.Black, [new XPoint(100, 500), new XPoint(106, 497.5), new XPoint(106, 502.5)], XFillMode.Winding);
    g.DrawPolygon(XBrushes.Black, [new XPoint(300, 500), new XPoint(294, 497.5), new XPoint(294, 502.5)], XFillMode.Winding);
    T("L", 196, 489);
    g.DrawRectangle(pen, 100, 520, 200, 80);
}
doc.Save(Path.Combine(ziel, "datenblatt_testventil.pdf"));

// ---------- DXF
Layer Lay(CadDocument d, string n) { if (d.Layers.TryGetValue(n, out var l)) return l; l = new Layer(n); d.Layers.Add(l); return l; }
Line Li(CadDocument d, double x1, double y1, double x2, double y2) => new(new XYZ(x1, y1, 0), new XYZ(x2, y2, 0)) { Layer = Lay(d, "GEHAEUSE") };
var dxf = new CadDocument();
dxf.Header.InsUnits = UnitsType.Millimeters;
dxf.Entities.Add(Li(dxf, 0, 0, 70, 0)); dxf.Entities.Add(Li(dxf, 70, 0, 70, 35));
dxf.Entities.Add(Li(dxf, 70, 35, 0, 35)); dxf.Entities.Add(Li(dxf, 0, 35, 0, 0));
dxf.Entities.Add(new DimensionLinear { FirstPoint = new XYZ(0, 0, 0), SecondPoint = new XYZ(70, 0, 0), DefinitionPoint = new XYZ(70, -12, 0), Rotation = 0, Layer = Lay(dxf, "MASSE") });
using (var w = new DxfWriter(Path.Combine(ziel, "massbild_testventil.dxf"), dxf, false)) w.Write();

Console.WriteLine("erzeugt in " + ziel);
