using Godot;
using System;

/// <summary>
/// Maneja el estado del tutorial: si ya se completó, en qué paso está,
/// y notifica cuando el tutorial termina para que el juego continúe.
/// </summary>
public static class TutorialManager
{
	public static bool TutorialCompletado { get; private set; } = false;
	public static int PasoActual { get; set; } = 0;
	public static bool TutorialActivo { get; private set; } = false;

	public const int TotalPasos = 7;
	/// <summary>Pasos del tutorial jugable: 0 calle, 1 auto en rojo,
	/// 2 cobrar con E, 3 vereda con Espacio, 4 mochila con G, 5 tienda con F,
	/// 6 aviso del tutorial del menú.</summary>
	public const int TotalInteractivo = 7;

	public static event Action<int>? PasoAvanzado;
	public static event Action? TutorialTerminado;

	static TutorialManager()
	{
		Cargar();
	}

	public static void IniciarTutorial()
	{
		PasoActual = 0;
		TutorialActivo = true;
	}

	public static void AvanzarPaso()
	{
		if (!TutorialActivo) return;
		PasoActual++;
		PasoAvanzado?.Invoke(PasoActual);
		if (PasoActual >= TotalPasos)
		{
			CompletarTutorial();
		}
	}

	public static void CompletarTutorial()
	{
		TutorialActivo = false;
		TutorialCompletado = true;
		Guardar();
		TutorialTerminado?.Invoke();
	}

	/// <summary>
	/// Cerrar el tutorial sin terminarlo. Cuenta como visto igual que
	/// completarlo: si no, reaparece en cada partida nueva.
	/// </summary>
	public static void SaltarTutorial()
	{
		if (!TutorialActivo) return;
		TutorialActivo = false;
		TutorialCompletado = true;
		Guardar();
		TutorialTerminado?.Invoke();
	}

	/// <summary>Solo la primera vez: si ya se vio (o se saltó), no se repite.</summary>
	public static bool DebeMostrarTutorialEnPartida()
	{
		return !TutorialCompletado;
	}

	static void Guardar()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("tutorial", "completado", TutorialCompletado);
		cfg.Save("user://tutorial.cfg");
	}

	static void Cargar()
	{
		var cfg = new ConfigFile();
		if (cfg.Load("user://tutorial.cfg") == Error.Ok)
		{
			TutorialCompletado = (bool)cfg.GetValue("tutorial", "completado", false);
		}
	}
}
