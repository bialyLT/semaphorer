using Godot;

/// <summary>
/// La tirada del tipo de gala: ruleta de plata ($50 a $500) con peso
/// inverso al monto (más plata, menos chance). MUESTRA el millón pero
/// está arreglada: el millón ni siquiera está entre los candidatos y
/// nunca sale. Valor esperado ≈ $137 por tirada.
/// </summary>
public static class TiradaTipo
{
	public const int Millon = 1000000;

	/// <summary>(monto, peso): el peso cae al subir el monto.</summary>
	public static readonly (int monto, int peso)[] Premios =
	{
		(50, 40),
		(100, 24),
		(200, 14),
		(350, 8),
		(500, 5),
	};

	/// <summary>Sortea un premio (el millón nunca: no es candidato).</summary>
	public static int Tirar(RandomNumberGenerator rng)
	{
		int total = 0;
		foreach (var p in Premios) total += p.peso;
		int tiro = rng.RandiRange(1, total);
		foreach (var p in Premios)
		{
			tiro -= p.peso;
			if (tiro <= 0) return p.monto;
		}
		return Premios[0].monto;
	}

	public static string TextoPremio(int monto) =>
		monto >= Millon ? "¡$1.000.000!" : $"${monto}";
}
