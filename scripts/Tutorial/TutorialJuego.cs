using Godot;

/// <summary>
/// Controlador del tutorial jugable: cada paso se valida con una acción
/// real en el mundo (no con botones Siguiente).
/// 0 calle con WASD · 1 auto frenado en rojo · 2 cobrar con E ·
/// 3 vereda con Espacio · 4 mochila con G · 5 tienda con F ·
/// 6 aviso del tutorial del menú. En el paso 4 muestra una flecha
/// flotante sobre la mochila azul (Mejoras.Escondite).
/// </summary>
public partial class TutorialJuego : Node3D
{
	[Export] public Player? Jugador;
	[Export] public Economy? Economia;
	[Export] public Mejoras? Mejoras;
	[Export] public CarSpawner? Spawner;
	[Export] public TutorialUI? Tutorial;

	int paso;
	bool activo;
	double acum;
	double tiempoFinal;
	int monedasBase;
	int guardadoBase;
	Label3D? flecha;
	double tFlecha;

	public override void _Ready()
	{
		Jugador ??= GetNodeOrNull<Player>("../Player");
		Economia ??= GetNodeOrNull<Economy>("../Economy");
		Mejoras ??= GetNodeOrNull<Mejoras>("../Mejoras");
		Spawner ??= GetNodeOrNull<CarSpawner>("../CarSpawner");
		if (Tutorial == null)
		{
			foreach (var h in GetParent()?.GetChildren() ?? new Godot.Collections.Array<Node>())
				if (h is TutorialUI t && t.ModoInteractivo) { Tutorial = t; break; }
		}
		if (Tutorial != null && !Tutorial.ModoInteractivo)
			Tutorial.MostrarInteractivo(paso);
		monedasBase = Economia?.Coins ?? 0;
		guardadoBase = Economia?.Guardado ?? 0;
		CrearFlecha();
		activo = true;
		AlEntrarPaso();
	}

	public override void _ExitTree()
	{
		LimpiarFlecha();
	}

	public override void _Process(double delta)
	{
		// Flecha flotante (siempre animada mientras se vea).
		if (flecha != null && flecha.Visible)
		{
			tFlecha += delta;
			var base_ = Mejoras.Escondite + new Vector3(0, 2.4f, 0);
			flecha.GlobalPosition = base_ + new Vector3(0, Mathf.Sin((float)tFlecha * 3f) * 0.25f, 0);
		}
		if (!activo) return;
		if (!TutorialManager.TutorialActivo) { activo = false; LimpiarFlecha(); return; }
		acum += delta;
		if (acum < 0.15) return;
		acum = 0;
		// Leyendo el tutorial no te decomisan: la atención policial se
		// mantiene a cero hasta terminar (como pausaba el modal viejo).
		ProtegerDePolicia();
		ChequearPaso((float)delta);
	}

	void ChequearPaso(float d)
	{
		switch (paso)
		{
			case 0: // Bajá a la calle con WASD
				if (Jugador != null && Jugador.EstaEnCalle()) Avanzar();
				break;
			case 1: // Acercate a un auto frenado en Rojo
				if (CercaDeAutoServible()) Avanzar();
				break;
			case 2: // Apretá E hasta cobrar
				if (Economia != null && Economia.Coins > monedasBase) Avanzar();
				break;
			case 3: // Espacio para saltar y subir a la vereda
				if (Jugador != null && !Jugador.EstaEnCalle()) Avanzar();
				break;
			case 4: // Mochila azul + G
				if (Economia != null && Economia.Guardado > guardadoBase) Avanzar();
				break;
			case 5: // F para ver mejoras
				if (Mejoras != null && Mejoras.Abierta) Avanzar();
				break;
			case 6: // Aviso final: se cierra solo a los 12s (o con tecla/botón/Esc).
				tiempoFinal += 0.15;
				if (tiempoFinal >= 12.0) Completar();
				break;
		}
	}

	bool CercaDeAutoServible()
	{
		if (Jugador == null) return false;
		Vector3 p = Jugador.GlobalPosition;
		if (Jugador.Trabajo != null && Jugador.Trabajo.PuedeTrabajar()) return true;
		if (Jugador.Malabares != null && Jugador.Malabares.PuedeTrabajar()) return true;
		if (Jugador.Venta != null && Jugador.Venta.PuedeTrabajar()) return true;
		if (Spawner != null && Spawner.AutoCercanoDetenido(p, 5.0f) != null) return true;
		// Patrulla frenada: no está en autos[], se busca por grupo.
		foreach (var n in GetTree().GetNodesInGroup("patrulla"))
			if (n is CarAI c && IsInstanceValid(c) && c.Servible() &&
				p.DistanceTo(c.GlobalPosition) < 5.0f) return true;
		return false;
	}

	void Avanzar()
	{
		paso++;
		TutorialManager.PasoActual = paso;
		if (paso >= TutorialManager.TotalInteractivo) { Completar(); return; }
		Tutorial?.AvanzarInteractivo(paso);
		AlEntrarPaso();
	}

	/// <summary>La patrulla vigila igual pero no acumula causa durante el
	/// tutorial: evita un decomiso a mitad del paso 2 (cobrar lleva tiempo).</summary>
	void ProtegerDePolicia()
	{
		foreach (var n in GetTree().GetNodesInGroup("patrulla"))
			if (n is PatrolAI p && IsInstanceValid(p)) p.FijarTiempoVisto(0f);
	}

	void AlEntrarPaso()
	{
		if (paso == 2) monedasBase = Economia?.Coins ?? 0;
		if (paso == 4) guardadoBase = Economia?.Guardado ?? 0;
		MostrarFlecha(paso == 4);
		if (paso == 6) tiempoFinal = 0;
	}

	void Completar()
	{
		activo = false;
		LimpiarFlecha();
		if (Tutorial != null) Tutorial.CompletarInteractivo();
		else TutorialManager.CompletarTutorial();
	}

	void CrearFlecha()
	{
		LimpiarFlecha();
		flecha = new Label3D
		{
			Text = "▼ MOCHILA AZUL",
			FontSize = 96,
			Modulate = new Color(1, 0.8f, 0.2f),
			OutlineSize = 16,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = Mejoras.Escondite + new Vector3(0, 2.4f, 0),
			Visible = false
		};
		AddChild(flecha);
	}

	void MostrarFlecha(bool ver)
	{
		if (flecha != null && IsInstanceValid(flecha)) flecha.Visible = ver;
	}

	void LimpiarFlecha()
	{
		if (flecha != null)
		{
			if (IsInstanceValid(flecha)) { flecha.QueueFree(); }
			flecha = null;
		}
	}
}
