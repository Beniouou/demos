// L'effet maquette (« tilt-shift »), posé sur l'image de la scène : une bande nette au milieu de l'écran, un flou qui
// grandit vers le haut et vers le bas, et des couleurs un peu plus saturées. Les étiquettes sont dessinées après,
// par-dessus : elles restent nettes. Force 0 : l'image telle quelle (vue avec des filtres d'information).
shader_type canvas_item;

uniform float strength = 1.0;        // 0 : aucun effet, 1 : l'effet entier
uniform float focus_center = 0.52;   // le milieu de la bande nette (0 en haut, 1 en bas de l'écran)
uniform float focus_half = 0.16;     // sa demi-hauteur
uniform float blur_band = 0.30;      // le flou atteint son maximum à cette distance au-delà de la bande
uniform float max_blur = 4.0;        // le flou le plus fort (pixels)
uniform float saturation = 1.22;
uniform float contrast = 1.05;

void fragment() {
	float d = clamp((abs(UV.y - focus_center) - focus_half) / blur_band, 0.0, 1.0);
	float radius = d * d * max_blur * strength;
	vec4 sum = texture(TEXTURE, UV);
	float weight = 1.0;
	if (radius > 0.05) {
		// Deux anneaux de six points autour du pixel, le second tourné d'un demi-pas.
		for (int k = 0; k < 6; k++) {
			float a = float(k) * 1.0472;
			vec2 outer = vec2(cos(a), sin(a)) * radius;
			vec2 inner = vec2(cos(a + 0.5236), sin(a + 0.5236)) * radius * 0.5;
			sum += texture(TEXTURE, UV + outer * TEXTURE_PIXEL_SIZE) * 0.7;
			sum += texture(TEXTURE, UV + inner * TEXTURE_PIXEL_SIZE);
			weight += 1.7;
		}
	}
	vec3 c = sum.rgb / weight;
	float gray = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(gray), c, mix(1.0, saturation, strength));
	c = (c - 0.5) * mix(1.0, contrast, strength) + 0.5;
	COLOR = vec4(clamp(c, 0.0, 1.0), 1.0);
}
