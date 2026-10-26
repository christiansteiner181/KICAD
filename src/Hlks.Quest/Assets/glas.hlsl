// Quest App V1a: Glas im Stil von visionOS (Apple HIG «Materials», visionOS: durchscheinend, neutral, stark gerundete Ecken, feiner Glanz an der Oberkante, weicher
// Schatten; Hlks.Xr.Bedienung.Glas, GlasStil). Ein Rechteck je Netz (GlasZeichner.cs): uv = Lage im Rechteck in m (Mitte 0), Normale = (halbe Breite, halbe Höhe,
// Eckenradius) in m – so bleibt der Eckenradius bei jeder Grösse gleich, ohne Textur. Die Farbe der Instanz ist die Tönung (sRGB umgerechnet, Alpha = Deckkraft).
// art.x: 0 = Fläche (Glas bzw. dünnes Material), 1 = Schatten (weich ausserhalb), 2 = Leuchten am Rand (Bodenplatte am Tisch); art.y = Glanz an der Oberkante;
// art.z = Weichheit (m) für Schatten und Leuchten; art.w = Aufhellen zum Rand hin.
// Eingang genau wie das Standardmaterial von StereoKit (Position, Normale, UV, Farbe) – sonst vergibt der Übersetzer für OpenGL ES die Stellen falsch (Befund P4b,
// schnitt.hlsl). Keine Weichzeichnung des Hintergrunds (StereoKit 0.3.11 gibt das Bild dahinter nicht her; ein eigener Durchgang wäre zu teuer für die Quest).
#include <stereokit.hlsli>

//--name = app/glas

//--color:color = 1,1,1,1
float4 color;
//--art = 0,0.35,0.02,0.15
float4 art;
// V1e (Christian 24.10.2026: «apps sollen etwas mehr glanz erhalten»): schimmer.x = Stärke («anzeige.glanz», 0 = wie bis V1d) – Lichtsaum am ganzen Rand (oben
// kräftiger, bei runden Scheiben stärker), ein weicher heller Schein oben, der sich mit dem Blickwinkel leicht verschiebt; aufgehellt im «Screen»-Verfahren
// (1 − (1 − Farbe) · (1 − Glanz)), damit helles Pastell nicht überstrahlt und die dunkle Schrift lesbar bleibt.
//--schimmer = 0,0,0,0
float4 schimmer;

struct vsIn {
	float4 pos  : SV_Position;
	float3 norm : NORMAL0;
	float2 uv   : TEXCOORD0;
	float4 col  : COLOR0;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float4 color : COLOR0;
	float2 lage  : TEXCOORD0;
	float3 mass  : TEXCOORD1;
	float2 blick : TEXCOORD2;
	uint view_id : SV_RenderTargetArrayIndex;
};

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn o;
	o.view_id = id % sk_view_count;
	id        = id / sk_view_count;
	float4 world = mul(float4(input.pos.xyz, 1), sk_inst[id].world);
	o.pos   = mul(world, sk_viewproj[o.view_id]);
	o.color = color * input.col * sk_inst[id].color;
	o.lage  = input.uv;
	o.mass  = input.norm;
	// V1e: Richtung zur Kamera in den Achsen der Fläche (x rechts, y oben) – der Schein wandert leicht mit dem Kopf
	float3 ax = normalize(mul(float4(1, 0, 0, 0), sk_inst[id].world).xyz);
	float3 ay = normalize(mul(float4(0, 1, 0, 0), sk_inst[id].world).xyz);
	float3 zk = normalize(sk_camera_pos[o.view_id].xyz - world.xyz);
	o.blick = float2(dot(zk, ax), dot(zk, ay));
	return o;
}

// Abstand zum abgerundeten Rechteck (negativ innen)
float rundRechteck(float2 p, float2 halb, float r) {
	float2 q = abs(p) - halb + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float4 ps(psIn input) : SV_TARGET {
	float2 halb = input.mass.xy;
	float  r    = min(input.mass.z, min(halb.x, halb.y));
	float  d    = rundRechteck(input.lage, halb, r);
	float  aa   = max(fwidth(d), 0.0002);
	float4 c    = input.color;
	if (art.x > 1.5) {
		// Leuchten am Rand: weich zu beiden Seiten der Kante
		float a = exp(-abs(d) / max(art.z, 0.001));
		if (a < 0.01) discard;
		return float4(c.rgb, c.a * a);
	}
	if (art.x > 0.5) {
		// Schatten: weich ausserhalb, innen voll
		float a = 1.0 - smoothstep(-art.z * 0.3, art.z, d);
		if (a < 0.01) discard;
		return float4(c.rgb, c.a * a);
	}
	float innen = 1.0 - smoothstep(-aa, aa, d);
	if (innen < 0.01) discard;
	// leicht heller nach oben, zum Rand hin aufgehellt, feiner Glanz an der Oberkante
	float oben  = saturate(input.lage.y / max(halb.y, 0.001) * 0.5 + 0.5);
	float rand  = 1.0 - smoothstep(0.0, 0.008, -d);
	float glanz = art.y * (1.0 - smoothstep(0.0, 0.0025, abs(d + 0.0012))) * smoothstep(halb.y - r - 0.01, halb.y, input.lage.y);
	float3 rgb  = c.rgb * (0.92 + 0.12 * oben) + art.w * rand + glanz;
	float  a    = c.a * innen + glanz * 0.6 * innen;
	// V1e: Lichtsaum am ganzen Rand (oben kräftiger; runde Scheiben stärker) und weicher Schein oben, mit dem Blickwinkel leicht verschoben
	float g     = schimmer.x;
	float rund  = step(min(halb.x, halb.y) - 0.0005, r);
	float saum  = g * 0.30 * (1.0 + 0.6 * rund) * (1.0 - smoothstep(0.0, 0.0035, abs(d + 0.0016))) * (0.35 + 0.65 * oben);
	float2 mitteSchein = float2(-input.blick.x * halb.x * 0.5, halb.y * (0.42 - 0.25 * input.blick.y));
	float2 ds   = (input.lage - mitteSchein) / max(float2(halb.x * 0.75, halb.y * 0.45), 0.01);
	float schein = g * 0.10 * exp(-dot(ds, ds));
	float licht = saturate(saum + schein);
	rgb = 1.0 - (1.0 - saturate(rgb)) * (1.0 - licht);
	a   = a + (saum * 0.5 + schein * 0.3) * innen;
	return float4(saturate(rgb), saturate(a));
}
