using Godot;
using System.Text.Json;

/// <summary>
/// Patrullero: es un CarAI que va por el eje X en vaivén (carriles 3/4).
/// Hereda frenado por semáforo, suciedad/humor, progreso y gracia, así que
/// también se le puede limpiar el parabrisas. Además vigila la calzada:
/// si te ve mucho tiempo, decomisa (plata de encima + mejoras).
/// </summary>
public partial class PatrolAI : CarAI
{
	[Export] public Player? Jugador;
	[Export] public Economy? Economia;
	[Export] public Inventario? Inventario;
	[Export] public CarSpawner? Spawner;
	[Export] public HUD? Hud;
	[Export] public AudioManager? Audio;
	[Export] public Mejoras? Mejoras;
	/// <summary>Luces de cada enfoque (este: viene de -X, oeste: viene de +X).</summary>
	[Export] public TrafficLight? SemaforoEste;
	[Export] public TrafficLight? SemaforoOeste;

	[Export] public float radioVision = 30f;
	[Export] public float toleranciaSeg = 32f;
	[Export] public float decaimiento = 6f;
	[Export] public float ritmoBase = 15f;

	float tiempoVisto = 0f;
	double tFlash = 0;
	StandardMaterial3D? matR, matB;

	public override void _Ready()
	{
		AddToGroup("patrulla");
		// El spawner lo crea con todo asignado; fallbacks por seguridad.
		// OJO: es hijo del spawner, las rutas suben dos niveles.
		Jugador ??= GetNodeOrNull<Player>("../../Player");
		Economia ??= GetNodeOrNull<Economy>("../../Economy");
		Inventario ??= GetNodeOrNull<Inventario>("../../Inventario");
		Spawner ??= GetParentOrNull<CarSpawner>();
		Hud ??= GetNodeOrNull<HUD>("../../HUD");
		Audio ??= GetNodeOrNull<AudioManager>("../../Audio");
		Mejoras ??= GetNodeOrNull<Mejoras>("../../Mejoras");
		SemaforoEste ??= GetNodeOrNull<TrafficLight>("../../Semaforo3");
		SemaforoOeste ??= GetNodeOrNull<TrafficLight>("../../Semaforo4");
		CargarBalance();
		AplicarSentido();
		// Visual + baliza (la colisión la agrega el spawner, como a los autos).
		var visual = ProcAuto.Build("patrulla", new Color(0.92f, 0.92f, 0.94f));
		AddChild(visual);
		matR = visual.GetNodeOrNull<MeshInstance3D>("BalizaR")?.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
		matB = visual.GetNodeOrNull<MeshInstance3D>("BalizaB")?.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
	}

	void CargarBalance()
	{
		var path = "res://data/balance.json";
		if (!FileAccess.FileExists(path)) return;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = JsonDocument.Parse(f.GetAsText());
			var root = doc.RootElement;
			var p = root.GetProperty("policia");
			Velocidad = (float)p.GetProperty("velocidad").GetDouble();
			radioVision = (float)p.GetProperty("radio_vision").GetDouble();
			if (p.TryGetProperty("tolerancia_seg", out var t)) toleranciaSeg = (float)t.GetDouble();
			if (p.TryGetProperty("decaimiento", out var d2)) decaimiento = (float)d2.GetDouble();
			if (p.TryGetProperty("ritmo_base", out var r2)) ritmoBase = (float)r2.GetDouble();
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Patrulla] balance.json sin policía, uso defaults: {e.Message}");
		}
	}

	void AplicarSentido()
	{
		bool este = Direccion > 0;
		RotationDegrees = new Vector3(0, este ? 90 : -90, 0);
		Semaforo = este ? SemaforoEste : SemaforoOeste;
		LineaDetencion = este ? -14f : 14f;
	}

	public override void _Process(double delta)
	{
		// Baliza alternada
		tFlash += delta;
		bool fase = ((int)(tFlash / 0.4)) % 2 == 0;
		if (matR != null) matR.EmissionEnergyMultiplier = fase ? 3f : 0.2f;
		if (matB != null) matB.EmissionEnergyMultiplier = fase ? 0.2f : 3f;

		Vigilar((float)delta);
	}

	void Vigilar(float d)
	{
		if (Jugador == null) return;
		// Sigilo: achica el radio de visión según su nivel.
		float radio = radioVision * (1f - (Mejoras != null ? Mejoras.ValorNivel("sigilo", Mejoras.NivelDe("sigilo")) : 0) / 100f);
		float dist = GlobalPosition.DistanceTo(Jugador.GlobalPosition);
		if (dist >= radio || !Jugador.EstaEnCalle())
		{
			// En la vereda (o lejos) la atención baja LENTO, no se borra.
			if (tiempoVisto > 0f) tiempoVisto = Mathf.Max(0f, tiempoVisto - d * decaimiento);
			return;
		}
		// Ritmo por cercanía (cuadrático): pegado llega al tope de 100%.
		float t = 1f - dist / radio;
		float ritmo = Mathf.Min(ritmoBase * t * t, 1f);
		// Cortando el tránsito (algún auto frena por vos): x2, hasta 200%.
		bool obstruye = Spawner != null && Spawner.JugadorObstruye;
		if (obstruye) ritmo = Mathf.Min(ritmo * 2f, 2f);
		tiempoVisto += d * ritmo;
		if (tiempoVisto >= toleranciaSeg) Decomisar();
		// El aviso visual lo muestra el HUD con la barra (NivelAlerta).
	}

	/// <summary>0..1 de atención policial; negativo = sin alerta (oculta el widget).</summary>
	public float NivelAlerta() =>
		tiempoVisto <= 0f ? -1f : Mathf.Min(tiempoVisto / toleranciaSeg, 1f);

	/// <summary>Segundos acumulados de atención (para el autosave del Paso 5).</summary>
	public float TiempoVisto() => tiempoVisto;

	/// <summary>Restaura la atención al cargar partida (Paso 5).</summary>
	public void FijarTiempoVisto(float t) => tiempoVisto = Mathf.Max(0f, t);

	void Decomisar()
	{
		tiempoVisto = 0f;
		Audio?.Play("sirena");
		int plata = Economia?.Coins ?? 0;
		Economia?.QuitarTodo();
		Inventario?.Resetear();
		Spawner?.ResetServicio();
		Jugador?.VolverAlSpawn();
		Hud?.SetProgreso(0f);
		Hud?.SetError($"¡Decomiso! Perdiste ${plata} y tus mejoras. Lo del escondite está a salvo (G en la mochila).");
	}
}
