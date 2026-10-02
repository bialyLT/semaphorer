using Godot;

/// <summary>
/// Ladrón de la mochila: es un Peaton con un "propósito" extra.
/// Pasea como cualquier transeúnte, pero si pasa cerca del escondite
/// (mochila azul, Mejoras.Escondite) y hay plata guardada, se queda
/// acechando junto a ella:
///  - Mientras NO lo mirás, la barra de robo se carga (TiempoRoboSeg).
///  - Mientras lo mirás (cámara apuntándole, desde donde sea, con línea
///    de vista), la barra baja (TiempoPerdonSeg).
///  - Barra llena = te roba todo lo guardado y huye corriendo.
///  - Barra vacía = desiste y sigue su ruta como un peatón normal.
/// La barra vive en el HUD (SetLadron) + marca flotante sobre la cabeza.
/// </summary>
public partial class Ladron : Peaton
{
	[Signal] public delegate void AcechoIniciadoEventHandler();
	[Signal] public delegate void RoboProgresoEventHandler(float progreso);
	[Signal] public delegate void RoboConsumadoEventHandler(int monto);
	[Signal] public delegate void DisuadidoEventHandler();
	[Signal] public delegate void RecuperadoEventHandler(int monto);

	public Economy? Economia;
	public HUD? Hud;

	/// <summary>Distancia a la mochila que dispara el acecho.</summary>
	[Export] public float RadioAcecho = 4.5f;
	/// <summary>Segundos sin mirar para que llene la barra y robe.</summary>
	[Export] public float TiempoRoboSeg = 10f;
	/// <summary>Segundos mirándolo para vaciar la barra desde lleno.</summary>
	[Export] public float TiempoPerdonSeg = 3.5f;
	/// <summary>Ventana tras el robo para alcanzarlo y recuperar (balance).</summary>
	[Export] public float VentanaRecuperacionSeg = 8f;
	/// <summary>Distancia para tocar al ladrón que huye y recuperar.</summary>
	[Export] public float RadioRecuperacion = 2f;
	/// <summary>Si te le acercás a esta distancia, te ve y afloja sin
	/// necesidad de apuntarle con la cámara (balance, radio_espanto_m).</summary>
	[Export] public float RadioEspanto = 4f;

	bool acechando;
	bool resuelto;
	bool huyendo;
	bool recuperable;
	float tiempoDesdeRobo;
	int montoRobado;
	float progreso;
	Vector3 huidaDir = Vector3.Forward;
	float velocidadHuida = 5.5f;
	Label3D? marca;

	/// <summary>0..1 de robo en curso; negativo = nadie acechando (oculta HUD).</summary>
	public float NivelRobo() => !acechando || resuelto ? -1f : Mathf.Clamp(progreso, 0f, 1f);
	public bool Acechando => acechando && !resuelto;
	public bool Mirado { get; private set; }

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("ladron");
		CargarBalanceRecuperacion();
		Economia ??= GetNodeOrNull<Economy>("../../Economy");
		Hud ??= GetNodeOrNull<HUD>("../../HUD");
		marca = new Label3D
		{
			Text = "!",
			FontSize = 96,
			Modulate = new Color(1, 0.45f, 0.1f),
			OutlineSize = 16,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(0, 2.2f, 0),
			Visible = false
		};
		AddChild(marca);
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		if (resuelto && huyendo)
		{
			Huir(d);
			return;
		}
		if (resuelto)
		{
			base._PhysicsProcess(delta);
			return;
		}
		if (!acechando)
		{
			// ¿Pasó cerca de la mochila con algo que valga la pena?
			if (Economia != null && Economia.Guardado > 0 &&
				PlanoPropio().DistanceTo(new Vector3(Mejoras.Escondite.X, 0, Mejoras.Escondite.Z)) <= RadioAcecho)
			{
				EmpezarAcecho();
			}
			else
			{
				base._PhysicsProcess(delta);
				return;
			}
		}
		Acechar(d);
	}

	void EmpezarAcecho()
	{
		acechando = true;
		progreso = 0.15f; // arranca ya visible para dar chance de reacción
		if (marca != null) marca.Visible = true;
		// Se planta mirando a la mochila.
		MirarInstantanea(Mejoras.Escondite - GlobalPosition);
		Hud?.SetError("¡Hay un ladrón tanteando tu mochila! ¡Mirá hacia la mochila azul o acercate a menos de 4m para espantarlo!");
		EmitSignal(SignalName.AcechoIniciado);
	}

	void Acechar(float d)
	{
		// Sin nada guardado ya no hay motivo: se va sin drama.
		if (Economia == null || Economia.Guardado <= 0)
		{
			Desistir(true);
			return;
		}
		Mirado = JugadorMira() || JugadorCerca();
		if (Mirado)
			progreso -= d / Mathf.Max(0.5f, TiempoPerdonSeg);
		else
			progreso += d / Mathf.Max(0.5f, TiempoRoboSeg);
		progreso = Mathf.Clamp(progreso, 0f, 1f);
		Hud?.SetLadron(progreso, Mirado);
		EmitSignal(SignalName.RoboProgreso, progreso);
		// Parpadeo de la marca: rápido cuando está por robar.
		if (marca != null)
		{
			double t = Time.GetTicksMsec() / 1000.0;
			marca.Visible = Mirado ? ((int)(t * 6) % 2 == 0) : true;
		}
		if (progreso >= 1f) Robar();
		else if (progreso <= 0f) Desistir(false);
	}

	void Desistir(bool sinBotin)
	{
		resuelto = true;
		acechando = false;
		if (marca != null) marca.Visible = false;
		Hud?.SetLadron(-1f, false);
		EmitSignal(SignalName.Disuadido);
		if (!sinBotin)
			Hud?.SetOk("Lo viste y se fue. La mochila está a salvo... por ahora.");
		// Espantado también huye corriendo y desaparece (no retoma la ruta).
		EmpezarHuida();
	}

	void Robar()
	{
		resuelto = true;
		acechando = false;
		int monto = Economia?.RobarGuardado() ?? 0;
		int queda = Economia?.Guardado ?? 0;
		montoRobado = monto;
		recuperable = monto > 0;
		tiempoDesdeRobo = 0f;
		if (marca != null) marca.Visible = false;
		Hud?.SetLadron(-1f, false);
		EmitSignal(SignalName.RoboConsumado, monto);
		Hud?.SetError(monto > 0
			? $"¡Te robó la mochila! Perdiste ${monto} guardado (te quedan ${queda}). ¡Alcanzalo antes que escape para recuperar!"
			: "¡Te robó la mochila!");
		// Huye: corre hacia afuera del mapa y desaparece.
		EmpezarHuida();
	}

	void CargarBalanceRecuperacion()
	{
		try
		{
			var path = "res://data/balance.json";
			if (!FileAccess.FileExists(path)) return;
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			using var doc = System.Text.Json.JsonDocument.Parse(f.GetAsText());
			if (!doc.RootElement.TryGetProperty("ladrones", out var l)) return;
			if (l.TryGetProperty("ventana_recuperacion_seg", out var v))
				VentanaRecuperacionSeg = (float)v.GetDouble();
			if (l.TryGetProperty("radio_recuperacion_m", out v))
				RadioRecuperacion = (float)v.GetDouble();
			if (l.TryGetProperty("radio_espanto_m", out v))
				RadioEspanto = (float)v.GetDouble();
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[Ladron] balance sin recuperacion: {e.Message}");
		}
	}

	void Recuperar()
	{
		recuperable = false;
		Economia?.DevolverRobo(montoRobado);
		EmitSignal(SignalName.Recuperado, montoRobado);
		Hud?.SetOk($"¡Lo alcanzaste! Recuperaste ${montoRobado} de la mochila.");
		var rig = GetViewport().GetCamera3D()?.GetParent() as CameraRig;
		rig?.Punch(0.35f);
		montoRobado = 0;
	}

	void EmpezarHuida()
	{
		huyendo = true;
		Vector3 fuera = GlobalPosition - new Vector3(Mejoras.Escondite.X, GlobalPosition.Y, Mejoras.Escondite.Z);
		if (fuera.LengthSquared() < 0.01f) fuera = GlobalPosition.Normalized();
		fuera.Y = 0;
		huidaDir = fuera.Normalized();
		velocidadHuida = Mathf.Max(VelocidadApurado * 1.6f, 5f);
	}

	void Huir(float d)
	{
		tiempoDesdeRobo += d;
		// Ventana de recuperación: lo tocás antes que escape y vuelve lo robado.
		if (recuperable && tiempoDesdeRobo <= VentanaRecuperacionSeg)
		{
			var jugador = GetNodeOrNull<Player>("../../Player");
			if (jugador != null && IsInstanceValid(jugador) &&
				GlobalPosition.DistanceTo(jugador.GlobalPosition) <= RadioRecuperacion)
			{
				Recuperar();
			}
		}
		else recuperable = false;
		GlobalPosition += huidaDir * velocidadHuida * d;
		GlobalPosition = new Vector3(GlobalPosition.X, Mathf.Lerp(GlobalPosition.Y, 0.12f, Mathf.Min(1f, 8f * d)), GlobalPosition.Z);
		float objetivo = Mathf.Atan2(huidaDir.X, huidaDir.Z);
		Rotation = new Vector3(0, (float)Mathf.LerpAngle(Rotation.Y, objetivo, Mathf.Min(1f, 10f * d)), 0);
		if (Mathf.Abs(GlobalPosition.X) > 28f || Mathf.Abs(GlobalPosition.Z) > 28f)
		{
			Hud?.SetLadron(-1f, false);
			QueueFree();
		}
	}

	/// <summary>
	/// ¿El jugador está encima? A menos de RadioEspanto el ladrón se siente
	/// encarado y la barra baja sola: no hace falta apuntarle justo.
	/// </summary>
	bool JugadorCerca()
	{
		var jugador = GetNodeOrNull<Player>("../../Player");
		if (jugador == null || !IsInstanceValid(jugador)) return false;
		Vector3 a = new(GlobalPosition.X, 0f, GlobalPosition.Z);
		Vector3 b = new(jugador.GlobalPosition.X, 0f, jugador.GlobalPosition.Z);
		return a.DistanceTo(b) <= RadioEspanto;
	}

	/// <summary>
	/// ¿La cámara activa lo está mirando? Sin límite de distancia ("desde
	/// donde sea"), pero con cono de visión y línea de vista (los edificios
	/// tapan). El rayo arranca un poco adelante para no chocar con el jugador.
	/// </summary>
	bool JugadorMira()
	{
		var cam = GetViewport().GetCamera3D();
		if (cam == null) return false;
		Vector3 objetivo = GlobalPosition + Vector3.Up * 1.4f;
		Vector3 aCam = objetivo - cam.GlobalPosition;
		float dist = aCam.Length();
		if (dist < 0.6f) return true;
		if (dist > 90f) return false;
		if (!cam.IsPositionInFrustum(objetivo)) return false;
		Vector3 fwd = -cam.GlobalTransform.Basis.Z;
		Vector3 dir = aCam / dist;
		// Cono ~8° (dot 0.990): generoso para que valga "mirar hacia él".
		if (fwd.Dot(dir) < 0.990f) return false;
		Vector3 desde = cam.GlobalPosition + fwd * 0.5f;
		var espacio = GetWorld3D().DirectSpaceState;
		var query = new PhysicsRayQueryParameters3D
		{
			From = desde,
			To = objetivo,
			CollisionMask = 1
		};
		var hit = espacio.IntersectRay(query);
		if (hit.Count > 0)
		{
			Vector3 p = (Vector3)hit["position"];
			if (p.DistanceTo(cam.GlobalPosition) < dist - 0.8f) return false;
		}
		return true;
	}

	Vector3 PlanoPropio() => new(GlobalPosition.X, 0, GlobalPosition.Z);

	public override void _ExitTree()
	{
		if (acechando && !resuelto)
			Hud?.SetLadron(-1f, false);
		base._ExitTree();
	}
}
