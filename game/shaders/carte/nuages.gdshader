// Les nuages : quelques nuages clairs qui passent lentement au-dessus de la maquette, et leur ombre sur le sol
// (décalée vers le sud-est : la lumière vient du nord-ouest). Scène animée : rendue à chaque image.
shader_type canvas_item;

#include "res://shaders/carte/relief.gdshaderinc"
#include "res://shaders/carte/bruit.gdshaderinc"

uniform float cloud_size = 180.0;           // la taille d'un nuage (unités de carte)
uniform vec2 wind = vec2(5.0, -2.0);        // leur vitesse (unités de carte par seconde)
uniform float coverage = 0.64;              // plus haut : moins de nuages
uniform float cloud_opacity = 0.55;
uniform float shadow_opacity = 0.18;
uniform vec2 shadow_offset = vec2(40.0, 55.0);   // unités
uniform vec3 cloud_color = vec3(0.98, 0.98, 0.97);
uniform vec3 shadow_color = vec3(0.10, 0.12, 0.16);

void vertex() {
	screen_pos = VERTEX;
}

float density(vec2 world) {
	vec2 p = (world - wind * TIME) / cloud_size;
	return smoothstep(coverage, coverage + 0.12, fbm(p));
}

void fragment() {
	vec2 world = to_world(screen_pos);
	float cloud = density(world) * cloud_opacity;
	float shadow = density(world - shadow_offset) * shadow_opacity * (1.0 - cloud);
	float inside = outside_map(world) ? 0.0 : 1.0;
	cloud *= inside;
	shadow *= inside;
	float alpha = cloud + shadow;
	vec3 color = alpha > 0.0 ? (cloud_color * cloud + shadow_color * shadow) / alpha : cloud_color;
	COLOR = vec4(color, alpha);
}
