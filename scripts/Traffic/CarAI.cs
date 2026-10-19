using Godot;

/// <summary>
/// Auto Paso 2: avanza en su Dirección, frena con su semáforo y hace cola.
/// Servicio limpiaparabrisas: suciedad/humor sorteados por rojo, progreso,
/// calidad y pago por auto (Golpes, PuntosCalidad, Atendido).
/// Visual procedural vía ProcAuto; el parabrisas se tiñe según la mugre.
/// </summary>
public partial class CarAI : Node3D
{
	public TrafficLight? Semaforo;
	public float Velocidad = 6f;
	/// <summary>+1 avanza, -1 retrocede (en su eje).</summary>
	public int Direccion = 1;
	/// <summary>Línea de detención en su eje de marcha.</summary>
	public float LineaDetencion = -14f;
	public float ReaparicionZ = 40f;
	public float InicioZ = -40f;
	/// <summary>true: marcha sobre el eje X (patrulla).</summary>
	public bool EjeX = false;
	/// <summary>true: al llegar al borde invierte el sentido (patrulla).</summary>
	public bool Vaiven = false;
	/// <summary>Borde absoluto del vaivén en su eje.</summary>
	public float Limite = 45f;
	/// <summary>Mitad del largo (trompa al centro): sedán/taxi 2, bus 3.5.</summary>
	public float MitadLargo = 2f;
	/// <summary>Tamaño de la caja de colisión (lo pone el spawner).</summary>
	public Vector3 TamanoCol = new(1.8f, 1.6f, 4f);
	/// <summary>Lo pone CarSpawner cuando hay otro auto pegado delante.</summary>
	public bool BloqueadoPorTrafico = false;
	/// <summary>Ruta con waypoints tras cruzar (recto o giro). Vacía = patrulla.</summary>
	public System.Collections.Generic.List<Vector3> Ruta = new();
	public int WpIdx = 0;
	public bool EnRuta = false;

	/// <summary>
	/// Ruta según carril de origen y maniobra (0 recto, 1 izquierda, 2 derecha).
	/// Carriles (mano derecha): 1 = x-2 rumbo +Z, 2 = x+2 rumbo -Z,
	/// 3 = z+2 rumbo +X, 4 = z-2 rumbo -X. Los giros terminan SIEMPRE en
	/// el carril correspondiente, nunca por el medio de la calle.
	/// </summary>
	public static System.Collections.Generic.List<Vector3> RutaPara(int carril, int maniobra)
	{
		if (carril == 1)
			return maniobra switch
			{
				1 => new() { new Vector3(-2, 0, -6), new Vector3(-2, 0, -2), new Vector3(-8, 0, -2), new Vector3(-45, 0, -2) },
				2 => new() { new Vector3(-2, 0, -6), new Vector3(-2, 0, 2), new Vector3(8, 0, 2), new Vector3(45, 0, 2) },
				_ => new() { new Vector3(-2, 0, 0), new Vector3(-2, 0, 45) },
			};
		if (carril == 2)
			return maniobra switch
			{
				1 => new() { new Vector3(2, 0, 6), new Vector3(2, 0, 2), new Vector3(8, 0, 2), new Vector3(45, 0, 2) },
				2 => new() { new Vector3(2, 0, 6), new Vector3(2, 0, -2), new Vector3(-8, 0, -2), new Vector3(-45, 0, -2) },
				_ => new() { new Vector3(2, 0, 0), new Vector3(2, 0, -45) },
			};
		if (carril == 3)
			return maniobra switch
			{
				1 => new() { new Vector3(-6, 0, 2), new Vector3(-2, 0, 2), new Vector3(-2, 0, 8), new Vector3(-2, 0, 45) },
				2 => new() { new Vector3(-6, 0, 2), new Vector3(2, 0, 2), new Vector3(2, 0, -8), new Vector3(2, 0, -45) },
				_ => new() { new Vector3(0, 0, 2), new Vector3(45, 0, 2) },
			};
		// carril 4
		return maniobra switch
		{
			1 => new() { new Vector3(6, 0, -2), new Vector3(2, 0, -2), new Vector3(2, 0, -8), new Vector3(2, 0, -45) },
			2 => new() { new Vector3(6, 0, -2), new Vector3(-2, 0, -2), new Vector3(-2, 0, 8), new Vector3(-2, 0, 45) },
			_ => new() { new Vector3(0, 0, -2), new Vector3(-45, 0, -2) },
		};
	}

	public void SetRuta(System.Collections.Generic.List<Vector3> r)
	{
		Ruta = r;
		WpIdx = 0;
		EnRuta = false;
	}

	// --- Servicio limpiaparabrisas (un servicio = un rojo de su carril) ---
	public int Golpes = 0;
	/// <summary>perfecto=2, bien=1, mal=0 por toque.</summary>
	public int PuntosCalidad = 0;
	/// <summary>0 limpia .. 1 muy sucia.</summary>
	public float Suciedad = 0f;
	public string Humor = "amable"; // amable | apurado | enojado
	public bool Asignado = false; // ya sorteó suciedad/humor en este rojo
	public bool Atendido = false; // ya pagó (o se negó) en este rojo
	public double InicioServicio = 0; // segundos (Time) del primer golpe
	/// <summary>Si dio verde a medias y es amable: espera hasta este instante (segundos Time).</summary>
	public double GraciaHasta = 0;

	MeshInstance3D? vidrio;
	StandardMaterial3D? matVidrio;
	/// <summary>Material por vidrio (parabrisas + ventanillas).</summary>
	readonly System.Collections.Generic.Dictionary<string, StandardMaterial3D> matsVidrio = new();
	/// <summary>Vidrio roto por el vandalismo (para no repintarlo al lavar).</summary>
	MeshInstance3D? vidrioRotoNodo;

	/// <summary>
	/// En _PhysicsProcess (no _Process): al mover el cuerpo por física,
	/// el AnimatableBody empuja al jugador en vez de atravesarlo.
	/// Soporta eje Z (autos) y eje X con vaivén (patrulla, subclase PatrolAI).
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		// En ruta (ya cruzó, va recto o doblando): sigue waypoints y se va.
		if (EnRuta)
		{
			SeguirRuta(d);
			return;
		}
		// En gracia el auto espera frenado aunque su luz ya esté en verde.
		bool debeFrenar = BloqueadoPorTrafico || EnGracia();
		float factorVel = 1f;
		// Distancia del CENTRO a la línea según eje y sentido de marcha.
		float pos = EjeX ? Position.X : Position.Z;
		float dist = Direccion > 0 ? LineaDetencion - pos : pos - LineaDetencion;
		if (!debeFrenar && Semaforo != null && !Semaforo.PuedenAvanzar())
		{
			// Distancia de la TROMPA a la línea: ahí debe quedar parado.
			float distF = dist - MitadLargo;
			if (dist > 0f && distF <= 0.4f) debeFrenar = true;
			else if (dist > 0f && distF < 10f) factorVel = Mathf.Clamp(distF / 8f, 0.12f, 1f);
			// Si el centro ya cruzó, sigue para despejar el cruce.
		}
		if (!debeFrenar)
		{
			float paso = Velocidad * factorVel * Direccion * d;
			Vector3 dir = EjeX ? new Vector3(Direccion, 0, 0) : new Vector3(0, 0, Direccion);
			Position += dir * Velocidad * factorVel * d;
			if (!Vaiven) MirarHacia(dir, d);
			// Cruzó del todo (centro bien pasada la línea): entra en ruta.
			if (Ruta.Count > 0 && dist < -(MitadLargo + 1.5f)) EnRuta = true;
		}

		if (!EjeX)
		{
			if (Direccion > 0 && Position.Z > ReaparicionZ)
				Position = new Vector3(Position.X, Position.Y, InicioZ);
			else if (Direccion < 0 && Position.Z < InicioZ)
				Position = new Vector3(Position.X, Position.Y, ReaparicionZ);
		}
		else if (!Vaiven)
		{
			if (Direccion > 0 && Position.X > ReaparicionZ)
				Position = new Vector3(InicioZ, Position.Y, Position.Z);
			else if (Direccion < 0 && Position.X < InicioZ)
				Position = new Vector3(ReaparicionZ, Position.Y, Position.Z);
		}
		else if (Direccion > 0 && Position.X > Limite)
		{
			Direccion = -1;
			AlCambiarSentido();
		}
		else if (Direccion < 0 && Position.X < -Limite)
		{
			Direccion = 1;
			AlCambiarSentido();
		}
	}

	/// <summary>Hook al invertir el vaivén (la patrulla rota y cambia de luz/línea).</summary>
	public virtual void AlCambiarSentido() { }

	/// <summary>Sigue los waypoints (recto o doblando) hasta salir del mapa.</summary>
	void SeguirRuta(float d)
	{
		while (WpIdx < Ruta.Count && DistXZ(GlobalPosition, Ruta[WpIdx]) < 1f) WpIdx++;
		if (WpIdx >= Ruta.Count || Mathf.Abs(GlobalPosition.X) > 55f || Mathf.Abs(GlobalPosition.Z) > 55f)
		{
			QueueFree(); // el spawner purga la referencia y respawnea otro aleatorio
			return;
		}
		Vector3 dir = Ruta[WpIdx] - GlobalPosition;
		dir.Y = 0;
		if (dir.Length() < 0.01f) { WpIdx++; return; }
		dir = dir.Normalized();
		Position += dir * Velocidad * d;
		MirarHacia(dir, d);
	}

	/// <summary>Gira el frente (+Z local) hacia la marcha, suave.</summary>
	protected void MirarHacia(Vector3 dir, float d)
	{
		float objetivo = Mathf.Atan2(dir.X, dir.Z);
		Rotation = new Vector3(0, (float)Mathf.LerpAngle(Rotation.Y, objetivo, Mathf.Min(1f, 8f * d)), 0);
	}

	static float DistXZ(Vector3 a, Vector3 b)
	{
		float dx = a.X - b.X, dz = a.Z - b.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	public bool EstaDetenido()
	{
		if (SiempreDetenido) return true; // test de vidrios: estacionados libres
		if (Semaforo == null) return false;
		// En verde solo cuenta si está en gracia (amable esperando a medias).
		if (!Semaforo.EsRojo() && !EnGracia()) return false;
		// Todo auto esperando antes de su línea (zona de 8m) o en fila.
		float pos = EjeX ? Position.X : Position.Z;
		float dist = Direccion > 0 ? LineaDetencion - pos : pos - LineaDetencion;
		if (dist < 0f) return false; // ya cruzó, está despejando
		return dist < 8f || BloqueadoPorTrafico || EnGracia();
	}

	/// <summary>¿Está en período de gracia (verde a medias, conductor amable esperando)?</summary>
	public bool EnGracia()
	{
		if (GraciaHasta <= 0) return false;
		return (Time.GetTicksMsec() / 1000.0) < GraciaHasta;
	}

	/// <summary>Se puede lavar: detenido, con servicio asignado y sin pagar aún.</summary>
	public bool Servible() => EstaDetenido() && Asignado && !Atendido;

	/// <summary>Reset completo al dar verde: el auto se va, se pierde lo no cobrado.</summary>
	public void LimpiarServicio()
	{
		Golpes = 0;
		PuntosCalidad = 0;
		Suciedad = 0f;
		Humor = "amable";
		Asignado = false;
		Atendido = false;
		InicioServicio = 0;
		GraciaHasta = 0;
		VidrioRoto = false;
		vidrioRotoNodo = null;
		CachearVidrios();
		var visual = GetChildOrNull<Node3D>(0);
		if (visual != null)
		{
			foreach (var nombre in matsVidriosNombres)
			{
				var v = visual.GetNodeOrNull<MeshInstance3D>(nombre);
				v?.GetNodeOrNull<Node3D>("Trincado")?.QueueFree();
			}
		}
		foreach (var m in matsVidrio.Values) m.Roughness = 0.15f;
		RefrescarVidrio(0f);
	}

	/// <summary>¿Le rompieron un vidrio en este rojo? (vandalismo con click).</summary>
	public bool VidrioRoto { get; private set; }

	/// <summary>Solo-dev (test de vidrios): detenido siempre, sin pedir rojo.</summary>
	public bool SiempreDetenido = false;

	/// <summary>Nombres de vidrios rompibles (el bus usa la banda lateral).</summary>
	static readonly string[] matsVidriosNombres =
		{ "Parabrisas", "VentanillaIzq", "VentanillaDer", "Ventanas" };

	/// <summary>
	/// Rompe el vidrio más cercano al rayo de puntería (lo que se mira,
	/// no lo más cercano al cuerpo): queda trincado (estrella en el punto
	/// del rayo) y el conductor se pone furioso (puede no pagar).
	/// Una vez por rojo.
	/// </summary>
	public bool RomperVidrio(Vector3 origenRayo, Vector3 dirRayo)
	{
		if (VidrioRoto) return false;
		CachearVidrios();
		if (matsVidrio.Count == 0) return false;
		dirRayo = dirRayo.Normalized();
		// Primer vidrio que toca el rayo desde tu lado (intersección con el
		// cuadrilátero de cada vidrio): el de entrada se rompe, nunca el de
		// atrás. Así pegar de un lado siempre rompe ese lado o el frente.
		MeshInstance3D? elegido = null;
		string nombreElegido = "";
		float mejorT = float.MaxValue;
		Vector3 mejorHit = Vector3.Zero;
		var visual = GetChildOrNull<Node3D>(0);
		foreach (var kv in matsVidrio)
		{
			var nodo = visual?.GetNodeOrNull<MeshInstance3D>(kv.Key);
			if (nodo == null || !IsInstanceValid(nodo)) continue;
			if (nodo.Mesh is not BoxMesh caja) continue;
			// Eje fino = normal de la cara (en mundo, con la rotación del auto).
			Vector3 tam = caja.Size;
			Vector3 normalL = tam.X <= tam.Y && tam.X <= tam.Z ? Vector3.Right : Vector3.Forward;
			Vector3 normal = (nodo.GlobalTransform.Basis * normalL).Normalized();
			// Ejes del plano (los dos más anchos), en mundo.
			Vector3 e1 = (nodo.GlobalTransform.Basis * (normalL == Vector3.Right ? Vector3.Forward : Vector3.Right)).Normalized();
			Vector3 e2 = (nodo.GlobalTransform.Basis * Vector3.Up).Normalized();
			float denom = dirRayo.Dot(normal);
			if (Mathf.Abs(denom) < 0.0001f) continue;
			float t = (nodo.GlobalPosition - origenRayo).Dot(normal) / denom;
			if (t < 0f || t > 30f) continue;
			Vector3 hit = origenRayo + dirRayo * t;
			Vector3 d = hit - nodo.GlobalPosition;
			float hu = (normalL == Vector3.Right ? tam.Z : tam.X) / 2f + 0.15f;
			float hv = tam.Y / 2f + 0.15f;
			if (Mathf.Abs(d.Dot(e1)) > hu || Mathf.Abs(d.Dot(e2)) > hv) continue;
			if (t < mejorT) { mejorT = t; mejorHit = hit; elegido = nodo; nombreElegido = kv.Key; }
		}
		if (elegido == null || !matsVidrio.TryGetValue(nombreElegido, out var mat)) return false;
		VidrioRoto = true;
		vidrioRotoNodo = elegido;
		mat.AlbedoColor = new Color(0.82f, 0.88f, 0.95f, 0.9f);
		mat.Roughness = 0.6f;
		// La estrella va donde el rayo roza el vidrio (en su cara).
		// (mejorHit ya está sobre la cara externa del vidrio elegido)
		Vector3 dirLocal = (elegido.GlobalTransform.Basis.Inverse() * dirRayo).Normalized();
		PonerTrincado(elegido, elegido.ToLocal(mejorHit), dirLocal);
		Humor = "enojado";
		return true;
	}

	/// <summary>
	/// Estrella de grietas sobre la cara externa del vidrio (la que mira al
	/// rayo), centrada en el impacto y 1.5cm por fuera para que no quede
	/// tapada por el vidrio. La cara se detecta por el eje más fino del
	/// BoxMesh (parabrisas: +Z, ventanillas: ±X) y la estrella se rota a ella.
	/// </summary>
	static void PonerTrincado(MeshInstance3D vidrioNodo, Vector3 impactoRel, Vector3 dirLocal)
	{
		if (vidrioNodo.GetNodeOrNull<Node3D>("Trincado") != null) return;
		Vector3 tam = (vidrioNodo.Mesh as BoxMesh)?.Size ?? new Vector3(1.5f, 0.55f, 0.08f);
		bool deCostado = tam.X <= tam.Y && tam.X <= tam.Z;
		// El rayo entra por la cara opuesta a su dirección de avance.
		float avance = deCostado ? dirLocal.X : dirLocal.Z;
		float signo = avance < 0f ? 1f : avance > 0f ? -1f : 0f;
		if (deCostado && signo == 0f) signo = Mathf.Sign(impactoRel.X);
		if (signo == 0f) signo = 1f;
		var estrella = new Node3D { Name = "Trincado" };
		vidrioNodo.AddChild(estrella);
		float u, v, cara;
		if (!deCostado)
		{
			u = Mathf.Clamp(impactoRel.X, -tam.X / 2f + 0.1f, tam.X / 2f - 0.1f);
			v = Mathf.Clamp(impactoRel.Y, -tam.Y / 2f + 0.08f, tam.Y / 2f - 0.08f);
			cara = tam.Z / 2f + 0.015f;
			estrella.Position = new Vector3(0, 0, cara);
		}
		else
		{
			u = Mathf.Clamp(impactoRel.Z, -tam.Z / 2f + 0.1f, tam.Z / 2f - 0.1f);
			v = Mathf.Clamp(impactoRel.Y, -tam.Y / 2f + 0.08f, tam.Y / 2f - 0.08f);
			cara = tam.X / 2f + 0.015f;
			estrella.RotationDegrees = new Vector3(0, 90f * signo, 0);
			estrella.Position = new Vector3(signo * cara, 0, 0);
		}
		var matGrieta = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.07f, 0.08f, 0.1f),
			Roughness = 0.9f
		};
		var cc = new Vector3(u, v, 0.005f);
		var punto = new MeshInstance3D
		{
			Name = "Impacto",
			Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f },
			Position = cc
		};
		punto.SetSurfaceOverrideMaterial(0, matGrieta);
		estrella.AddChild(punto);
		for (int i = 0; i < 7; i++)
		{
			float ang = i * Mathf.Tau / 7f + 0.3f;
			float largo = 0.2f + (i % 3) * 0.09f;
			var grieta = new MeshInstance3D
			{
				Name = $"Grieta{i}",
				Mesh = new BoxMesh { Size = new Vector3(0.035f, largo, 0.012f) },
				Position = cc + new Vector3(Mathf.Cos(ang) * largo / 2f, Mathf.Sin(ang) * largo / 2f, 0),
				Rotation = new Vector3(0, 0, ang - Mathf.Pi / 2f)
			};
			grieta.SetSurfaceOverrideMaterial(0, matGrieta);
			estrella.AddChild(grieta);
		}
	}

	void CachearVidrio() => CachearVidrios();

	void CachearVidrios()
	{
		if (matsVidrio.Count > 0) return;
		var visual = GetChildOrNull<Node3D>(0);
		if (visual == null) return;
		foreach (var nombre in matsVidriosNombres)
		{
			var v = visual.GetNodeOrNull<MeshInstance3D>(nombre);
			if (v == null || matsVidrio.ContainsKey(nombre)) continue;
			var mat = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				Roughness = 0.15f
			};
			v.SetSurfaceOverrideMaterial(0, mat);
			matsVidrio[nombre] = mat;
		}
		// Compat: parabrisas como vidrio principal.
		vidrio = visual.GetNodeOrNull<MeshInstance3D>("Parabrisas");
		matVidrio = matsVidrio.TryGetValue("Parabrisas", out var m) ? m : null;
	}

	/// <summary>Tiñe los vidrios según la mugre restante (marrón opaco → celeste).</summary>
	public void RefrescarVidrio(float progresoLimpieza)
	{
		CachearVidrios();
		if (matsVidrio.Count == 0) return;
		float sucio = Mathf.Clamp(Suciedad * (1f - progresoLimpieza), 0f, 1f);
		var limpio = new Color(0.6f, 0.85f, 1f, 0.45f);
		var mugre = new Color(0.4f, 0.32f, 0.16f, 0.9f);
		foreach (var m in matsVidrio.Values)
			m.AlbedoColor = mugre.Lerp(limpio, 1f - sucio);
		// El roto queda blanco aunque lo laven.
		if (vidrioRotoNodo != null && IsInstanceValid(vidrioRotoNodo) &&
			matsVidrio.TryGetValue(vidrioRotoNodo.Name, out var roto))
			roto.AlbedoColor = new Color(0.82f, 0.88f, 0.95f, 0.9f);
	}
}
