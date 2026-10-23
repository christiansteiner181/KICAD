// Quest App P4b (Modul XR, XR-H Sitzungsraum): das Modell mit Etagenschnitt bzw. Ausschnitt («Raum zeigen (VR)»). Licht wie das Standardmaterial von StereoKit
// (sk_lighting, Farbe × Farbe der Instanz), dazu ein Quader in den Koordinaten der Szene (vor der Verkleinerung auf den Tisch): u = (x, z)·schnitt_u.xy in
// [schnitt_u.z, schnitt_u.w], v = (x, z)·schnitt_v.xy in [schnitt_v.z, schnitt_v.w], y in [schnitt_y.x, schnitt_y.y]. Was ausserhalb liegt, wird verworfen.
// Die Werte rechnet Hlks.Xr.Sitzung.SchnittQuader (ShaderWerte). Die App braucht den Shader nur für Blöcke, die den Quader schneiden; Blöcke ganz innen zeichnet
// sie mit dem gewöhnlichen Material (frühe Tiefenprüfung bleibt), Blöcke ganz aussen gar nicht.
#include <stereokit.hlsli>

//--name = app/schnitt

//--color:color = 1,1,1,1
float4 color;
//--schnitt_u = 1,0,-1000000,1000000
float4 schnitt_u;
//--schnitt_v = 0,1,-1000000,1000000
float4 schnitt_v;
//--schnitt_y = -1000000,1000000,0,0
float4 schnitt_y;

struct vsIn {
	float4 pos  : SV_Position;
	float3 norm : NORMAL0;
	float4 col  : COLOR0;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float4 color : COLOR0;
	float3 lokal : TEXCOORD0;
	uint view_id : SV_RenderTargetArrayIndex;
};

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn o;
	o.view_id = id % sk_view_count;
	id        = id / sk_view_count;

	float4 world = mul(float4(input.pos.xyz, 1), sk_inst[id].world);
	o.pos        = mul(world, sk_viewproj[o.view_id]);

	float3 normal = normalize(mul(input.norm, (float3x3)sk_inst[id].world));
	o.lokal       = input.pos.xyz;
	o.color       = color * input.col * sk_inst[id].color;
	o.color.rgb  *= sk_lighting(normal);
	return o;
}

float4 ps(psIn input) : SV_TARGET {
	float u = dot(input.lokal.xz, schnitt_u.xy);
	float v = dot(input.lokal.xz, schnitt_v.xy);
	if (u < schnitt_u.z || u > schnitt_u.w || v < schnitt_v.z || v > schnitt_v.w || input.lokal.y < schnitt_y.x || input.lokal.y > schnitt_y.y)
		discard;
	return input.color;
}
