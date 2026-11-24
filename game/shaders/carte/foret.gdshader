// Les forêts (24 novembre 2026) : une canopée peinte par la carte graphique au lieu d'un arbre par image. Trois échelles de
// bosses : les couronnes des arbres (~160 m), les bosquets (~800 m) et les massifs (~4 km). Chacune s'efface quand elle
// devient plus petite que quelques pixels à l'écran (pas de scintillement de loin). La lumière vient du nord-ouest, comme
// pour le relief, et suit la pente de la canopée ; au-dessus de 900 m, les conifères, plus sombres et plus bleus.
// Le coût ne dépend que des pixels à l'écran, pas de la taille de la carte.
shader_type canvas_item;

#include "res://shaders/carte/relief.gdshaderinc"
#include "res://shaders/carte/bruit.gdshaderinc"

// 1 : la forêt pleine ; moins : estompée (sous les filtres Politique et Production).
uniform float strength = 1.0;
uniform float conifer_altitude = 900.0;
uniform bool has_relief = false;

const vec3 LEAFY = vec3(0.30, 0.45, 0.22);
const vec3 LEAFY_LIGHT = vec3(0.47, 0.60, 0.30);
const vec3 CONIFER = vec3(0.17, 0.31, 0.24);
const vec3 CONIFER_LIGHT = vec3(0.29, 0.43, 0.32);
const float CROWN = 1.6;      // unités de carte (100 m)
const float GROVE = 8.0;
const float MASSIF = 40.0;

void vertex() {
	screen_pos = VERTEX;
}

vec2 hash2(vec2 p) {
	return vec2(hash(p), hash(p + vec2(37.0, 91.0)));
}

// Les couronnes : l'arbre le plus proche (un par case, un peu décalé, de taille variable) : la position du point par
// rapport à son centre, rapportée à son rayon (xy), la distance rapportée au rayon (z) et son numéro (w).
vec4 crowns(vec2 p) {
	vec2 cell = floor(p);
	vec4 best = vec4(0.0, 0.0, 9.0, 0.0);
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 c = cell + vec2(float(x), float(y));
			vec2 centre = c + 0.2 + 0.6 * hash2(c);
			float radius = 0.5 + 0.25 * hash(c + vec2(13.0, 7.0));
			vec2 rel = (p - centre) / radius;
			float d = length(rel);
			if (d < best.z)
				best = vec4(rel, d, hash(c + vec2(5.0, 3.0)));
		}
	}
	return best;
}

// Le poids d'une échelle : 0 quand elle fait moins de 2 pixels à l'écran, 1 à partir de 6.
float visible(float size) {
	return smoothstep(2.0, 6.0, size * zoom);
}

void fragment() {
	vec2 world = to_world(screen_pos);
	vec4 c = crowns(world / CROWN);
	float w_crown = visible(CROWN);
	float w_grove = visible(GROVE);

	// Chaque couronne est une boule éclairée du nord-ouest (calculée exactement, quel que soit le zoom) ; entre les
	// couronnes, l'ombre du sous-bois.
	vec3 normal = normalize(vec3(c.xy, sqrt(max(1.0 - c.z * c.z, 0.04))));
	float crown_light = c.z < 1.0 ? clamp(dot(normal, normalize(vec3(-1.0, -1.0, 1.1))), 0.0, 1.0) : 0.0;
	float crown_shade = c.z < 1.0 ? mix(0.62, 1.18, crown_light) : 0.5;

	// La hauteur de la canopée : les bosquets et les massifs (leur pente à l'écran donne la lumière).
	float grove = fbm(world / GROVE);
	float massif = fbm(world / MASSIF + vec2(11.0, 4.0));
	float height = w_grove * grove * 1.6 + massif * 1.2;

	// La pente à l'écran donne la lumière (nord-ouest) ; elle tient compte toute seule du zoom.
	vec2 slope = vec2(dFdx(height), dFdy(height));
	float lit = clamp(0.5 - dot(slope, normalize(vec2(1.0, 1.0))) * 4.0, 0.0, 1.0);

	float altitude = has_relief ? altitude_at(world) : 0.0;
	float conifer = smoothstep(conifer_altitude - 120.0, conifer_altitude + 120.0, altitude);
	vec3 dark = mix(LEAFY, CONIFER, conifer);
	vec3 light = mix(LEAFY_LIGHT, CONIFER_LIGHT, conifer);

	// Un ton par arbre et par bosquet, puis le volume de chaque arbre quand on le voit.
	float tone = 0.45 + 0.25 * massif + 0.2 * w_grove * (grove - 0.5) + 0.3 * w_crown * (c.w - 0.5);
	vec3 col = mix(dark, light, clamp(tone + (lit - 0.5) * 0.9, 0.0, 1.0));
	col *= mix(1.0, crown_shade, w_crown);
	if (has_relief)
		col *= mix(1.0, light_at(world), 0.6);
	COLOR = vec4(col, strength);
}
