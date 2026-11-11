// L'ombre portée d'une image (arbre, toit) : sa silhouette, sombre et transparente. Le nœud qui la porte est décalé
// vers le sud-est (la lumière vient du nord-ouest).
shader_type canvas_item;

uniform float strength = 0.35;
uniform vec3 shadow_color = vec3(0.10, 0.12, 0.06);

void fragment() {
	COLOR = vec4(shadow_color, texture(TEXTURE, UV).a * strength);
}
