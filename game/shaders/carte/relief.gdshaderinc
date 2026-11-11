// Ce que partagent les shaders de la carte : la caméra et la carte d'altitude (game/data/relief-altitude.png).
// Un point de l'écran (en pixels) est sur la carte en (écran − pan) / zoom, en lieues.

uniform vec2 pan = vec2(0.0);
uniform float zoom = 1.0;
uniform sampler2D altitude_tex : filter_nearest, repeat_disable;
uniform sampler2D shade_tex : filter_linear, repeat_disable;
uniform vec2 texel_count = vec2(1.0);
uniform float cell_size = 2.0;

varying vec2 screen_pos;

vec2 to_world(vec2 screen) {
	return (screen - pan) / zoom;
}

// L'altitude d'un pixel de l'image : rouge et vert sur 16 bits, en décimètres au-dessus de −1 000 m.
float altitude_texel(ivec2 p) {
	p = clamp(p, ivec2(0), ivec2(texel_count) - 1);
	vec4 t = texelFetch(altitude_tex, p, 0);
	return (floor(t.r * 255.0 + 0.5) * 256.0 + floor(t.g * 255.0 + 0.5)) / 10.0 - 1000.0;
}

// L'altitude en un point de la carte (lieues), interpolée entre les quatre pixels voisins (après le décodage : on ne
// peut pas laisser la carte graphique mélanger les octets).
float altitude_at(vec2 world) {
	vec2 f = world / cell_size - 0.5;
	vec2 i = floor(f);
	vec2 t = f - i;
	ivec2 p = ivec2(i);
	float top = mix(altitude_texel(p), altitude_texel(p + ivec2(1, 0)), t.x);
	float bottom = mix(altitude_texel(p + ivec2(0, 1)), altitude_texel(p + ivec2(1, 1)), t.x);
	return mix(top, bottom, t.y);
}

// L'éclairement du relief en un point (1 sur un terrain plat).
float light_at(vec2 world) {
	return texture(shade_tex, world / (texel_count * cell_size)).r / 0.7059;
}

// Vrai hors de la carte (au-delà de ses bords) : on y voit le plateau sur lequel la maquette est posée.
bool outside_map(vec2 world) {
	vec2 size = texel_count * cell_size;
	return world.x < 0.0 || world.y < 0.0 || world.x > size.x || world.y > size.y;
}
