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
	return float4(saturate(rgb), saturate(a));
}
