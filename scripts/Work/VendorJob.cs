using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Fase 3 venta ambulante: ofrecés limones y morrones al conductor detenido
/// (E con ritmo de pregón). Venta rápida de pocas ofertas + propina.
/// Reusa targeting, contadores del auto y humor igual que los otros oficios.
/// El verde lo procesa SOLO WindshieldJob (siempre en escena) sobre los
/// contadores compartidos: este job NO se suscribe a las luces para no
/// triplicar eventos ni mezclar mensajes de oficios.
/// </summary>
public partial class VendorJob : Node, IJob
{
	[Export] public TrafficLight? Semaforo;
	[Export] public TrafficLight? Semaforo2;
	[Export] public TrafficLight? Semaforo3;
	[Export] public TrafficLight? Semaforo4;
	[Export] public CarSpawner? Spawner;
	[Export] public Economy? Economia;
	[Export] public AudioManager? Audio;
	[Export] public Mejoras? Mejoras;

	int Nv(string id) => Mejoras != null ? Mejoras.NivelDe(id) : 0;
	int Val(string id) => Mejoras != null ? Mejoras.ValorNivel(id, Nv(id)) : 0;

	// IJob exige estos eventos; el verde lo emite SOLO WindshieldJob,
	// así estos nunca se invocan acá (evitan mezclar avisos entre oficios).
#pragma warning disable CS0067
	public event Action? ProgresoReseteado;
	public event Action? GraciaOtorgada;
#pragma warning restore CS0067

	[Signal] public delegate void ProgresoPerdidoEventHandler();
	[Signal] public delegate void GraciaDadaEventHandler();

	public string Nombre => "Venta ambulante";
	public float Progreso01 => ProgresoAutoActual();
	public string UltimoMensaje { get; private set; } = "";
	public string UltimoTono { get; private set; } = "info";

	int ofertasParaVenta = 3;
	int pagoBase = 9;
	float rango = 4.5f;
	float perfectoMin = 0.4f, perfectoMax = 1.0f, bienMax = 2.0f;
	int propinaPerfecta = 4, propinaBuena = 2;
	float limiteApuradoSeg = 16f;
	float probRechazoEnojado = 0.25f;
	float graciaAmableSeg = 4f;

	Node3D? jugador;
	double ultimaOfertaSeg = -1;

	public override void _Ready()
	{
		jugador = GetParentOrNull<Node3D>();
		Semaforo ??= GetNodeOrNull<TrafficLight>("../../Semaforo");
		Semaforo2 ??= GetNodeOrNull<TrafficLight>("../../Semaforo2");
		Semaforo3 ??= GetNodeOrNull<TrafficLight>("../../Semaforo3");
		Semaforo4 ??= GetNodeOrNull<TrafficLight>("../../Semaforo4");
		Spawner ??= GetNodeOrNull<CarSpawner>("../../CarSpawner");
		Economia ??= GetNodeOrNull<Economy>("../../Economy");
		Audio ??= GetNodeOrNull<AudioManager>("../../Audio");
		Mejoras ??= GetNodeOrNull<Mejoras>("../../Mejoras");
		CargarBalance();
		// NO suscribirse a LightChanged: WindshieldJob es el único procesador
		// del verde. Suscribirse acá triplicaba eventos y mezclaba avisos.
	}

	void CargarBalance()
	{
		var path = "res://data/balance.json";
		if (!FileAccess.FileExists(path)) return;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = JsonDocument.Parse(f.GetAsText());
			if (!doc.RootElement.TryGetProperty("venta", out var j)) return;
			ofertasParaVenta = j.GetProperty("ofertas_para_venta").GetInt32();
			pagoBase = j.GetProperty("pago_base").GetInt32();
			rango = (float)j.GetProperty("rango_interaccion").GetDouble();
			perfectoMin = (float)j.GetProperty("ritmo_perfecto_min").GetDouble();
			perfectoMax = (float)j.GetProperty("ritmo_perfecto_max").GetDouble();
			bienMax = (float)j.GetProperty("ritmo_bien_max").GetDouble();
			propinaPerfecta = j.GetProperty("propina_perfecta").GetInt32();
			propinaBuena = j.GetProperty("propina_buena").GetInt32();
			limiteApuradoSeg = (float)j.GetProperty("limite_apurado_seg").GetDouble();
			probRechazoEnojado = (float)j.GetProperty("prob_rechazo_enojado").GetDouble();
			graciaAmableSeg = (float)j.GetProperty("gracia_amable_seg").GetDouble();
		}
		catch (Exception e)
		{
			GD.PushWarning($"[VendorJob] balance.json inválido, uso defaults: {e.Message}");
		}
	}

	CarAI? AutoObjetivo()
	{
		if (Spawner == null || jugador == null) return null;
		return Spawner.AutoCercanoDetenido(jugador.GlobalPosition, rango);
	}

	float ProgresoAutoActual()
	{
		var auto = AutoObjetivo();
		return auto == null ? 0f : (float)auto.Golpes / ofertasParaVenta;
	}

	public bool PuedeTrabajar() => AutoObjetivo() != null;

	public string Motivo()
	{
		if (Spawner == null || jugador == null)
			return "Solo en ROJO y junto al auto detenido (E)";
		var cerca = Spawner.AutoDetenidoCercano(jugador.GlobalPosition, rango);
		if (cerca == null)
			return "Solo en ROJO y junto al auto detenido (E)";
		if (cerca.Atendido)
			return "Este ya compró, ofrecé en otro auto.";
		return "Solo en ROJO y junto al auto detenido (E)";
	}

	public int Actuar()
	{
		var auto = AutoObjetivo();
		if (auto == null) return 0;
		double ahora = Time.GetTicksMsec() / 1000.0;

		string calidad;
		int puntos;
		if (auto.Golpes == 0)
		{
			calidad = "Bien";
			puntos = 1;
			auto.InicioServicio = ahora;
		}
		else
		{
			float dt = (float)(ahora - ultimaOfertaSeg);
			if (dt < perfectoMin) { calidad = "Mal: lo atolondraste"; puntos = 0; }
			else if (dt <= perfectoMax) { calidad = "¡Buen pregón!"; puntos = 2; }
			else if (dt <= bienMax) { calidad = "Bien"; puntos = 1; }
			else { calidad = "Mal: te dormiste"; puntos = 0; }
		}
		ultimaOfertaSeg = ahora;
		auto.Golpes += 1;
		auto.PuntosCalidad += puntos;
		Audio?.PlayToque(calidad);
		float prog = (float)auto.Golpes / ofertasParaVenta;

		if (auto.Golpes < ofertasParaVenta)
		{
			UltimoMensaje = $"Ofreciendo... {prog:P0} {calidad} [{auto.Humor}]";
			UltimoTono = "info";
			return 0;
		}

		float promedio = (float)auto.PuntosCalidad / ofertasParaVenta;
		auto.Golpes = 0;
		auto.PuntosCalidad = 0;
		auto.Atendido = true;
		auto.GraciaHasta = 0;

		// Labia al máximo: -80% de rechazo (antes inmunidad).
		float probRechazo = probRechazoEnojado;
		if (Mejoras != null && Mejoras.ExtraNivel("chamuyo", Nv("chamuyo")) == "sin_rechazo")
			probRechazo *= 0.2f;
		if (auto.Humor == "enojado" && GD.Randf() < probRechazo)
		{
			Audio?.Play("bocina");
			UltimoMensaje = "¡Bocinazo! El enojado no quiso nada.";
			UltimoTono = "mal";
			return 0;
		}
		double duracion = ahora - auto.InicioServicio;
		float limiteApurado = limiteApuradoSeg + Val("chamuyo");
		if (auto.Humor == "apurado" && duracion > limiteApurado)
		{
			Audio?.Play("bocina");
			UltimoMensaje = $"Tardaste {duracion:0}s. El apurado arrancó sin comprar.";
			UltimoTono = "mal";
			return 0;
		}
		int propina = promedio >= 1.7f ? propinaPerfecta
			: promedio >= 1.0f ? propinaBuena : 0;
		if (auto.Humor == "enojado") propina = 0;
		if (auto.Humor == "apurado") propina = Math.Min(propina, propinaBuena);
		propina += Val("pregon");
		int pago = pagoBase + Val("canasta") + propina + Clima.PropinaExtra;
		float multCiudad = Ciudades.Mult(SaveSystem.CargarCiudad());
		pago = Ciudades.AplicarMult(pago);
		Economia?.AddCoins(pago);
		Audio?.PlayCobro(propina);
		if (Economia != null) SaveSystem.Guardar(Economia.Coins);
		var extras = new System.Collections.Generic.List<string>();
		if (Val("canasta") > 0) extras.Add($"canasta ${Val("canasta")}");
		if (propina > 0) extras.Add($"propina ${propina}");
		if (Clima.PropinaExtra > 0) extras.Add($"lluvia ☔ ${Clima.PropinaExtra}");
		if (multCiudad > 1f) extras.Add($"ciudad x{multCiudad}");
		UltimoMensaje = extras.Count > 0
			? $"¡Vendido! +${pago} ({string.Join(", ", extras)})"
			: $"¡Vendido! +${pago}";
		UltimoTono = "ok";
		return pago;
	}

	public void Reset() => Spawner?.ResetServicio();
}
