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
		RefrescarVidrio(0f);
	}

	void CachearVidrio()
	{
		if (vidrio != null) return;
		var visual = GetChildOrNull<Node3D>(0);
		vidrio = visual?.GetNodeOrNull<MeshInstance3D>("Parabrisas");
		if (vidrio == null) return;
		matVidrio = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.15f
		};
		vidrio.SetSurfaceOverrideMaterial(0, matVidrio);
	}

	/// <summary>Tiñe el parabrisas según la mugre restante (marrón opaco → celeste).</summary>
	public void RefrescarVidrio(float progresoLimpieza)
	{
		CachearVidrio();
		if (matVidrio == null) return;
		float sucio = Mathf.Clamp(Suciedad * (1f - progresoLimpieza), 0f, 1f);
		var limpio = new Color(0.6f, 0.85f, 1f, 0.45f);
		var mugre = new Color(0.4f, 0.32f, 0.16f, 0.9f);
		matVidrio.AlbedoColor = mugre.Lerp(limpio, 1f - sucio);
	}
}
