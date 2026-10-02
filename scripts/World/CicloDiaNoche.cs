using Godot;

/// <summary>
/// Paso 5 — ciclo día-noche (skill godot-csharp + game-feel).
/// Un día dura DuracionDiaSeg (360s): el sol viaja de este a oeste, el cielo
/// mezcla día/noche/atardecer con transiciones suavizadas (nada lineal) y la
/// luz ambiente acompaña. Emite HoraCambiada/DiaNuevo para el HUD (eventos,
/// sin polling) y GameManager persiste día+hora en el autosave (skill rpg).
/// </summary>
public partial class CicloDiaNoche : Node3D
{
	[Signal] public delegate void HoraCambiadaEventHandler(int hora);
	[Signal] public delegate void DiaNuevoEventHandler(int dia);

	[Export] public float DuracionDiaSeg = 360f;

	public float HoraActual { get; private set; } = 9f;
	public int Dia { get; private set; } = 1;

	DirectionalLight3D? sol;
	Godot.Environment? entorno;
	ProceduralSkyMaterial? cielo;

	int ultimaHoraEmitida = -1;

	static readonly Color CieloDiaTop = new(0.25f, 0.5f, 0.9f);
	static readonly Color CieloDiaHor = new(0.7f, 0.85f, 0.95f);
	static readonly Color CieloNocheTop = new(0.02f, 0.03f, 0.08f);
	static readonly Color CieloNocheHor = new(0.09f, 0.11f, 0.18f);
	static readonly Color TinteAtardecer = new(1f, 0.45f, 0.15f);

	public override void _Ready()
	{
		sol ??= GetNodeOrNull<DirectionalLight3D>("../Sun");
		entorno = GetNodeOrNull<WorldEnvironment>("../WorldEnvironment")?.Environment;
		cielo = entorno?.Sky?.SkyMaterial as ProceduralSkyMaterial;
		CargarBalance();
		Dia = SaveSystem.CargarDia();
		HoraActual = SaveSystem.CargarHoraDia(9f);
		AplicarAstro();
	}

	void CargarBalance()
	{
		try
		{
			var path = "res://data/balance.json";
			if (!FileAccess.FileExists(path)) return;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = System.Text.Json.JsonDocument.Parse(f.GetAsText());
			if (doc.RootElement.TryGetProperty("dia_noche", out var d))
			{
				if (d.TryGetProperty("duracion_dia_seg", out var v))
					DuracionDiaSeg = Mathf.Max(60f, (float)v.GetDouble());
			}
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[DiaNoche] balance sin dia_noche: {e.Message}");
		}
	}

	public override void _Process(double delta)
	{
		if (DuracionDiaSeg <= 0f) return;
		HoraActual += (float)delta * 24f / DuracionDiaSeg;
		if (HoraActual >= 24f)
		{
			HoraActual -= 24f;
			Dia++;
			EmitSignal(SignalName.DiaNuevo, Dia);
		}
		int h = (int)HoraActual;
		if (h != ultimaHoraEmitida)
		{
			ultimaHoraEmitida = h;
			EmitSignal(SignalName.HoraCambiada, h);
		}
		AplicarAstro();
	}

	/// <summary>0..1 de luz diurna (para faroles/lluvia/UI).</summary>
	public float LuzDiurna()
	{
		float elev = Mathf.Sin((HoraActual - 6f) / 12f * Mathf.Pi);
		return Mathf.SmoothStep(-0.08f, 0.18f, elev);
	}

	/// <summary>Fija la hora (debug/trucos y tests). Aplica el astro al acto.</summary>
	public void FijarHora(float h)
	{
		HoraActual = Mathf.Clamp(h, 0f, 23.99f);
		AplicarAstro();
	}

	void AplicarAstro()
	{
		float dia = LuzDiurna();
		// Atardecer/amanecer: banda naranja cuando el sol roza el horizonte.
		float elev = Mathf.Sin((HoraActual - 6f) / 12f * Mathf.Pi);
		float ocaso = Mathf.Clamp(1f - Mathf.Abs(elev) * 4f, 0f, 1f);

		if (sol != null)
		{
			float y = Mathf.Lerp(-90f, 90f, Mathf.Clamp((HoraActual - 6f) / 12f, 0f, 1f));
			sol.RotationDegrees = new Vector3(Mathf.Lerp(-4f, -88f, dia), y, 0f);
			sol.LightEnergy = 0.04f + dia * 1.1f;
		}
		if (entorno != null)
			entorno.AmbientLightEnergy = 0.22f + dia * 0.78f;
		if (cielo != null)
		{
			var top = CieloNocheTop.Lerp(CieloDiaTop, dia);
			var hor = CieloNocheHor.Lerp(CieloDiaHor, dia);
			hor = hor.Lerp(TinteAtardecer, ocaso * 0.65f);
			cielo.SkyTopColor = top;
			cielo.SkyHorizonColor = hor;
		}
	}
}
