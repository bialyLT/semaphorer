using Godot;

/// <summary>
/// Set fijo verificado para las cinemáticas del cruce (intro y final).
/// La vereda este-sur, parche x∈[4,10] z∈[-13,-5], no tiene utilería alta:
/// el árbol más cercano (A5 en 12.5,-6) queda a 6m+, los faroles y bancos
/// están al norte (z≥7), los pórticos a 9m+ y el edificio E3 queda al sur
/// (detrás de cámara). Encuadrar donde esté el jugador es lotería (ahí
/// tapaba un árbol): el orquestador pone actores/cámara acá siempre.
/// </summary>
public static class CineSet
{
	public static readonly Vector3 Foco = new(7, ProcPeaton.YVereda, -9);
}
