// L'eau, sous le terrain : une teinte selon la profondeur (de l'image d'altitude), des rides qui passent lentement, et
// l'écume qui va et vient le long de la côte. Scène animée : rendue à chaque image par la carte graphique.
shader_type canvas_item;

#include "res://shaders/carte/relief.gdshaderinc"
#include "res://shaders/carte/bruit.gdshaderinc"

uniform vec3 shallow_color = vec3(0.55, 0.77, 0.82);
uniform vec3 deep_color = vec3(0.20, 0.42, 0.60);
uniform float deep_at = 60.0;                        // la profondeur (m) de la teinte la plus sombre
uniform vec3 board_color = vec3(0.16, 0.15, 0.14);   // le plateau sous la maquette, hors de la carte

uniform float ripple_scale = 0.06;      // la taille des rides (plus petit : plus grandes), par lieue
uniform float ripple_speed = 0.35;      // leur vitesse
uniform float ripple_strength = 0.10;   // leur éclat
uniform vec3 foam_color = vec3(0.93, 0.96, 0.95);
uniform float foam_depth = 3.0;         // l'écume jusqu'à cette profondeur (m)
uniform float foam_speed = 1.2;

void vertex() {
	screen_pos = VERTEX;
}

void fragment() {
	vec2 world = to_world(screen_pos);
	float depth = max(-altitude_at(world), 0.0);
	vec3 water = mix(shallow_color, deep_color, smoothstep(0.0, deep_at, depth));

	// Les rides : les crêtes fines de deux bruits étirés dans le sens du vent, qui glissent l'un sur l'autre.
	vec2 p = world * ripple_scale * vec2(1.0, 2.6);
	float n1 = noise(p + vec2(TIME * ripple_speed, 0.0));
	float n2 = noise(p * 1.9 + vec2(-TIME * ripple_speed * 0.7, 3.0));
	float crest = pow(1.0 - abs(n1 - 0.5) * 2.0, 10.0) * 0.6 + pow(1.0 - abs(n2 - 0.5) * 2.0, 14.0) * 0.4;
	water += vec3(crest * ripple_strength);

	// L'écume : une bande claire près de la côte qui avance et recule.
	float wave = 0.5 + 0.5 * sin(TIME * foam_speed - depth * 2.2 + noise(world * 0.08) * 4.0);
	float foam = (1.0 - smoothstep(0.0, foam_depth, depth)) * smoothstep(0.55, 0.95, wave) * 0.6;
	water = mix(water, foam_color, foam);

	COLOR = vec4(outside_map(world) ? board_color : water, 1.0);
}
