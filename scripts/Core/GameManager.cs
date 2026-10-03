using Godot;
using System.Text.Json;

/// <summary>
/// Raíz del juego. Carga balance.json y conecta semáforos, economía y HUD.
/// Ciclo en 4 fases (carril 1 → 2 → 3 → 4): cada luz tiene su verde de 12s
/// y su rojo de 45s; nunca hay doble verde.
/// </summary>
public partial class GameManager : Node3D
{
	[Export] public TrafficLight? Semaforo;
	[Export] public TrafficLight? Semaforo2;
	[Export] public TrafficLight? Semaforo3;
	[Export] public TrafficLight? Semaforo4;
	[Export] public Economy? Economia;
	[Export] public HUD? Hud;
	[Export] public PatrolAI? Patrulla;
	[Export] public Mejoras? Mejoras;
	CicloDiaNoche? ciclo;
	Clima? clima;
	string climaActual = "despejado";
	bool restaurarPoliciaPendiente = true;
	double acumAutosave;

	public int VerdeSeg { get; private set; } = 12;
	public int AmarilloSeg { get; private set; } = 3;
	public int RojoSeg { get; private set; } = 45;

	TutorialUI? tutorial;
	HistoriaUI? historia;
	IntroCinematica? intro;
	ViajeCinematica? viaje;
	FinalCinematica? final;

	public override void _Ready()
	{
		Ajustes.Cargar();
		Ajustes.AplicarAlIniciarEscena();
		Semaforo ??= GetNodeOrNull<TrafficLight>("Semaforo");
		Semaforo2 ??= GetNodeOrNull<TrafficLight>("Semaforo2");
		Semaforo3 ??= GetNodeOrNull<TrafficLight>("Semaforo3");
		Semaforo4 ??= GetNodeOrNull<TrafficLight>("Semaforo4");
		Economia ??= GetNodeOrNull<Economy>("Economy");
		Hud ??= GetNodeOrNull<HUD>("HUD");
		Patrulla ??= GetNodeOrNull<PatrolAI>("CarSpawner/Patrulla");
		Mejoras ??= GetNodeOrNull<Mejoras>("Mejoras");
		CargarBalance();
		Semaforo?.ConstruirPortico(new Vector3(0, 0, -5), 180f, 2f);
		Semaforo2?.ConstruirPortico(new Vector3(0, 0, 5), 0f, 2f);
		Semaforo3?.ConstruirPortico(new Vector3(-6, 0, 0), -90f, 2f);
		Semaforo4?.ConstruirPortico(new Vector3(6, 0, 0), 90f, 2f);
		ConfigurarLuz(Semaforo, 0f);
		// Desfases en cuartos de ciclo (nunca hay doble verde): se derivan
		// de los tiempos cargados para que verde/rojo siempre encajen.
		float cuarto = (VerdeSeg + AmarilloSeg + RojoSeg) / 4f;
		ConfigurarLuz(Semaforo2, cuarto * 3f);
		ConfigurarLuz(Semaforo3, cuarto * 2f);
		ConfigurarLuz(Semaforo4, cuarto * 1f);
		if (Economia != null && Hud != null)
		{
			Economia.CoinsChanged += Hud.SetDinero;
			Hud.SetDinero(Economia.Coins);
			Hud.InicializarMinimapa(GetNodeOrNull<Player>("Player"),
				new TrafficLight?[] { Semaforo, Semaforo2, Semaforo3, Semaforo4 }, Mejoras);
		}
		if (Mejoras != null)
		{
			Mejoras.ViajeRealizado += OnViaje;
			Mejoras.CompraRealizada += RefrescarObjetivo;
			Mejoras.FinalDesbloqueado += OnFinal;
		}
		if (Economia != null) Economia.CoinsChanged += OnMonedasParaObjetivo;
		if (Economia != null)
		{
			Economia.GuardadoRobado += OnRoboParaCamara;
			Economia.Decomiso += OnDecomisoParaCamara;
			Economia.GuardadoCambiado += _ => RefrescarObjetivo();
		}
		if (InventarioRef() != null) InventarioRef().Actualizado += RefrescarObjetivo;
		// Paso 5: mundo vivo (día-noche + clima) y restauración de la partida.
		ciclo = new CicloDiaNoche { Name = "CicloDiaNoche" };
		AddChild(ciclo);
		clima = new Clima { Name = "Clima" };
		AddChild(clima);
		ciclo.HoraCambiada += OnHoraReloj;
		ciclo.DiaNuevo += OnDiaReloj;
		clima.ClimaCambiado += OnClimaReloj;
		RestaurarPosicion();
		RefrescarHUD();
		RefrescarObjetivo();
		RefrescarReloj();
		// Peatones ambiente: andan por las veredas y cruzan con rojo.
		var peatones = new PeatonSpawner
		{
			Name = "Peatones",
			Semaforo = Semaforo,
			Semaforo2 = Semaforo2,
			Semaforo3 = Semaforo3,
			Semaforo4 = Semaforo4,
			Trafico = GetNodeOrNull<CarSpawner>("CarSpawner"),
			Economia = Economia,
			Hud = Hud,
			// Robo a medias al salir: el próximo peatón es ladrón seguro.
			ForzarLadronInicial = SaveSystem.CargarLadronProgreso() >= 0.5f && SaveSystem.CargarGuardado() > 0
		};
		AddChild(peatones);
		// Fase 2: prólogo solo en partidas nuevas. Las viejas (con plata,
		// items o guardado pero sin sección historia) migran en silencio.
		if (!SaveSystem.TieneHistoria() &&
			(SaveSystem.CargarCoins() > 0 || SaveSystem.CargarInventario().Length > 0 || SaveSystem.CargarGuardado() > 0))
		{
			SaveSystem.GuardarHistoria(SaveSystem.OficioDefecto, SaveSystem.CiudadDefecto, true, true, null);
		}
		if (!SaveSystem.CargarPrologoVisto())
		{
			// Fase cinemática básica: travelling + subtítulos y después
			// la ruleta de HistoriaUI como plano final (skipeable con Esc).
			intro = new IntroCinematica();
			AddChild(intro);
			intro.Terminada += OnIntroTerminada;
			intro.Mostrar();
			return;
		}
		MostrarTutorialSiToca();
	}

	/// <summary>Terminó la cinemática: recién ahí se muestra la tirada.</summary>
	void OnIntroTerminada()
	{
		if (intro != null)
		{
			intro.Terminada -= OnIntroTerminada;
			intro = null;
		}
		historia = new HistoriaUI();
		AddChild(historia);
		historia.Terminada += OnHistoriaTerminada;
		historia.Mostrar();
	}

	/// <summary>La tirada terminó: plata inicial del oficio, guardado y a laburar.</summary>
	void OnHistoriaTerminada(string oficio)
	{
		if (historia != null)
		{
			historia.Terminada -= OnHistoriaTerminada;
			historia = null;
		}
		int inicial = LeerPlataInicial(oficio);
		if (Economia != null && Economia.Coins < inicial)
			Economia.AddCoins(inicial - Economia.Coins);
		SaveSystem.GuardarTodo(Economia?.Coins ?? 50, SaveSystem.CargarInventario(), 1,
			Economia?.Guardado ?? 0, oficio, SaveSystem.CiudadDefecto, true, true, null);
		// El Player ya cargó (con oficio viejo): que reelija trabajo y herramienta.
		GetNodeOrNull<Player>("Player")?.AplicarOficio(oficio);
		// La tienda también filtró con el oficio viejo: que muestre solo lo suyo.
		GetNodeOrNull<Mejoras>("Mejoras")?.AplicarOficio(oficio);
		RefrescarObjetivo();
		if (Hud != null && Economia != null)
		{
			Hud.SetDinero(Economia.Coins);
			Hud.SetOk($"Te tocó {TiradaSuerte.NombreDe(oficio)}: {TiradaSuerte.DescDe(oficio)}. ¡A laburar en el rojo!");
		}
		// Historia nueva = tutorial jugable siempre (aunque ya se haya visto en
		// otra partida: cada historia arranca desde cero; se salta con Esc).
		MostrarTutorialInteractivo();
	}

	/// <summary>Tutorial jugable: toast abajo sin pausa, cada paso se valida
	/// con una acción real (calle, auto en rojo, cobrar, vereda, mochila, tienda).</summary>
	void MostrarTutorialInteractivo()
	{
		TutorialManager.IniciarTutorial();
		tutorial = new TutorialUI { EnJuego = true };
		AddChild(tutorial);
		tutorial.MostrarInteractivo(0);
		var tj = new TutorialJuego
		{
			Jugador = GetNodeOrNull<Player>("Player"),
			Economia = Economia,
			Mejoras = GetNodeOrNull<Mejoras>("Mejoras"),
			Spawner = GetNodeOrNull<CarSpawner>("CarSpawner"),
			Tutorial = tutorial
		};
		AddChild(tj);
	}

	void MostrarTutorialSiToca()
	{
		if (TutorialManager.DebeMostrarTutorialEnPartida())
		{
			MostrarTutorialInteractivo();
		}
	}

	string ultimoObjetivo = "";

	/// <summary>Viaje: cinemática (el re-skin corre bajo negro) y objetivo al día.</summary>
	void OnViaje(int nueva)
	{
		GetNodeOrNull<Mejoras>("Mejoras")?.Cerrar();
		if (viaje != null) return;
		int origen = Mathf.Max(1, nueva - 1);
		viaje = new ViajeCinematica();
		AddChild(viaje);
		viaje.Terminada += OnViajeTerminada;
		viaje.Mostrar(origen, nueva,
			() => GetNodeOrNull<ProcEdificios>("Edificios")?.Reconstruir(nueva));
	}

	/// <summary>Terminó el viaje: objetivo, HUD y aviso de llegada al día.</summary>
	void OnViajeTerminada()
	{
		if (viaje != null)
		{
			viaje.Terminada -= OnViajeTerminada;
			viaje = null;
		}
		RefrescarObjetivo();
		RefrescarHUD();
		int ciudad = SaveSystem.CargarCiudad();
		Hud?.SetOk($"¡Llegaste a {Ciudades.Nombre(ciudad)}! Acá se paga x{Ciudades.Mult(ciudad)}.");
	}

	/// <summary>Todo al máximo en la última ciudad: el Tipo vuelve con la
	/// tirada final (una sola vez; la tienda ya filtró repetidos).</summary>
	void OnFinal()
	{
		if (final != null || SaveSystem.CargarFinalVisto()) return;
		GetNodeOrNull<Mejoras>("Mejoras")?.Cerrar();
		final = new FinalCinematica();
		AddChild(final);
		final.Terminada += OnFinalTerminada;
		final.Mostrar();
	}

	/// <summary>Terminó el final: sueño cumplido, objetivo y HUD al día.</summary>
	void OnFinalTerminada()
	{
		if (final != null)
		{
			final.Terminada -= OnFinalTerminada;
			final = null;
		}
		RefrescarObjetivo();
		RefrescarHUD();
		Hud?.SetOk("★ Sueño cumplido.");
	}

	/// <summary>Objetivo de ciudad en el HUD (solo se recalcula si cambió).</summary>
	void OnMonedasParaObjetivo(int _n) => RefrescarObjetivo();

	static int LeerPlataInicial(string oficio)
	{
		try
		{
			var path = "res://data/oficios.json";
			if (!FileAccess.FileExists(path)) return 50;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = JsonDocument.Parse(f.GetAsText());
			foreach (var e in doc.RootElement.GetProperty("oficios").EnumerateArray())
			{
				if (e.TryGetProperty("id", out var id) && id.GetString() == oficio &&
					e.TryGetProperty("plata_inicial", out var p))
					return p.GetInt32();
			}
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[GameManager] oficios.json sin plata_inicial: {e.Message}");
		}
		return 50;
	}

	Inventario? InventarioRef() => GetNodeOrNull<Inventario>("Inventario");

	CameraRig? CamaraRef() => GetNodeOrNull<Player>("Player")?.Rig;

	void OnRoboParaCamara(int monto)
	{
		CamaraRef()?.Punch(0.6f);
		RefrescarObjetivo();
	}

	void OnDecomisoParaCamara(int _monto)
	{
		CamaraRef()?.Punch(0.85f);
		RefrescarObjetivo();
	}

	/// <summary>Paso 5: vuelve al jugador a su posición guardada (rpg: save/load).</summary>
	void RestaurarPosicion()
	{
		var pos = SaveSystem.CargarPosJugador();
		if (pos.HasValue)
			GetNodeOrNull<Player>("Player")?.FijarPosicionInicial(new Vector3(pos.Value.X, 0.5f, pos.Value.Y));
	}

	/// <summary>Reloj del HUD por eventos (hora, día, clima). Sin polling.</summary>
	void OnHoraReloj(int _h) => RefrescarReloj();
	void OnDiaReloj(int _d) { RefrescarReloj(); AutosaveMundo(); }
	void OnClimaReloj(string c) { climaActual = c; RefrescarReloj(); }

	void RefrescarReloj()
	{
		if (Hud == null || ciclo == null) return;
		int h = (int)ciclo.HoraActual;
		string icono = h >= 7 && h < 18 ? "☀" : h >= 18 && h < 20 ? "🌇" : "🌙";
		string climaIcono = climaActual == "lluvia" ? "☔ Lluvia" : climaActual == "nublado" ? "☁ Nublado" : "☀ Despejado";
		Hud.SetReloj($"{icono} Día {ciclo.Dia} · {h:00}:00 · {climaIcono}");
	}

	/// <summary>Paso 5: guardado completo al salir (plata + mundo).</summary>
	public void GuardarSalida()
	{
		if (Economia != null) SaveSystem.Guardar(Economia.Coins);
		AutosaveMundo();
	}
	/// <summary>Paso 5: autosave del mundo (día/hora/pos/ladrón/policía).</summary>
	void AutosaveMundo()
	{
		if (ciclo == null) return;
		var jp = GetNodeOrNull<Player>("Player")?.GlobalPosition ?? Vector3.Zero;
		float ladron = -1f;
		foreach (var n in GetTree().GetNodesInGroup("ladron"))
			if (n is Ladron l && IsInstanceValid(l) && l.Acechando)
				ladron = Mathf.Max(ladron, l.NivelRobo());
		if (Patrulla != null && !IsInstanceValid(Patrulla)) Patrulla = null;
		Patrulla ??= BuscarPatrulla();
		SaveSystem.GuardarMundo(ciclo.Dia, ciclo.HoraActual, new Vector2(jp.X, jp.Z),
			ladron, Patrulla?.TiempoVisto() ?? 0f);
	}

	void RefrescarObjetivo()
	{
		if (Hud == null) return;
		int ciudad = SaveSystem.CargarCiudad();
		string texto;
		if (Mejoras != null)
		{
			var (a, t) = Mejoras.ProgresoMejoras();
			int precio = Ciudades.PasajeSiguiente(ciudad);
			texto = precio > 0
				? $"🚂 C{ciudad} {Ciudades.Nombre(ciudad)} · Mejoras {a}/{t} · Pasaje ${Economia?.Coins ?? 0}/${precio} (F)"
				: $"🏁 C{ciudad} {Ciudades.Nombre(ciudad)} · Mejoras {a}/{t}";
		}
		else
		{
			texto = $"🚂 C{ciudad} {Ciudades.Nombre(ciudad)}";
		}
		if (texto != ultimoObjetivo)
		{
			ultimoObjetivo = texto;
			Hud.SetObjetivo(texto);
		}
	}

	void ConfigurarLuz(TrafficLight? luz, float desfase)
	{
		if (luz == null) return;
		luz.Setup(VerdeSeg, AmarilloSeg, RojoSeg);
		luz.Adelantar(desfase);
		luz.LightChanged += _ => RefrescarHUD();
	}

	void CargarBalance()
	{
		var path = "res://data/balance.json";
		if (!FileAccess.FileExists(path)) return;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		var json = f.GetAsText();
		try
		{
			using var doc = JsonDocument.Parse(json);
			var sem = doc.RootElement.GetProperty("semaforo");
			VerdeSeg = sem.GetProperty("verde_seg").GetInt32();
			AmarilloSeg = sem.GetProperty("amarillo_seg").GetInt32();
			RojoSeg = sem.GetProperty("rojo_seg").GetInt32();
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"balance.json inválido, uso defaults: {e.Message}");
		}
	}

	void RefrescarHUD()
	{
		if (Hud == null) return;
		if (Semaforo != null) Hud.SetSemaforo(Semaforo.Estado, Semaforo.TiempoRestante);
		if (Semaforo2 != null) Hud.SetSemaforo2(Semaforo2.Estado, Semaforo2.TiempoRestante);
		if (Semaforo3 != null) Hud.SetSemaforo3(Semaforo3.Estado, Semaforo3.TiempoRestante);
		if (Semaforo4 != null) Hud.SetSemaforo4(Semaforo4.Estado, Semaforo4.TiempoRestante);
		// La patrulla respawnea: si la referencia murió, buscar la nueva.
		if (Patrulla != null && !IsInstanceValid(Patrulla)) Patrulla = null;
		Patrulla ??= BuscarPatrulla();
		float alerta = Patrulla?.NivelAlerta() ?? -1f;
		Hud.SetPolicia(alerta);
		// Pulso de cámara cuando la policía está por decomisar.
		if (alerta > 0.85f) CamaraRef()?.AddShake(0.12f);
		RefrescarLadron();
	}

	float ultimoLadron = -2f;

	/// <summary>Ladrón activo al HUD + ping de cámara al empezar el acecho.</summary>
	void RefrescarLadron()
	{
		if (Hud == null) return;
		float mejor = -1f;
		bool mirado = false;
		foreach (var n in GetTree().GetNodesInGroup("ladron"))
		{
			if (n is Ladron l && IsInstanceValid(l) && l.Acechando)
			{
				float v = l.NivelRobo();
				if (v > mejor) { mejor = v; mirado = l.Mirado; }
			}
		}
		// Flanco: empezó un acecho nuevo -> ping corto de cámara.
		if (ultimoLadron < 0f && mejor >= 0f) CamaraRef()?.AddShake(0.25f);
		ultimoLadron = mejor;
		// El Ladron ya empuja SetLadron cada frame; acá solo garantizamos
		// que el HUD se oculte si el ladrón murió sin avisar.
		if (mejor < 0f) Hud.SetLadron(-1f, false);
		else if (mirado) Hud.SetLadron(mejor, true);
	}

	PatrolAI? BuscarPatrulla()
	{
		var grupo = GetTree().GetNodesInGroup("patrulla");
		foreach (var h in grupo)
			if (h is PatrolAI p && IsInstanceValid(p)) return p;
		var sp = GetNodeOrNull<CarSpawner>("CarSpawner");
		if (sp == null) return null;
		foreach (var h in sp.GetChildren())
			if (h is PatrolAI p && IsInstanceValid(p)) return p;
		return null;
	}

	public override void _ExitTree()
	{
		if (Economia != null && Hud != null) Economia.CoinsChanged -= Hud.SetDinero;
		if (Economia != null)
		{
			Economia.CoinsChanged -= OnMonedasParaObjetivo;
			Economia.GuardadoRobado -= OnRoboParaCamara;
			Economia.Decomiso -= OnDecomisoParaCamara;
		}
		if (Mejoras != null)
		{
			Mejoras.ViajeRealizado -= OnViaje;
			Mejoras.CompraRealizada -= RefrescarObjetivo;
			Mejoras.FinalDesbloqueado -= OnFinal;
		}
		var inv = InventarioRef();
		if (inv != null) inv.Actualizado -= RefrescarObjetivo;
		if (ciclo != null)
		{
			ciclo.HoraCambiada -= OnHoraReloj;
			ciclo.DiaNuevo -= OnDiaReloj;
		}
		if (clima != null) clima.ClimaCambiado -= OnClimaReloj;
		if (historia != null) historia.Terminada -= OnHistoriaTerminada;
		if (intro != null) intro.Terminada -= OnIntroTerminada;
		if (viaje != null) viaje.Terminada -= OnViajeTerminada;
		if (final != null) final.Terminada -= OnFinalTerminada;
	}

	public override void _Process(double delta)
	{
		// Antes era 60fps con strings nuevos cada frame; ahora 6-7fps alcanza:
		// los semáforos avisan por LightChanged y el HUD filtra repetidos.
		acumHud += delta;
		if (acumHud >= 0.15)
		{
			acumHud = 0;
			RefrescarHUD();
		}
		// La patrulla respawnea: restaura su atención guardada una sola vez.
		if (restaurarPoliciaPendiente && Patrulla != null && IsInstanceValid(Patrulla))
		{
			restaurarPoliciaPendiente = false;
			Patrulla.FijarTiempoVisto(SaveSystem.CargarPoliciaTiempo());
		}
		// Autosave del mundo cada 30s (rpg: persistencia sin fricción).
		acumAutosave += delta;
		if (acumAutosave >= 30.0)
		{
			acumAutosave = 0;
			AutosaveMundo();
		}
	}
	double acumHud;
}
