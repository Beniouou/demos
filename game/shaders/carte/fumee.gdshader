// Les fumées des villages : chaque bouffée monte d'une cheminée, grossit, dérive avec le vent et s'efface, puis repart.
// Tout le mouvement est calculé ici, par la carte graphique. Données de chaque bouffée (INSTANCE_CUSTOM) : la cheminée
// (x, y, en lieues), son moment dans le cycle (0 à 1) et sa taille au départ (lieues).
shader_type canvas_item;

uniform float rise_time = 7.0;              // la durée d'une montée (s)
uniform vec2 wind = vec2(10.0, -16.0);      // le chemin d'une bouffée sur toute sa montée (lieues)
uniform float grow = 2.2;                   // elle finit tant de fois plus grosse
uniform float opacity = 0.55;

varying float age;

void vertex() {
	age = fract(TIME / rise_time + INSTANCE_CUSTOM.z);
	float size = INSTANCE_CUSTOM.w * (1.0 + grow * age);
	VERTEX = INSTANCE_CUSTOM.xy + wind * age + VERTEX * size;
}

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	COLOR = vec4(t.rgb, t.a * opacity * sin(age * 3.14159));
}
