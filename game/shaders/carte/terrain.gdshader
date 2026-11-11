// Le terrain de la vue Monde : une teinte selon l'altitude, assombrie ou éclairée par les ombres du relief. La mer est
// transparente (l'eau animée est dessous). Fond fixe : rendu seulement quand la caméra change.
shader_type canvas_item;

#include "res://shaders/carte/relief.gdshaderinc"

// Les teintes du terrain, de la côte aux neiges (altitude en mètres) : une maquette peinte, un peu saturée.
const float STOPS[7] = float[](0.0, 150.0, 400.0, 800.0, 1400.0, 2000.0, 2400.0);
const vec3 COLORS[7] = vec3[](
	vec3(0.58, 0.74, 0.40),   // plaine côtière
	vec3(0.65, 0.76, 0.43),   // prairies
	vec3(0.78, 0.76, 0.50),   // herbes sèches
	vec3(0.76, 0.64, 0.44),   // ocre des piémonts
	vec3(0.60, 0.50, 0.40),   // roche brune
	vec3(0.58, 0.56, 0.54),   // roche grise
	vec3(0.95, 0.95, 0.93));  // neiges

// La force des ombres (0 : aucune, 1 : celles de l'image).
uniform float shade_strength = 1.0;

void vertex() {
	screen_pos = VERTEX;
}

vec3 ramp(float a) {
	vec3 c = COLORS[0];
	for (int k = 0; k < 6; k++) {
		float t = clamp((a - STOPS[k]) / (STOPS[k + 1] - STOPS[k]), 0.0, 1.0);
		c = a >= STOPS[k] ? mix(COLORS[k], COLORS[k + 1], t) : c;
	}
	return c;
}

void fragment() {
	vec2 world = to_world(screen_pos);
	float a = altitude_at(world);
	// La côte : nette et lissée sur un pixel d'écran (calculé avant tout branchement).
	float edge = max(fwidth(a), 0.001);
	float land = outside_map(world) ? 0.0 : smoothstep(-0.5 * edge, 0.5 * edge, a + 0.5);
	vec3 c = ramp(max(a, 0.0)) * mix(1.0, light_at(world), shade_strength);
	COLOR = vec4(c, land);
}
