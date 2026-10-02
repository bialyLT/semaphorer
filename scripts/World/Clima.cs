using Godot;

/// <summary>
/// Paso 5 — clima (skill godot-csharp + balanceo-economia + game-feel).
/// Estados data-driven (balance.json [clima]): despejado / nublado / lluvia.
/// La transición es gradual (nada aparece de golpe): la lluvia sube con easing,
/// la niebla acompaña y el sol se atenúa. Aplica el modificador DESPUÉS del
/// ciclo día-noche (multiplica, no pisa). Con lluvia la propina sube +N
/// (balance: cambio chico que premia laburar con mal tiempo).
/// </summary>
public partial class Clima : Node3D
{
	[Signal] public delegate void ClimaCambiadoEventHandler(string nuevo);

	[Export] public float ProbLluvia = 0.3f;
	[Export] public float DurMinSeg = 60f;
	[Export] public float DurMaxSeg = 150f;

	public string Actual { get; private set; } = "despejado";

	/// <summary>0..1 de lluvia real (con easing). Lo lee el AudioManager para el loop.</summary>
	public static float NivelLluvia { get; private set; }

	/// <summary>Propina +N con lluvia para los 3 oficios (la leen los jobs).</summary>
	public static int PropinaExtra { get; private set; }
	int propinaLluvia = 2;

	DirectionalLight3D? sol;
	Godot.Environment? entorno;
	GpuParticles3D? lluvia;
	Player? jugador;

	float timer = 20f;
	float nivelLluvia; // 0..1 real (con easing hacia el objetivo)

	public override void _Ready()
	{
		sol ??= GetNodeOrNull<DirectionalLight3D>("../Sun");
		entorno = GetNodeOrNull<WorldEnvironment>("../WorldEnvironment")?.Environment;
		jugador ??= GetNodeOrNull<Player>("../Player");
		CargarBalance();
		ConstruirLluvia();
		if (entorno != null) entorno.FogEnabled = true;
	}

	void CargarBalance()
	{
		try
		{
			var path = "res://data/balance.json";
			if (!FileAccess.FileExists(path)) return;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = System.Text.Json.JsonDocument.Parse(f.GetAsText());
			if (!doc.RootElement.TryGetProperty("clima", out var c)) return;
			if (c.TryGetProperty("prob_lluvia", out var v)) ProbLluvia = (float)v.GetDouble();
			if (c.TryGetProperty("duracion_min_seg", out v)) DurMinSeg = (float)v.GetDouble();
			if (c.TryGetProperty("duracion_max_seg", out v)) DurMaxSeg = (float)v.GetDouble();
			if (c.TryGetProperty("propina_extra_lluvia", out v)) propinaLluvia = v.GetInt32();
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Clima] balance sin clima: {e.Message}");
		}
	}

	void ConstruirLluvia()
	{
		lluvia = new GpuParticles3D
		{
			Amount = 600,
			Lifetime = 0.8f,
			Preprocess = 0.8f,
			LocalCoords = false,
			AmountRatio = 0f,
			VisibilityAabb = new Aabb(new Vector3(-40f, -5f, -40f), new Vector3(80f, 30f, 80f)),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(15f, 1f, 15f),
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 6f,
				InitialVelocityMin = 16f,
				InitialVelocityMax = 20f,
				Gravity = new Vector3(0f, -9f, 0f)
			}
		};
		var gota = new BoxMesh { Size = new Vector3(0.03f, 0.45f, 0.03f) };
		gota.Material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.6f, 0.75f, 0.95f, 0.7f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		lluvia.DrawPass1 = gota;
		AddChild(lluvia);
	}

	/// <summary>Fuerza un estado (debug/trucos y tests). Reinicia el timer.</summary>
	public void Forzar(string estado)
	{
		if (estado != "lluvia" && estado != "nublado" && estado != "despejado") return;
		Actual = estado;
		timer = GD.Randf() * (DurMaxSeg - DurMinSeg) + DurMinSeg;
		EmitSignal(SignalName.ClimaCambiado, Actual);
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		timer -= d;
		if (timer <= 0f)
		{
			float r = GD.Randf();
			Actual = r < ProbLluvia ? "lluvia" : (GD.Randf() < 0.5f ? "nublado" : "despejado");
			timer = GD.Randf() * (DurMaxSeg - DurMinSeg) + DurMinSeg;
			EmitSignal(SignalName.ClimaCambiado, Actual);
		}
		// Easing hacia el objetivo (feel: la tormenta "entra", no spawnea).
		float objetivo = Actual == "lluvia" ? 1f : 0f;
		nivelLluvia = Mathf.MoveToward(nivelLluvia, objetivo, d * 0.4f);
		NivelLluvia = nivelLluvia;
		PropinaExtra = nivelLluvia > 0.5f ? propinaLluvia : 0;

		float dim = Actual == "lluvia" ? 0.55f : Actual == "nublado" ? 0.8f : 1f;
		if (sol != null) sol.LightEnergy *= dim;
		if (entorno != null)
		{
			entorno.AmbientLightEnergy *= dim;
			entorno.FogDensity = Mathf.Lerp(entorno.FogDensity, nivelLluvia * 0.02f, Mathf.Min(1f, d * 0.5f));
		}
		if (lluvia != null)
		{
			lluvia.AmountRatio = nivelLluvia;
			if (jugador != null && IsInstanceValid(jugador))
				lluvia.GlobalPosition = jugador.GlobalPosition + Vector3.Up * 10f;
		}
	}
}
