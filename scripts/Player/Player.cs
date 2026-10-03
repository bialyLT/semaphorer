using Godot;

/// <summary>
/// Player CharacterBody3D: WASD + mouse, V cambia cámara, E trabaja.
/// F abre la tienda (cerca del puesto), G usa el escondite, I muestra inventario.
/// Espacio salta (para subir a la vereda). Click captura el mouse.
/// Esc lo libera / cierra tienda.
/// </summary>
public partial class Player : CharacterBody3D
{
	[Export] public float Velocidad = 5f;
	[Export] public CameraRig? Rig;
	[Export] public WindshieldJob? Trabajo;
	[Export] public JugglingJob? Malabares;
	[Export] public VendorJob? Venta;
	[Export] public HUD? Hud;
	[Export] public Mejoras? Mejoras;
	[Export] public Inventario? Inventario;
	[Export] public Economy? Economia;
	[Export] public Pausa? Pausa;
	[Export] public MochilaUI? Mochila;

	/// <summary>Oficio de la partida (lo eligió la tirada). Define qué hace E.</summary>
	public string OficioActual { get; private set; } = SaveSystem.OficioDefecto;
	IJob? trabajoActivo;

	bool MejorasAbierta() => Mejoras != null && Mejoras.Abierta;
	bool MochilaAbierta() => Mochila != null && Mochila.Abierta;
	/// <summary>Con un panel de inventario/tienda abierto no se maneja el juego.</summary>
	bool PanelAbierto() => MejorasAbierta() || MochilaAbierta();

	float yaw = 0f;
	Vector3 spawn;
	MeshInstance3D? cuerpo;
	Camera3D? camara;
	Node3D? herramientaTPS, herramientaFPS, baldeTPS, baldeFPS;
	/// <summary>Pivotes de la persona para balancear al caminar (ver ProcPeaton).</summary>
	Node3D? pataIzqP, pataDerP, brazoIzqP, brazoDerP;
	float faseAndar = 0f;

	public override void _Ready()
	{
		spawn = GlobalPosition;
		Mejoras.CargarEscondite();
		CargarBalanceJugador();
		if (Rig == null) Rig = GetNodeOrNull<CameraRig>("CameraRig");
		if (Trabajo == null) Trabajo = GetNodeOrNull<WindshieldJob>("WindshieldJob");
		if (Hud == null) Hud = GetNodeOrNull<HUD>("../HUD");
		if (Mejoras == null) Mejoras = GetNodeOrNull<Mejoras>("../Mejoras");
		if (Inventario == null) Inventario = GetNodeOrNull<Inventario>("../Inventario");
		if (Economia == null) Economia = GetNodeOrNull<Economy>("../Economy");
		if (Pausa == null) Pausa = GetNodeOrNull<Pausa>("../Pausa");
		if (Mochila == null) Mochila = GetNodeOrNull<MochilaUI>("../MochilaUI");
		// Si la escena no lo trae, se crea acá como hijo del Player.
		// CanvasLayer se dibuja igual en cualquier rama del árbol, y las
		// rutas ../Economy solo resuelven si ya está en el árbol: por eso
		// se agrega directo (agregarlo al padre en _Ready falla porque el
		// padre está ocupado instanciando al resto de sus hijos).
		if (Mochila == null)
		{
			Mochila = new MochilaUI { Name = "MochilaUI", Inventario = Inventario, Mejoras = Mejoras, Economia = Economia };
			AddChild(Mochila);
		}
		FloorSnapLength = 0.2f;
		// Capa 4 = muros invisibles del borde (ProcCalle): solo el jugador
		// choca con ellos (autos/peatones van por posición, el rayo del
		// ladrón usa máscara 1).
		CollisionMask |= 4;
		cuerpo = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		camara = GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
		VestirLaburante();
		AsegurarOficios();
		RefrescarLimpiavidrios();
		AplicarVisibilidadCuerpo();
		AplicarVisibilidadHerramienta();
		SuscribirEventosTrabajos();
		Hud?.SetAyudaOficio(OficioActual);
		// Cualquier cambio de niveles (compra con F o decomiso policial)
		// rehace la herramienta y el balde.
		if (Inventario != null) Inventario.Actualizado += RefrescarLimpiavidrios;
		// El inventario se carga DESPUÉS que el Player: ahí se refresca la skin.
		if (Inventario != null) Inventario.Cargado += RefrescarLimpiavidrios;
	}

	public override void _ExitTree()
	{
		if (Inventario != null)
		{
			Inventario.Actualizado -= RefrescarLimpiavidrios;
			Inventario.Cargado -= RefrescarLimpiavidrios;
		}
		DesuscribirEventosTrabajos();
	}

	/// <summary>Skin según la esponja: dorada (Nv3) > roja (Nv1+) > básica.</summary>
	string SkinActual()
	{
		if (Inventario == null) return "basico";
		int esponja = Inventario.Nivel("esponja");
		if (esponja >= 3) return "dorado";
		if (esponja >= 1) return "rojo";
		return "basico";
	}

	/// <summary>
	/// Saca el nodo viejo del padre en el acto: con solo QueueFree queda
	/// un frame dibujado junto al nuevo y el nombre choca al re-agregar
	/// (Godot le pone @Node3D@id).
	/// </summary>
	static void Descartar(Node? padre, Node3D? viejo)
	{
		if (viejo == null) return;
		padre?.RemoveChild(viejo);
		viejo.QueueFree();
	}

	/// <summary>
	/// Los jobs nuevos se crean por código (hijos del Player, como el
	/// WindshieldJob del .tscn) y se cablean con las mismas referencias.
	/// El activo lo define el oficio guardado por la tirada.
	/// </summary>
	void AsegurarOficios()
	{
		if (Malabares == null)
		{
			Malabares = new JugglingJob { Name = "JugglingJob" };
			AddChild(Malabares);
		}
		if (Venta == null)
		{
			Venta = new VendorJob { Name = "VendorJob" };
			AddChild(Venta);
		}
		// Cableado explícito (además del fallback por rutas de cada job).
		if (Trabajo != null)
		{
			if (Malabares.Spawner == null) Malabares.Spawner = Trabajo.Spawner;
			if (Venta.Spawner == null) Venta.Spawner = Trabajo.Spawner;
		}
		if (Malabares.Economia == null) Malabares.Economia = Economia;
		if (Venta.Economia == null) Venta.Economia = Economia;
		if (Malabares.Mejoras == null) Malabares.Mejoras = Mejoras;
		if (Venta.Mejoras == null) Venta.Mejoras = Mejoras;
		OficioActual = NormalizarOficio(SaveSystem.CargarOficio());
		trabajoActivo = ElegirTrabajo(OficioActual, Trabajo, Malabares, Venta);
		if (trabajoActivo == null) trabajoActivo = Trabajo;
	}

	/// <summary>Normaliza ids (" Vendedor " → "vendedor", null → defecto).</summary>
	static string NormalizarOficio(string? oficio)
	{
		string id = (oficio ?? "").Trim().ToLowerInvariant();
		return string.IsNullOrEmpty(id) ? SaveSystem.OficioDefecto : id;
	}

	/// <summary>
	/// Fuente única trabajo↔oficio: "malabarista" → Malabares,
	/// "vendedor" → Venta, resto → limpiavidrios.
	/// Así E siempre habla con el job de TU profesión y nunca con otro rubro.
	/// </summary>
	static IJob? ElegirTrabajo(string oficio, WindshieldJob? base_, JugglingJob? mal, VendorJob? ven)
	{
		return oficio switch
		{
			"malabarista" => (IJob?)mal ?? base_,
			"vendedor" => (IJob?)ven ?? base_,
			_ => base_,
		};
	}

	/// <summary>
	/// La tirada termina DESPUÉS del _Ready (el Player carga antes que el
	/// GameManager muestre el prólogo): reelige trabajo y herramienta con el
	/// oficio real. También vale si el oficio cambiara más adelante.
	/// </summary>
	public void AplicarOficio(string oficio)
	{
		DesuscribirEventosTrabajos();
		OficioActual = NormalizarOficio(oficio);
		trabajoActivo = ElegirTrabajo(OficioActual, Trabajo, Malabares, Venta);
		if (trabajoActivo == null) trabajoActivo = Trabajo;
		SuscribirEventosTrabajos();
		RefrescarLimpiavidrios();
		AplicarVisibilidadHerramienta();
		Hud?.SetAyudaOficio(OficioActual);
	}

	void SuscribirEventosTrabajos()
	{
		// El verde lo procesa SOLO WindshieldJob (siempre en escena) sobre
		// contadores compartidos: se escucha solo a él. Escuchar a los 3
		// triplicaba avisos y mezclaba mensajes entre oficios.
		// E (IntentarTrabajar) sí usa trabajoActivo, así cada profesión
		// muestra SOLO sus mensajes (Limpiando / Función / Ofreciendo).
		var fuente = Trabajo ?? trabajoActivo;
		if (fuente != null) { fuente.ProgresoReseteado += OnProgresoPerdido; fuente.GraciaOtorgada += OnGracia; }
	}

	void DesuscribirEventosTrabajos()
	{
		var fuente = Trabajo ?? trabajoActivo;
		if (fuente != null) { fuente.ProgresoReseteado -= OnProgresoPerdido; fuente.GraciaOtorgada -= OnGracia; }
		// Por si AplicarOficio cambió la fuente a mitad de partida, desuscribir
		// también el resto sin romper (quitar un handler ausente no falla).
		if (Trabajo != null && Trabajo != fuente) { Trabajo.ProgresoReseteado -= OnProgresoPerdido; Trabajo.GraciaOtorgada -= OnGracia; }
		if (Malabares != null && Malabares != fuente) { Malabares.ProgresoReseteado -= OnProgresoPerdido; Malabares.GraciaOtorgada -= OnGracia; }
		if (Venta != null && Venta != fuente) { Venta.ProgresoReseteado -= OnProgresoPerdido; Venta.GraciaOtorgada -= OnGracia; }
	}
	/// <summary>
	/// Herramienta en mano según oficio: limpiavidrios (esponja/balde),
	/// pelotas de tenis o palo con verdura (+canasta).
	/// En 3ª va en el cuerpo, en 1ª en la cámara estilo viewmodel.
	/// </summary>
	void RefrescarLimpiavidrios()
	{
		Descartar(cuerpo, herramientaTPS);
		Descartar(camara, herramientaFPS);
		Descartar(cuerpo, baldeTPS);
		Descartar(camara, baldeFPS);
		herramientaTPS = null; herramientaFPS = null; baldeTPS = null; baldeFPS = null;
		if (OficioActual == "malabarista")
		{
			int pel = Inventario != null ? Inventario.Nivel("pelotas") : 0;
			herramientaTPS = HerramientaOficio.BuildPelotas(pel);
			herramientaTPS.Position = new Vector3(0.42f, 0.05f, -0.35f);
			cuerpo?.AddChild(herramientaTPS);
			herramientaFPS = HerramientaOficio.BuildPelotas(pel);
			herramientaFPS.Position = new Vector3(0.3f, -0.26f, -0.55f);
			herramientaFPS.Scale = Vector3.One * 0.8f;
			camara?.AddChild(herramientaFPS);
		}
		else if (OficioActual == "vendedor")
		{
			int can = Inventario != null ? Inventario.Nivel("canasta") : 0;
			herramientaTPS = HerramientaOficio.BuildPalo();
			herramientaTPS.Position = new Vector3(0.3f, 0.35f, -0.1f);
			herramientaTPS.RotationDegrees = new Vector3(0, 0, 70);
			cuerpo?.AddChild(herramientaTPS);
			baldeTPS = HerramientaOficio.BuildCanasta(can);
			if (baldeTPS != null)
			{
				baldeTPS.Position = new Vector3(-0.42f, -0.05f, -0.06f);
				cuerpo?.AddChild(baldeTPS);
			}
			herramientaFPS = HerramientaOficio.BuildPalo();
			herramientaFPS.Position = new Vector3(0.3f, -0.1f, -0.55f);
			herramientaFPS.RotationDegrees = new Vector3(0, 0, 70);
			herramientaFPS.Scale = Vector3.One * 0.8f;
			camara?.AddChild(herramientaFPS);
			baldeFPS = HerramientaOficio.BuildCanasta(can);
			if (baldeFPS != null)
			{
				baldeFPS.Position = new Vector3(-0.36f, -0.42f, -0.7f);
				baldeFPS.Scale = Vector3.One * 0.8f;
				camara?.AddChild(baldeFPS);
			}
		}
		else
		{
			RefrescarHerramientaLimpiavidrios();
		}
		AplicarVisibilidadHerramienta();
	}

	void RefrescarHerramientaLimpiavidrios()
	{
		string skin = SkinActual();
		int balde = Inventario != null ? Inventario.Nivel("balde") : 0;
		Descartar(cuerpo, herramientaTPS);
		Descartar(camara, herramientaFPS);
		Descartar(cuerpo, baldeTPS);
		Descartar(camara, baldeFPS);
		herramientaTPS = Limpiavidrios.Build(skin);
		herramientaTPS.Position = new Vector3(0.42f, 0.05f, -0.35f);
		herramientaTPS.RotationDegrees = new Vector3(-18, 168, 0);
		cuerpo?.AddChild(herramientaTPS);
		baldeTPS = Limpiavidrios.BuildBalde(balde);
		if (baldeTPS != null)
		{
			baldeTPS.Position = new Vector3(-0.42f, -0.05f, -0.06f);
			cuerpo?.AddChild(baldeTPS);
		}
		herramientaFPS = Limpiavidrios.Build(skin);
		herramientaFPS.Position = new Vector3(0.3f, -0.26f, -0.55f);
		herramientaFPS.RotationDegrees = new Vector3(-8, 162, 0);
		herramientaFPS.Scale = Vector3.One * 0.8f;
		camara?.AddChild(herramientaFPS);
		baldeFPS = Limpiavidrios.BuildBalde(balde);
		if (baldeFPS != null)
		{
			baldeFPS.Position = new Vector3(-0.36f, -0.42f, -0.7f);
			baldeFPS.RotationDegrees = new Vector3(0, 20, 0);
			baldeFPS.Scale = Vector3.One * 0.8f;
			camara?.AddChild(baldeFPS);
		}
		AplicarVisibilidadHerramienta();
	}

	void AplicarVisibilidadHerramienta()
	{
		bool enPrimera = Rig == null || Rig.PrimeraPersona;
		if (herramientaFPS != null) herramientaFPS.Visible = enPrimera;
		if (baldeFPS != null) baldeFPS.Visible = enPrimera;
	}

	void OnProgresoPerdido()
	{
		Hud?.SetProgreso(0f);
		Hud?.SetError("Verde: el auto arrancó sin pagar. Progreso perdido.");
	}

	void OnGracia()
	{
		Hud?.SetOk("Verde: el amable te espera unos segundos. ¡Terminálo!");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Solo clicks reales capturan el mouse (la rueda es scroll, no click),
		// y nunca con menús abiertos (si no el cursor desaparece scrolleando).
		if (@event is InputEventMouseButton mb && mb.Pressed
			&& !PanelAbierto() && !(Pausa?.Abierta() ?? false)
			&& (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
			Input.MouseMode = Input.MouseModeEnum.Captured;
		if (@event is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			yaw -= mm.Relative.X * Ajustes.Sensibilidad;
			Rotation = new Vector3(0, yaw, 0);
			Rig?.SumarPitch(mm.Relative.Y);
		}
		if (@event is InputEventKey k && k.Pressed && !k.Echo)
		{
			if (k.PhysicalKeycode == Key.V) { Rig?.Alternar(); AplicarVisibilidadCuerpo(); AplicarVisibilidadHerramienta(); }
			if (k.PhysicalKeycode == Key.E) IntentarTrabajar();
			if (k.PhysicalKeycode == Key.F) Mejoras?.Alternar();
			if (k.PhysicalKeycode == Key.G) UsarEscondite();
			if (k.PhysicalKeycode == Key.I) Mochila?.Alternar();
			if (k.Keycode == Key.Escape)
			{
				if (MochilaAbierta()) Mochila?.Cerrar();
				else if (MejorasAbierta()) Mejoras?.Cerrar();
				else Pausa?.Alternar();
			}
		}
	}

	/// <summary>¿Está sobre la calzada (no en la vereda)? La policía mira esto.</summary>
	public bool EstaEnCalle()
	{
		var p = GlobalPosition;
		return Mathf.Abs(p.X) < 4.2f || Mathf.Abs(p.Z) < 4.2f;
	}

	public void VolverAlSpawn()
	{
		GlobalPosition = spawn;
		Velocity = Vector3.Zero;
	}

	/// <summary>Paso 5: restaura la posición guardada al cargar partida.</summary>
	public void FijarPosicionInicial(Vector3 p)
	{
		spawn = p;
		GlobalPosition = p;
		Velocity = Vector3.Zero;
	}

	void CargarBalanceJugador()
	{
		try
		{
			var path = "res://data/balance.json";
			if (!FileAccess.FileExists(path)) return;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = System.Text.Json.JsonDocument.Parse(f.GetAsText());
			if (doc.RootElement.TryGetProperty("jugador", out var j) &&
				j.TryGetProperty("velocidad", out var v))
				Velocidad = (float)v.GetDouble();
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Player] balance sin jugador: {e.Message}");
		}
	}

	/// <summary>
	/// Escondite del puesto (G cerca): guarda toda la plata encima;
	/// si no llevás nada y hay guardado, lo sacás. La policía solo
	/// decomisa lo que llevás encima.
	/// </summary>
	void UsarEscondite()
	{
		if (Economia == null || Mejoras == null || Hud == null) return;
		if (GlobalPosition.DistanceTo(Mejoras.Escondite) > Mejoras.RadioEscondite)
		{
			Hud.SetMensaje("El escondite está en la vereda este, cerca de la esquina (mochila azul).");
			return;
		}
		if (Economia.Coins > 0)
		{
			int monto = Economia.Coins;
			Economia.DepositarTodo();
			Hud.SetOk($"Guardaste ${monto} en el escondite (a salvo: ${Economia.Guardado}).");
		}
		else if (Economia.Guardado > 0)
		{
			Economia.RetirarTodo();
			Hud.SetOk($"Sacaste la plata del escondite: ${Economia.Coins} encima.");
		}
		else
		{
			Hud.SetMensaje("Escondite vacío y bolsillos vacíos. A laburar.");
		}
	}

	void AplicarVisibilidadCuerpo()
	{
		// En 1ª persona el cuerpo tapaba la vista (cámara dentro de la cabeza).
		if (cuerpo == null) return;
		cuerpo.Visible = Rig == null || !Rig.PrimeraPersona;
	}

	/// <summary>Balanceo de piernas y brazos con la velocidad de marcha.</summary>
	void AnimarAndar(float d)
	{
		float h = new Vector2(Velocity.X, Velocity.Z).Length();
		if (h > 0.2f) faseAndar += h * d * 2.4f;
		float fase = Mathf.Sin(faseAndar) * Mathf.Clamp(h / Velocidad, 0f, 1f);
		if (pataIzqP != null) pataIzqP.Rotation = new Vector3(fase * 0.5f, 0, 0);
		if (pataDerP != null) pataDerP.Rotation = new Vector3(-fase * 0.5f, 0, 0);
		if (brazoIzqP != null) brazoIzqP.Rotation = new Vector3(-fase * 0.35f, 0, 0);
		if (brazoDerP != null) brazoDerP.Rotation = new Vector3(fase * 0.35f, 0, 0);
	}

	/// <summary>
	/// Ya no es un globo: el capsule pasa a ser el perchero de una persona
	/// procedural (chaleco naranja, pantalón azul, cabeza y gorra azul) que
	/// balancea piernas y brazos al caminar. Todo queda colgado del torso,
	/// así que se oculta junto en 1ª persona.
	/// </summary>
	void VestirLaburante()
	{
		if (cuerpo == null) return;
		cuerpo.Mesh = null; // se acabó la cápsula gris
		var persona = ProcPeaton.Build(
			new Color(1, 0.45f, 0.08f),   // chaleco naranja
			new Color(0.2f, 0.25f, 0.4f), // pantalón azul
			new Color(0.85f, 0.6f, 0.45f),
			true,
			new Color(0.1f, 0.2f, 0.6f)); // gorra azul
		// El cuerpo está a 0.9 del origen: la persona apoya los pies en el suelo.
		persona.Position = new Vector3(0, -0.9f, 0);
		cuerpo.AddChild(persona);
		pataIzqP = persona.GetNodeOrNull<Node3D>("Cuerpo/PataIzq");
		pataDerP = persona.GetNodeOrNull<Node3D>("Cuerpo/PataDer");
		brazoIzqP = persona.GetNodeOrNull<Node3D>("Cuerpo/BrazoIzq");
		brazoDerP = persona.GetNodeOrNull<Node3D>("Cuerpo/BrazoDer");
	}

	public override void _PhysicsProcess(double delta)
	{
		var dir = new Vector3();
		if (!PanelAbierto())
		{
			var basis = GlobalTransform.Basis;
			if (Input.IsPhysicalKeyPressed(Key.W)) dir -= basis.Z;
			if (Input.IsPhysicalKeyPressed(Key.S)) dir += basis.Z;
			if (Input.IsPhysicalKeyPressed(Key.A)) dir -= basis.X;
			if (Input.IsPhysicalKeyPressed(Key.D)) dir += basis.X;
			dir.Y = 0; dir = dir.Normalized();
		}
		float vel = Velocidad * (1f + (Mejoras != null ? Mejoras.ValorNivel("zapas", Mejoras.NivelDe("zapas")) : 0) / 100f);

		float vy = Velocity.Y - 20f * (float)delta;
		if (IsOnFloor() && !PanelAbierto() && Input.IsPhysicalKeyPressed(Key.Space))
			vy = 4.8f; // salto para subir a la vereda
		Velocity = new Vector3(dir.X * vel, vy, dir.Z * vel);
		MoveAndSlide();
		// Garantía dura: ni el empujón de un auto te saca del mundo. El muro
		// frena lo normal, pero un empujón fuerte puede tunelarlo en 1 frame
		// (o dejarte afuera de un tirón): el clamp te trae adentro siempre.
		// El clamp es dinámico: se estira con la cola de autos detenidos
		// (hasta el borde del suelo) para alcanzar al último de la fila.
		GlobalPosition = AplicarLimites(GlobalPosition, MedioEjeDinamico());
		AnimarAndar((float)delta);
		// Red de seguridad: si algo falla y caes del mundo, reapareces en el spawn.
		if (GlobalPosition.Y < -10f)
		{
			GlobalPosition = spawn;
			Velocity = Vector3.Zero;
			Hud?.SetError("¡Te caíste del mundo! Reapareciste en la banqueta.");
		}
	}

	/// <summary>
	/// Borde jugable: base pegada al cruce, techo en el borde del suelo.
	/// El límite real es dinámico (ver AlcanceCola): crece con la cola de
	/// autos detenidos para llegar al último, nunca pasa el suelo.
	/// </summary>
	public const float LimiteMundo = 17.5f;
	public const float LimiteBase = 17.5f;
	public const float LimiteSuelo = 29f;

	/// <summary>Clamp al área base (sin cola: igual que antes).</summary>
	public static Vector3 AplicarLimites(Vector3 p) => AplicarLimites(p, LimiteBase);

	/// <summary>Clamp puro al área jugable (testeable sin instanciar Player).</summary>
	public static Vector3 AplicarLimites(Vector3 p, float medioEje)
	{
		float m = Mathf.Clamp(medioEje, LimiteBase, LimiteSuelo);
		p.X = Mathf.Clamp(p.X, -m, m);
		p.Z = Mathf.Clamp(p.Z, -m, m);
		return p;
	}

	/// <summary>
	/// Medio eje jugable según la cola actual: base sin autos detenidos,
	/// hasta el último de la fila (+margen) con autos, tope en el suelo.
	/// </summary>
	float MedioEjeDinamico()
	{
		CarSpawner? sp = Trabajo?.Spawner ?? Malabares?.Spawner ?? Venta?.Spawner;
		if (sp == null) return LimiteBase;
		return Mathf.Clamp(Mathf.Max(LimiteBase, sp.AlcanceCola()), LimiteBase, LimiteSuelo);
	}

	void IntentarTrabajar()
	{
		// SOLO el trabajo activo habla: cada profesión muestra sus mensajes
		// (limpiavidrios "Limpiando.../¡Vidrio limpio!", malabarista
		// "Función.../¡Función completa!", vendedor "Ofreciendo.../¡Vendido!").
		IJob? trabajo = trabajoActivo ?? Trabajo;
		if (trabajo == null || Hud == null) return;
		if (PanelAbierto())
		{
			Hud.SetMensaje(MochilaAbierta()
				? "Cerrá la mochila (I) para seguir trabajando."
				: "Cerrá la tienda (F) para seguir trabajando.");
			return;
		}
		if (!trabajo.PuedeTrabajar())
		{
			Hud.SetMensaje(trabajo.Motivo());
			return;
		}
		int pago = trabajo.Actuar();
		Hud.SetProgreso(trabajo.Progreso01);
		if (trabajo.UltimoTono == "ok") Hud.SetOk(trabajo.UltimoMensaje);
		else if (trabajo.UltimoTono == "mal") Hud.SetError(trabajo.UltimoMensaje);
		else Hud.SetMensaje(trabajo.UltimoMensaje);
		if (pago > 0) Hud.SetProgreso(0f);
	}
}
