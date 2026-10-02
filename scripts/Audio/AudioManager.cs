using Godot;
using System.Collections.Generic;

/// <summary>
/// Paso 6: audio importado (los .wav viven en audio/, generados con
/// tools/generar_audio.py; se reemplazan por grabaciones reales sin tocar código).
/// Pool de 8 voces no-posicionales (todo es feedback de gameplay: importa que
/// se escuche, no de dónde viene) + 3 loops (ambiente ciudad, lluvia y
/// música de fondo). Los loops se configuran por código (los .import vienen
/// sin loop): suenan sin cortes porque los wav traen crossfade.
/// Reglas de game-juice: pitch aleatorio ±10% en cada disparo, cobro con
/// escalerita de tono por propina, volúmenes por importancia.
/// El volumen maestro lo maneja Ajustes en el bus Master (ya existía).
/// </summary>
public partial class AudioManager : Node3D
{
	const string RutaBase = "res://audio/";
	const int Voces = 8;

	/// <summary>Volumen base por sonido (los importantes suenan más fuerte).</summary>
	static readonly Dictionary<string, float> VolumenDb = new()
	{
		{ "monedas", -4f }, { "bocina", -2f }, { "sirena", -6f },
		{ "frote", -8f }, { "abrir", -8f }, { "negado", -6f },
	};

	readonly RandomNumberGenerator rng = new();
	readonly Dictionary<string, AudioStream> cache = new();
	readonly List<AudioStreamPlayer> pool = new();
	readonly HashSet<string> avisados = new();
	int rueda;

	AudioStreamPlayer? loopAmbiente;
	AudioStreamPlayer? loopLluvia;
	AudioStreamPlayer? loopMusica;

	public override void _Ready()
	{
		rng.Randomize();
		for (int i = 0; i < Voces; i++)
		{
			var p = new AudioStreamPlayer { Bus = "Master" };
			AddChild(p);
			pool.Add(p);
		}
		loopAmbiente = CrearLoop("ambiente", -26f);
		loopLluvia = CrearLoop("lluvia", -60f);
		// Música de fondo de la partida (loop; el volumen maestro la controla).
		loopMusica = CrearLoop("musica_juego", -17f);
	}

	/// <summary>Crea un player en loop real sobre el .wav (los .import vienen
	/// con loop_mode=0, así que el loop se configura acá: suena sin cortes).</summary>
	AudioStreamPlayer? CrearLoop(string nombre, float volumenDb)
	{
		var p = new AudioStreamPlayer { Bus = "Master", VolumeDb = volumenDb };
		AddChild(p);
		p.Stream = Cargar(nombre);
		if (p.Stream == null) return p;
		AplicarLoop(p.Stream);
		p.Play();
		return p;
	}

	/// <summary>Loop punta-con-punta sobre el stream importado. Los wavs de
	/// música/ambiente ya traen crossfade, así que el corte es inaudible.
	/// Estático para que el menú (sin AudioManager) lo reuse.</summary>
	public static void AplicarLoop(AudioStream? stream)
	{
		if (stream is not AudioStreamWav wav) return;
		try
		{
			int bytesPorFrame = (wav.Format == AudioStreamWav.FormatEnum.Format16Bits ? 2 : 1)
				* (wav.Stereo ? 2 : 1);
			int frames = bytesPorFrame > 0 ? wav.Data.Length / bytesPorFrame : 0;
			wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
			wav.LoopBegin = 0;
			wav.LoopEnd = frames;
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Audio] no se pudo loopear el stream: {e.Message}");
		}
	}

	AudioStream? Cargar(string nombre)
	{
		if (cache.TryGetValue(nombre, out var s)) return s;
		var stream = GD.Load<AudioStream>($"{RutaBase}{nombre}.wav");
		if (stream == null)
		{
			if (avisados.Add(nombre))
				GD.PushWarning($"[Audio] falta audio/{nombre}.wav (generar con tools/generar_audio.py)");
			return null;
		}
		cache[nombre] = stream;
		return stream;
	}

	/// <summary>Pitch aleatorio ±10% para que el frote x6 no suene ametralladora.</summary>
	public void Play(string nombre, float pitchBase = 1f, float masDb = 0f)
	{
		var stream = Cargar(nombre);
		if (stream == null || pool.Count == 0) return;
		var p = pool[rueda % pool.Count];
		rueda++;
		float baseDb = VolumenDb.TryGetValue(nombre, out float v) ? v : -6f;
		p.Stream = stream;
		p.PitchScale = pitchBase * rng.RandfRange(0.92f, 1.08f);
		p.VolumeDb = baseDb + masDb;
		p.Play();
	}

	/// <summary>Cobro con escalerita: propina grande suena más aguda.</summary>
	public void PlayCobro(int propina)
	{
		float p = propina >= 5 ? 1.18f : propina > 0 ? 1.08f : 1f;
		Play("monedas", p);
	}

	/// <summary>Toque con calidad: perfecto agudo, mal grave.</summary>
	public void PlayToque(string calidad)
	{
		float p = calidad.StartsWith("¡Perfecto") || calidad.StartsWith("¡Buen") ? 1.2f
			: calidad == "Bien" ? 1f : 0.85f;
		Play("frote", p);
	}

	public override void _Process(double delta)
	{
		// La lluvia entra y sale con easing (acompaña al nivel visual).
		if (loopLluvia != null)
		{
			float objetivo = Mathf.Lerp(-60f, -16f, Clima.NivelLluvia);
			loopLluvia.VolumeDb = Mathf.MoveToward(loopLluvia.VolumeDb, objetivo, (float)delta * 20f);
		}
	}
}
