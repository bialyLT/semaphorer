using Godot;

/// <summary>
/// La última tirada de la suerte (Fase 2). La ruleta MUESTRA el millón pero
/// está arreglada: el millón tiene peso 0 y nunca sale, salvo que se
/// fuerce explícitamente (tirada final de la Fase 6).
/// Tirar() solo devuelve oficios jugables.
/// </summary>
public static class TiradaSuerte
{
	public const string Millon = "millon";
	public static readonly string[] Oficios = { "limpiavidrios", "malabarista", "vendedor" };

	public static string Tirar(RandomNumberGenerator rng, bool forzarMillon = false)
	{
		if (forzarMillon) return Millon;
		// El millón ni siquiera está entre los candidatos: no puede salir.
		return Oficios[rng.RandiRange(0, Oficios.Length - 1)];
	}

	public static string EmojiDe(string id) => id switch
	{
		Millon => "💰",
		"limpiavidrios" => "🧽",
		"malabarista" => "🎾",
		"vendedor" => "🥬",
		_ => "❓"
	};

	public static string NombreDe(string id) => id switch
	{
		Millon => "¡$1.000.000!",
		"limpiavidrios" => "Limpiavidrios",
		"malabarista" => "Malabares",
		"vendedor" => "Venta ambulante",
		_ => id
	};

	public static string DescDe(string id) => id switch
	{
		Millon => "El sueño. Dicen que una vez salió...",
		"limpiavidrios" => "Botella con detergente + limpiavidrios",
		"malabarista" => "3 pelotas de tenis",
		"vendedor" => "Palo + limones y morrones",
		_ => ""
	};
}
