using Godot;

/// <summary>
/// Spawnea 3 autos procedurales en carriles (cada uno obedece a su semáforo).
/// Al frenar en rojo les sortea suciedad y humor del conductor (Paso 2).
/// </summary>
public partial class CarSpawner : Node3D
{
	[Export] public TrafficLight? Semaforo;
	[Export] public TrafficLight? Semaforo2;
	[Export] public TrafficLight? Semaforo3;
	[Export] public TrafficLight? Semaforo4;
	[Export] public float Velocidad = 6f;
	[Export] public Player? Jugador;
	[Export] public AudioManager? Audio;
	[Export] public Mejoras? Mejoras;

	readonly System.Collections.Generic.List<CarAI> autos = new();
	readonly RandomNumberGenerator rng = new();

	// Pesos del sorteo de humor (amable / apurado / enojado). Se leen de balance.json.
	float PAmable = 0.5f;
	float PApurado = 0.3f;
	// Si te parás en la trayectoria, frenan y tocan bocina (cooldown global).
	float DistanciaBocina = 4.5f;
	float VelocidadPatrulla = 7f;
	float GapAntisolape = 6f;
	double ultimoBocinazo = -99;
	/// <summary>Lado del próximo transversal (alterna L3/L4).</summary>
	bool eoLadoL3 = false;

	readonly Color[] ColSedan = {
		new(0.8f, 0.2f, 0.2f), new(0.2f, 0.4f, 0.9f), new(0.25f, 0.65f, 0.4f),
		new(0.5f, 0.5f, 0.55f), new(0.4f, 0.65f, 0.8f)
	};
	readonly Color[] ColTaxi = {
		new(0.95f, 0.8f, 0.15f), new(0.2f, 0.7f, 0.3f), new(0.6f, 0.6f, 0.65f)
	};
	readonly Color[] ColBus = {
		new(0.2f, 0.4f, 0.9f), new(0.9f, 0.5f, 0.1f), new(0.85f, 0.85f, 0.88f)
	};
	/// <summary>¿Algún auto frenó por el jugador este frame? (la policía lo mira).</summary>
	public bool JugadorObstruye { get; private set; }

	public override void _Ready()
	{
		rng.Randomize();
		// Fallback por si el export del .tscn no enlazó (clases C# no globales).
		Semaforo ??= GetNodeOrNull<TrafficLight>("../Semaforo");
		Semaforo2 ??= GetNodeOrNull<TrafficLight>("../Semaforo2");
		Semaforo3 ??= GetNodeOrNull<TrafficLight>("../Semaforo3");
		Semaforo4 ??= GetNodeOrNull<TrafficLight>("../Semaforo4");
		Jugador ??= GetNodeOrNull<Player>("../Player");
		Audio ??= GetNodeOrNull<AudioManager>("../Audio");
		Mejoras ??= GetNodeOrNull<Mejoras>("../Mejoras");
		if (Semaforo == null)
			GD.PushWarning("[Spawner] sin semáforo: los autos no van a frenar.");
		CargarBalance();
		// Todo (patrulla incluida) lo rellena CompletarTrafico con
		// autos aleatorios (tipo, color, carril y maniobra).
	}

	void CargarBalance()
	{
		var path = "res://data/balance.json";
		if (!FileAccess.FileExists(path)) return;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		try
		{
			using var doc = System.Text.Json.JsonDocument.Parse(f.GetAsText());
			if (!doc.RootElement.TryGetProperty("trafico", out var t)) return;
			if (t.TryGetProperty("velocidad_auto", out var v)) Velocidad = (float)v.GetDouble();
			if (t.TryGetProperty("velocidad_patrulla", out v)) VelocidadPatrulla = (float)v.GetDouble();
			if (t.TryGetProperty("p_amable", out v)) PAmable = (float)v.GetDouble();
			if (t.TryGetProperty("p_apurado", out v)) PApurado = (float)v.GetDouble();
			if (t.TryGetProperty("distancia_bocina_m", out v)) DistanciaBocina = (float)v.GetDouble();
			if (t.TryGetProperty("gap_antisolape_m", out v)) GapAntisolape = (float)v.GetDouble();
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Spawner] balance.json inválido, uso defaults: {e.Message}");
		}
	}

	/// <summary>
	/// La patrulla respawnea como el resto: sale por un lado con sentido,
	/// ruta y luz al azar, cruza, dobla o sigue, y al salir viene otra.
	/// Va por su carril (z=±2) como todos, no por el medio.
	/// </summary>
	void SpawnearPatrulla(int direccion)
	{
		var p = new PatrolAI
		{
			Name = "Patrulla",
			SemaforoEste = Semaforo3,
			SemaforoOeste = Semaforo4,
			Semaforo = direccion > 0 ? Semaforo3 : Semaforo4,
			Velocidad = VelocidadPatrulla,
			Direccion = direccion,
			EjeX = true,
			Vaiven = false,
			LineaDetencion = direccion > 0 ? -14f : 14f,
			MitadLargo = 2f,
			TamanoCol = ProcAuto.Tamano("patrulla"),
			Spawner = this,
			Position = direccion > 0 ? new Vector3(-35, 0, 2) : new Vector3(35, 0, -2)
		};
		p.RotationDegrees = new Vector3(0, direccion > 0 ? 90 : -90, 0);
		p.SetRuta(CarAI.RutaPara(direccion > 0 ? 3 : 4, ManiobraRandom()));
		// OJO: primero al árbol (el _Ready crea el visual hijo 0),
		// después la colisión (hijo 1, como en los autos).
		AddChild(p);
		p.AddChild(ProcAuto.Colision("patrulla"));
		autos.Add(p);
	}

	void Spawnear(string tipo, Color color, Vector3 pos, int direccion, float lineaZ, TrafficLight? sem, int carril)
	{
		var visual = ProcAuto.Build(tipo, color);
		var car = new CarAI
		{
			Semaforo = sem,
			Velocidad = Velocidad,
			Direccion = direccion,
			LineaDetencion = lineaZ,
			MitadLargo = tipo == "bus" ? 3.5f : 2f,
			TamanoCol = ProcAuto.Tamano(tipo)
		};
		car.Position = pos;
		// Si va en sentido contrario, girar 180°
		if (direccion < 0) car.RotationDegrees = new Vector3(0, 180, 0);
		car.SetRuta(CarAI.RutaPara(carril, ManiobraRandom()));
		car.AddChild(visual);
		// Colisión para que el jugador no atraviese el auto
		car.AddChild(ProcAuto.Colision(tipo));
		AddChild(car);
		autos.Add(car);
	}

	int ManiobraRandom()
	{
		float m = rng.Randf();
		return m < 0.5f ? 0 : (m < 0.75f ? 1 : 2); // 50% recto, 25% izq, 25% der
	}

	/// <summary>
	/// Separación manual jugador-autos: el motor físico ignora los cuerpos
	/// cinemáticos de los autos, así que garantizamos el contacto a mano
	/// (cápsula 0.4 contra caja del auto). Corre antes que el Player.
	/// </summary>
	public override void _PhysicsProcess(double _delta)
	{
		if (Jugador == null) return;
		Vector3 pp = Jugador.GlobalPosition;
		if (pp.Y > 1.6f || pp.Y < -1f) return; // sobre el techo o caído: no empujar
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c)) continue;
			Vector3 cp = c.GlobalPosition;
			// La patrulla gira 90°: su caja queda cruzada (se permutan X/Z).
			Vector3 t = c.EjeX
				? new Vector3(c.TamanoCol.Z, c.TamanoCol.Y, c.TamanoCol.X)
				: c.TamanoCol;
			float hx = t.X / 2f + 0.4f;
			float hz = t.Z / 2f + 0.4f;
			float dx = pp.X - cp.X, dz = pp.Z - cp.Z;
			float penX = hx - Mathf.Abs(dx), penZ = hz - Mathf.Abs(dz);
			if (penX > 0f && penZ > 0f)
			{
				if (penX < penZ) pp.X = cp.X + (dx >= 0f ? hx : -hx);
				else pp.Z = cp.Z + (dz >= 0f ? hz : -hz);
			}
		}
		Jugador.GlobalPosition = pp;
	}

	public override void _Process(double _delta)
	{
		// Purga los que terminaron su ruta (QueueFree) para respawnear otros.
		autos.RemoveAll(c => !IsInstanceValid(c));
		// Anti-solape simple: si un auto tiene otro del mismo carril/sentido
		// pegado delante (<6m), el de atrás espera aunque el semáforo esté verde.
		foreach (var c in autos) c.BloqueadoPorTrafico = false;
		for (int i = 0; i < autos.Count; i++)
		{
			for (int j = 0; j < autos.Count; j++)
			{
				if (i == j) continue;
				var atras = autos[i];
				var delante = autos[j];
				if (!IsInstanceValid(atras) || !IsInstanceValid(delante)) continue;
				if (atras.EjeX != delante.EjeX) continue; // ejes distintos no se cruzan
				if (atras.Direccion != delante.Direccion) continue;
				if (atras.EjeX)
				{
					if (Mathf.Abs(atras.Position.Z - delante.Position.Z) > 0.5f) continue;
					float gapX = (delante.Position.X - atras.Position.X) * atras.Direccion;
					if (gapX > 0f && gapX < GapAntisolape) atras.BloqueadoPorTrafico = true;
				}
				else
				{
					if (Mathf.Abs(atras.Position.X - delante.Position.X) > 0.5f) continue;
					float gap = (delante.Position.Z - atras.Position.Z) * atras.Direccion;
					if (gap > 0f && gap < GapAntisolape) atras.BloqueadoPorTrafico = true;
				}
			}
		}
		FrenarPorJugador();
		CompletarTrafico();
		// Sorteo Paso 2: auto recién detenido en rojo → suciedad + humor.
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c)) continue;
			if (!c.EstaDetenido() || c.Asignado) continue;
			c.Suciedad = 0.3f + rng.Randf() * 0.7f;
			float h = rng.Randf();
			c.Humor = h < PAmable ? "amable" : h < PAmable + PApurado ? "apurado" : "enojado";
			c.Asignado = true;
			c.RefrescarVidrio(0f);
		}
	}

	/// <summary>
	/// Si el jugador está parado en la trayectoria del auto (mismo carril,
	/// delante a menos de 4.5m, a nivel de calle), el auto frena y toca bocina.
	/// Los conductores no te pasan por encima: te detienen el tráfico.
	/// </summary>
	void FrenarPorJugador()
	{
		if (Jugador == null) { JugadorObstruye = false; return; }
		Vector3 pj = Jugador.GlobalPosition;
		if (pj.Y > 1.2f) { JugadorObstruye = false; return; } // saltó arriba (ej: sobre el techo): no frenan
		bool alguienFreno = false;
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c)) continue;
			float lateral = c.EjeX ? Mathf.Abs(c.Position.Z - pj.Z) : Mathf.Abs(c.Position.X - pj.X);
			if (lateral > 1.3f) continue;
			float gap = c.EjeX ? (pj.X - c.Position.X) * c.Direccion : (pj.Z - c.Position.Z) * c.Direccion;
			if (gap > 0f && gap < DistanciaBocina)
			{
				c.BloqueadoPorTrafico = true;
				alguienFreno = true;
			}
		}
		JugadorObstruye = alguienFreno;
		double ahora = Time.GetTicksMsec() / 1000.0;
		if (alguienFreno && ahora - ultimoBocinazo > 2.5)
		{
			ultimoBocinazo = ahora;
			Audio?.Play("bocina");
		}
	}

	/// <summary>
	/// Tráfico equilibrado y aleatorio en los 4 carriles: base uno por carril
	/// y los extras de Tráfico al 1 y al 2. Tipo, color y maniobra (recto o
	/// giros) se sortean; al salir del mapa el auto se va y viene otro.
	/// Spawnea de a uno por frame y evita amontonar en la salida.
	/// </summary>
	void CompletarTrafico()
	{
		int extra = Mejoras != null ? Mejoras.ValorNivel("trafico", Mejoras.NivelDe("trafico")) : 0;
		// Progresión rala: base L1+L2 (2), Nv1 suma UN transversal alternado (3),
		// Nv2 duplica los principales (5). La patrulla va aparte.
		int q1 = extra >= 2 ? 2 : 1;
		int q2 = extra >= 2 ? 2 : 1;
		int qEO = extra >= 1 ? 1 : 0;
		int l1 = 0, l2 = 0, l3 = 0, l4 = 0;
		bool hayPatrulla = false;
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c)) continue;
			if (c is PatrolAI) { hayPatrulla = true; continue; }
			if (c.EnRuta) continue;
			if (c.EjeX) { if (c.Position.Z > 0f) l3++; else l4++; }
			else if (c.Position.X < 0f) l1++; else l2++;
		}
		if (!hayPatrulla)
		{
			int dir = rng.Randf() < 0.5f ? 1 : -1;
			var p = dir > 0 ? new Vector3(-35f, 0, 2f) : new Vector3(35f, 0, -2f);
			if (ZonaLibre(p)) SpawnearPatrulla(dir);
		}
		else if (l3 + l4 < qEO)
		{
			// Un solo transversal por vez, alternando de lado en cada respawn.
			eoLadoL3 = !eoLadoL3;
			var (tipo, color) = AutoRandomEO();
			if (eoLadoL3)
			{
				var p = new Vector3(-35f + rng.Randf() * 4f, 0, 2f);
				if (ZonaLibre(p)) SpawnearEO(tipo, color, 1, 2f, p.X, Semaforo3, 3);
				else eoLadoL3 = !eoLadoL3;
			}
			else
			{
				var p = new Vector3(35f - rng.Randf() * 4f, 0, -2f);
				if (ZonaLibre(p)) SpawnearEO(tipo, color, -1, -2f, p.X, Semaforo4 ?? Semaforo3, 4);
				else eoLadoL3 = !eoLadoL3;
			}
		}
		else if (l1 < q1)
		{
			var (tipo, color) = AutoRandomNS();
			var p = new Vector3(-2f, 0, -38f + rng.Randf() * 4f);
			if (ZonaLibre(p)) Spawnear(tipo, color, p, 1, -14f, Semaforo, 1);
		}
		else if (l2 < q2)
		{
			var (tipo, color) = AutoRandomNS();
			var p = new Vector3(2f, 0, 38f - rng.Randf() * 4f);
			if (ZonaLibre(p)) Spawnear(tipo, color, p, -1, 14f, Semaforo2 ?? Semaforo, 2);
		}
	}

	(string tipo, Color color) AutoRandomNS()
	{
		float r = rng.Randf();
		if (r < 0.6f) return ("sedan", ColSedan[(int)(rng.Randf() * ColSedan.Length)]);
		if (r < 0.85f) return ("taxi", ColTaxi[(int)(rng.Randf() * ColTaxi.Length)]);
		return ("bus", ColBus[(int)(rng.Randf() * ColBus.Length)]);
	}

	(string tipo, Color color) AutoRandomEO()
	{
		float r = rng.Randf();
		if (r < 0.7f) return ("sedan", ColSedan[(int)(rng.Randf() * ColSedan.Length)]);
		return ("taxi", ColTaxi[(int)(rng.Randf() * ColTaxi.Length)]);
	}

	/// <summary>¿Hay lugar para salir? (ningún auto a menos de 9m).</summary>
	bool ZonaLibre(Vector3 p)
	{
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c)) continue;
			Vector3 q = c.GlobalPosition;
			float dx = q.X - p.X, dz = q.Z - p.Z;
			if (dx * dx + dz * dz < 81f) return false;
		}
		return true;
	}

	/// <summary>Auto de la transversal (eje X).</summary>
	void SpawnearEO(string tipo, Color color, int direccion, float zCarril, float xSpawn, TrafficLight? sem, int carril)
	{
		var visual = ProcAuto.Build(tipo, color);
		var car = new CarAI
		{
			Semaforo = sem,
			Velocidad = Velocidad,
			Direccion = direccion,
			EjeX = true,
			LineaDetencion = direccion > 0 ? -14f : 14f,
			MitadLargo = 2f,
			TamanoCol = ProcAuto.Tamano(tipo)
		};
		car.Position = new Vector3(xSpawn, 0, zCarril);
		car.RotationDegrees = new Vector3(0, direccion > 0 ? 90 : -90, 0);
		car.SetRuta(CarAI.RutaPara(carril, ManiobraRandom()));
		car.AddChild(visual);
		car.AddChild(ProcAuto.Colision(tipo));
		AddChild(car);
		autos.Add(car);
	}

	/// <summary>Auto lavable más cercano al jugador dentro de rango.</summary>
	public CarAI? AutoCercanoDetenido(Vector3 desde, float rango)
	{
		CarAI? mejor = null;
		float mejorDist = rango;
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c) || !c.Servible()) continue;
			float d = desde.DistanceTo(c.GlobalPosition);
			if (d < mejorDist) { mejorDist = d; mejor = c; }
		}
		return mejor;
	}

	/// <summary>Detenido más cercano aunque ya haya pagado (para mensajes).</summary>
	public CarAI? AutoDetenidoCercano(Vector3 desde, float rango)
	{
		CarAI? mejor = null;
		float mejorDist = rango;
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c) || !c.EstaDetenido()) continue;
			float d = desde.DistanceTo(c.GlobalPosition);
			if (d < mejorDist) { mejorDist = d; mejor = c; }
		}
		return mejor;
	}

	/// <summary>
	/// Al dar verde UNA luz: el amable a medias de ESE carril espera unos
	/// segundos (gracia), el resto de ese carril se va y su progreso se pierde.
	/// Los otros carriles no se tocan (ciclos en fases distintas).
	/// IDEMPOTENTE: si un auto ya está en gracia no se vuelve a contar (el
	/// único procesador del verde es WindshieldJob; esto cubre re-entradas).
	/// </summary>
	/// <returns>(golpes perdidos, autos en gracia)</returns>
	public (int perdidos, int conGracia) AplicarGraciaVerde(TrafficLight luz, float graciaSeg)
	{
		double ahora = Time.GetTicksMsec() / 1000.0;
		int perdidos = 0, conGracia = 0;
		foreach (var c in autos)
		{
			if (c.Semaforo != luz) continue;
			if (c.Golpes > 0 && !c.Atendido && c.Humor == "amable")
			{
				// Ya estaba esperando: no contar de nuevo (evita triple aviso).
				if (c.GraciaHasta > ahora) continue;
				c.GraciaHasta = ahora + graciaSeg;
				conGracia++;
			}
			else
			{
				perdidos += c.Golpes;
				c.LimpiarServicio();
			}
		}
		return (perdidos, conGracia);
	}

	/// <summary>
	/// ¿Hay algún auto sobre el tramo de una senda? Los peatones miran la
	/// calle antes de cruzar: con el semáforo en rojo igual puede quedar uno
	/// despejando el cruce. margen = medio ancho del corredor en metros.
	/// </summary>
	public bool HayAutoEnCorredor(Vector3 origen, Vector3 destino, float margen)
	{
		Vector2 d = new(destino.X - origen.X, destino.Z - origen.Z);
		float largo = d.Length();
		if (largo < 0.01f) return false;
		d /= largo;
		foreach (var c in autos)
		{
			if (!IsInstanceValid(c)) continue;
			Vector2 v = new(c.GlobalPosition.X - origen.X, c.GlobalPosition.Z - origen.Z);
			float t = v.Dot(d);
			if (t < -1f || t > largo + 1f) continue;         // fuera del tramo
			float perpendicular = Mathf.Abs(v.X * d.Y - v.Y * d.X);
			if (perpendicular > margen) continue;              // en otro carril
			return true;
		}
		return false;
	}

	/// <summary>Reset completo del servicio (IJob.Reset manual).</summary>
	public int ResetServicio()
	{
		int total = 0;
		foreach (var c in autos) { if (!IsInstanceValid(c)) continue; total += c.Golpes; c.LimpiarServicio(); }
		return total;
	}
}
