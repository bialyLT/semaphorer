using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Paso 2 limpiaparabrisas: 6 toques (E) con RITMO junto al auto detenido = $10.
/// Cada toque se califica (perfecto/bien/mal) y da propina; el humor del
/// conductor (amable/apurado/enojado) decide si paga. El progreso es por auto:
/// si su semáforo da verde antes de terminar, se pierde.
/// ÚNICO procesador del verde (Juggling/Vendor NO se suscriben): aplica la
/// gracia sobre los contadores compartidos y emite ProgresoReseteado /
/// GraciaOtorgada una sola vez, así no se mezclan avisos entre oficios.
/// Implementa IJob para que Juggling/Vendor reusen la interfaz en Paso 3.
/// </summary>
public partial class WindshieldJob : Node, IJob
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

	/// <summary>Se emite cuando un verde descarta progreso a medias.</summary>
	public event Action? ProgresoReseteado;
	/// <summary>Se emite cuando un amable da unos segundos de changüí en verde.</summary>
	public event Action? GraciaOtorgada;

	/// <summary>Altavoces Godot (visibles en el editor): se emiten junto a los Action.</summary>
	[Signal] public delegate void ProgresoPerdidoEventHandler();
	[Signal] public delegate void GraciaDadaEventHandler();

	public string Nombre => "Limpiaparabrisas";
	public float Progreso01 => ProgresoAutoActual();
	public string UltimoMensaje { get; private set; } = "";
	/// <summary>Tono del último mensaje: "info" | "ok" | "mal".</summary>
	public string UltimoTono { get; private set; } = "info";

	int golpesParaCompletar = 6;
	int pagoBase = 10;
	float rango = 4.5f;
	// Ritmo entre toques (segundos): muy rápido = spam, muy lento = dormido.
	float perfectoMin = 0.35f, perfectoMax = 0.9f, bienMax = 1.8f;
	int propinaPerfecta = 5, propinaBuena = 2;
	float limiteApuradoSeg = 14f;
	float probRechazoEnojado = 0.35f;
	float graciaAmableSeg = 4f;

	Node3D? jugador;
	double ultimoGolpeSeg = -1;

	public override void _Ready()
	{
		jugador = GetParentOrNull<Node3D>();
		// Fallback por si los exports del .tscn no enlazaron (clases C# no globales).
		Semaforo ??= GetNodeOrNull<TrafficLight>("../../Semaforo");
		Semaforo2 ??= GetNodeOrNull<TrafficLight>("../../Semaforo2");
		Semaforo3 ??= GetNodeOrNull<TrafficLight>("../../Semaforo3");
		Semaforo4 ??= GetNodeOrNull<TrafficLight>("../../Semaforo4");
		Spawner ??= GetNodeOrNull<CarSpawner>("../../CarSpawner");
		Economia ??= GetNodeOrNull<Economy>("../../Economy");
		Audio ??= GetNodeOrNull<AudioManager>("../../Audio");
		Mejoras ??= GetNodeOrNull<Mejoras>("../../Mejoras");
		CargarBalance();
		// Si un carril da verde, SUS autos arrancan: se pierde lo no terminado
		// de ese carril (los otros siguen en su fase).
		if (Semaforo != null) Semaforo.LightChanged += e => OnLuzCambiada(e, Semaforo);
		if (Semaforo2 != null) Semaforo2.LightChanged += e => OnLuzCambiada(e, Semaforo2);
		if (Semaforo3 != null) Semaforo3.LightChanged += e => OnLuzCambiada(e, Semaforo3);
		if (Semaforo4 != null) Semaforo4.LightChanged += e => OnLuzCambiada(e, Semaforo4);
	}

	void CargarBalance()
	{
		var path = "res://data/balance.json";
		if (!FileAccess.FileExists(path)) return;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = JsonDocument.Parse(f.GetAsText());
			var j = doc.RootElement.GetProperty("limpiaparabrisas");
			golpesParaCompletar = j.GetProperty("golpes_para_completar").GetInt32();
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
			GD.PushWarning($"[WindshieldJob] balance.json inválido, uso defaults: {e.Message}");
		}
	}

	void OnLuzCambiada(string nuevoEstado, TrafficLight luz)
	{
		if (nuevoEstado != "VERDE" || Spawner == null) return;
		// Labia: más changüí del amable (mitad del bonus de paciencia).
		float gracia = graciaAmableSeg + Val("chamuyo") / 2f;
		var (perdidos, conGracia) = Spawner.AplicarGraciaVerde(luz, gracia);
		if (perdidos > 0) { ProgresoReseteado?.Invoke(); EmitSignal(SignalName.ProgresoPerdido); }
		if (conGracia > 0) { GraciaOtorgada?.Invoke(); EmitSignal(SignalName.GraciaDada); }
	}

	CarAI? AutoObjetivo()
	{
		if (Spawner == null || jugador == null) return null;
		// El auto ya valida con SU semáforo (Servible), así cada carril
		// trabaja con su propia luz en oposición.
		return Spawner.AutoCercanoDetenido(jugador.GlobalPosition, rango);
	}

	float ProgresoAutoActual()
	{
		var auto = AutoObjetivo();
		return auto == null ? 0f : (float)auto.Golpes / golpesParaCompletar;
	}

	public bool PuedeTrabajar() => AutoObjetivo() != null;

	/// <summary>Por qué no se puede trabajar ahora (para el HUD).</summary>
	public string Motivo()
	{
		if (Spawner == null || jugador == null)
			return "Solo en ROJO y cerca del auto detenido (E)";
		var cerca = Spawner.AutoDetenidoCercano(jugador.GlobalPosition, rango);
		if (cerca == null)
			return "Solo en ROJO y cerca del auto detenido (E)";
		if (cerca.Atendido)
			return "Este ya pagó, buscá otro auto.";
		return "Solo en ROJO y cerca del auto detenido (E)";
	}

	public int Actuar()
	{
		var auto = AutoObjetivo();
		if (auto == null) return 0;
		double ahora = Time.GetTicksMsec() / 1000.0;

		// Calificar el toque por ritmo (el primero siempre cuenta como Bien).
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
			float dt = (float)(ahora - ultimoGolpeSeg);
			if (dt < perfectoMin) { calidad = "Mal: muy rápido"; puntos = 0; }
			else if (dt <= perfectoMax) { calidad = "¡Perfecto!"; puntos = 2; }
			else if (dt <= bienMax) { calidad = "Bien"; puntos = 1; }
			else { calidad = "Mal: te dormiste"; puntos = 0; }
		}
		ultimoGolpeSeg = ahora;
		// Esponja: cada toque vale el multiplicador de su nivel.
		int valor = Nv("esponja") > 0 ? Val("esponja") : 1;
		auto.Golpes += valor;
		auto.PuntosCalidad += puntos * valor;
		Audio?.PlayToque(calidad);
		float prog = (float)auto.Golpes / golpesParaCompletar;
		auto.RefrescarVidrio(prog);

		if (auto.Golpes < golpesParaCompletar)
		{
			UltimoMensaje = $"Limpiando... {prog:P0} {calidad} [{auto.Humor}]";
			UltimoTono = "info";
			return 0;
		}

		// Servicio completo: el humor decide si hay pago.
		float promedio = (float)auto.PuntosCalidad / golpesParaCompletar;
		auto.Golpes = 0;
		auto.PuntosCalidad = 0;
		auto.Atendido = true;
		auto.GraciaHasta = 0; // si terminó en changüí, ya no espera
		auto.RefrescarVidrio(1f);

		// Labia al máximo: -80% de rechazo (antes inmunidad total).
		float probRechazo = probRechazoEnojado;
		if (Mejoras != null && Mejoras.ExtraNivel("chamuyo", Nv("chamuyo")) == "sin_rechazo")
			probRechazo *= 0.2f;
		if (auto.Humor == "enojado" && GD.Randf() < probRechazo)
		{
			Audio?.Play("bocina");
			UltimoMensaje = "¡Bocinazo! El enojado no te pagó.";
			UltimoTono = "mal";
			return 0;
		}
		double duracion = ahora - auto.InicioServicio;
		float limiteApurado = limiteApuradoSeg + Val("chamuyo");
		if (auto.Humor == "apurado" && duracion > limiteApurado)
		{
			Audio?.Play("bocina");
			UltimoMensaje = $"Tardaste {duracion:0}s. El apurado se fue sin pagar.";
			UltimoTono = "mal";
			return 0;
		}
		int propina = promedio >= 1.7f ? propinaPerfecta
			: promedio >= 1.0f ? propinaBuena : 0;
		if (auto.Humor == "enojado") propina = 0;
		if (auto.Humor == "apurado") propina = Math.Min(propina, propinaBuena);
		// Balde: +$ por servicio según su nivel. Tarifa: base más alta.
		// Tope combinado $12: sin cap el end-game escala x2.7 sin costo.
		int extraBalde = Val("balde");
		int extraTarifa = Val("tarifa");
		int extraEquipo = Mathf.Min(extraTarifa + extraBalde, 12);
		int pago = pagoBase + extraEquipo + propina + Clima.PropinaExtra;
		float multCiudad = Ciudades.Mult(SaveSystem.CargarCiudad());
		pago = Ciudades.AplicarMult(pago);
		Economia?.AddCoins(pago);
		Audio?.PlayCobro(propina);
		if (Economia != null) SaveSystem.Guardar(Economia.Coins);
		var extras = new System.Collections.Generic.List<string>();
		if (extraEquipo > 0) extras.Add($"equipo ${extraEquipo}");
		if (propina > 0) extras.Add($"propina ${propina}");
		if (Clima.PropinaExtra > 0) extras.Add($"lluvia ☔ ${Clima.PropinaExtra}");
		if (multCiudad > 1f) extras.Add($"ciudad x{multCiudad}");
		UltimoMensaje = extras.Count > 0
			? $"¡Vidrio limpio! +${pago} ({string.Join(", ", extras)})"
			: $"¡Vidrio limpio! +${pago}";
		UltimoTono = "ok";
		return pago;
	}

	public void Reset() => Spawner?.ResetServicio();
}
