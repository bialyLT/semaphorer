using Godot;
using System;

/// <summary>
/// Intro cinematica basica: 3 planos con camara propia + letterbox +
/// subtitulos con typewriter + 2 actors (protagonista tirado y tipo
/// que llega caminando). Todo por codigo (sin video ni assets:
/// lo mas liviano y compatible: GL Compatibility, Windows/Linux).
/// El Player real se oculta (en 1ª persona su cuerpo esta invisible
/// y su viewmodel flotaria ante la cine-cam) y se usan dummies sin
/// herramientas. Al terminar se restaura todo y GameManager muestra
/// la ruleta como plano final.
/// </summary>
public partial class IntroCinematica : CanvasLayer
{
	public event Action? Terminada;

	readonly string[] guion =
	{
		"Te lo patinaste todo en las apuestas.",
		"Te quedaste en la calle. Te quedan $50.",
		"Un tipo se acerca... «Una última tirada de la suerte.»",
	};
	const float PlanoSeg = 6f;
	const float LetrasPorSeg = 32f;

	Camera3D? cineCam;
	Camera3D? camPrevia;
	ColorRect? fade;
	ColorRect? barraSup;
	ColorRect? barraInf;
	Label? lblSub;
	Label? lblSaltar;

	// Actors y escena real (para ocultar/restaurar).
	Node3D? jugadorReal;
	CanvasLayer? hudReal;
	Node3D? protagonista;
	Node3D? tipo;
	Node3D? pataIzqT, pataDerT, brazoIzqT, brazoDerT;

	Vector3 posSuelo = new(5, ProcPeaton.YVereda, -8);
	Vector3 posTipoInicio;
	Vector3 posTipoFin;
	Vector3[] desde = Array.Empty<Vector3>();
	Vector3[] hasta = Array.Empty<Vector3>();

	float tiempo;
	bool terminada;
	bool pausaSuelta;
	readonly SaltoHold salto = new();

	public override void _Ready()
	{
		Layer = 95;
		ProcessMode = ProcessModeEnum.Always;
		ConstruirUI();
		Visible = false;
	}

	void ConstruirUI()
	{
		barraSup = new ColorRect { Color = Colors.Black };
		barraSup.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		barraSup.OffsetBottom = 80;
		AddChild(barraSup);

		barraInf = new ColorRect { Color = Colors.Black };
		barraInf.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		barraInf.OffsetTop = -80;
		AddChild(barraInf);

		lblSub = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		lblSub.AddThemeFontSizeOverride("font_size", 22);
		lblSub.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
		lblSub.AddThemeColorOverride("font_shadow_color", Colors.Black);
		lblSub.AddThemeConstantOverride("shadow_offset_x", 2);
		lblSub.AddThemeConstantOverride("shadow_offset_y", 2);
		lblSub.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		lblSub.OffsetLeft = 120; lblSub.OffsetRight = -120;
		lblSub.OffsetTop = -160; lblSub.OffsetBottom = -90;
		AddChild(lblSub);

		lblSaltar = new Label { Text = "Mantené ESPACIO o ESC para saltar" };
		lblSaltar.AddThemeFontSizeOverride("font_size", 14);
		lblSaltar.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		lblSaltar.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		lblSaltar.HorizontalAlignment = HorizontalAlignment.Center;
		lblSaltar.OffsetTop = -78; lblSaltar.OffsetBottom = -54;
		AddChild(lblSaltar);

		fade = new ColorRect { Color = new Color(0, 0, 0, 1f) };
		fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fade);
	}

	public void Mostrar()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		UiPila.PedirPausa(GetTree(), "intro");
		Node? raiz = GetParent() ?? GetTree()?.CurrentScene;
		// Ocultar al Player real: en 1ª persona el cuerpo esta invisible
		// (Player.AplicarVisibilidadCuerpo) y su viewmodel en la Camera3D
		// vieja quedaria flotando ante la cine-cam ("limpiavidrios puesto").
		jugadorReal = raiz?.GetNodeOrNull<Node3D>("Player");
		if (jugadorReal == null) jugadorReal = GetTree()?.CurrentScene?.GetNodeOrNull<Node3D>("Player");
		if (jugadorReal != null) jugadorReal.Visible = false;
		// Set FIJO verificado (CineSet): encuadrar donde esté el jugador
		// es lotería (árboles/pórticos tapan según la posición).
		posSuelo = CineSet.Foco;
		// Ocultar HUD para que no tape el plano ($0, objetivos, etc.).
		hudReal = raiz?.GetNodeOrNull<CanvasLayer>("HUD");
		if (hudReal != null && hudReal.Visible) hudReal.Visible = false;
		else hudReal = null; // solo restaurar lo que ocultamos nosotros
		SpawnearActors(raiz);
		camPrevia = GetViewport()?.GetCamera3D();
		cineCam = new Camera3D { Name = "CineCam", Fov = 60f };
		(raiz ?? this).AddChild(cineCam);
		cineCam.MakeCurrent();
		Vector3 foco = posSuelo + new Vector3(0, 0.6f, 0);
		desde = new[]
		{
			foco + new Vector3(10, 8.5f, 10),
			foco + new Vector3(4.5f, 2.4f, 5.5f),
			posTipoFin + new Vector3(2.0f, 1.7f, 2.6f),
		};
		hasta = new[]
		{
			foco + new Vector3(7, 5.5f, 7),
			foco + new Vector3(3.0f, 1.9f, 4.0f),
			posTipoFin + new Vector3(1.3f, 1.45f, 1.8f),
		};
		tiempo = 0f;
	}

	/// <summary>Dummies sin herramientas: prota tirado + tipo que camina.</summary>
	void SpawnearActors(Node? raiz)
	{
		if (raiz == null) return;
		// Protagonista con la misma ropa del laburante (chaleco naranja,
		// pantalon azul, gorra azul), tirado de espaldas en la vereda.
		protagonista = ProcPeaton.Build(
			new Color(1, 0.45f, 0.08f),
			new Color(0.2f, 0.25f, 0.4f),
			new Color(0.85f, 0.6f, 0.45f),
			true, new Color(0.1f, 0.2f, 0.6f));
		protagonista.Name = "CineProta";
		raiz.AddChild(protagonista);
		protagonista.GlobalPosition = posSuelo + new Vector3(0, 0.24f, 0);
		protagonista.RotationDegrees = new Vector3(90, 0, 12);
		// El tipo: abrigo oscuro + gorra oscura, misterioso.
		tipo = ProcPeaton.Build(
			new Color(0.16f, 0.16f, 0.2f),
			new Color(0.1f, 0.1f, 0.13f),
			new Color(0.8f, 0.58f, 0.42f),
			true, new Color(0.05f, 0.05f, 0.07f));
		tipo.Name = "CineTipo";
		// Recorrido dentro del parche despejado (nunca a la calle).
		posTipoFin = posSuelo + new Vector3(-1.6f, 0, 1.2f);
		posTipoInicio = posSuelo + new Vector3(-2.5f, 0, 3.0f);
		raiz.AddChild(tipo);
		tipo.GlobalPosition = new Vector3(posTipoInicio.X, ProcPeaton.YVereda, posTipoInicio.Z);
		MirarA(tipo, posSuelo);
		pataIzqT = tipo.GetNodeOrNull<Node3D>("Cuerpo/PataIzq");
		pataDerT = tipo.GetNodeOrNull<Node3D>("Cuerpo/PataDer");
		brazoIzqT = tipo.GetNodeOrNull<Node3D>("Cuerpo/BrazoIzq");
		brazoDerT = tipo.GetNodeOrNull<Node3D>("Cuerpo/BrazoDer");
	}

	static void MirarA(Node3D n, Vector3 objetivo)
	{
		var t = new Vector3(objetivo.X, n.GlobalPosition.Y, objetivo.Z);
		if ((t - n.GlobalPosition).Length() > 0.05f)
			n.LookAt(t, Vector3.Up);
	}

	public override void _Process(double delta)
	{
		if (!Visible || terminada) return;
		float d = (float)delta;
		tiempo += d;
		int plano = Mathf.Clamp((int)(tiempo / PlanoSeg), 0, guion.Length - 1);
		float tPlano = Mathf.Clamp((tiempo - plano * PlanoSeg) / PlanoSeg, 0f, 1f);
		float s = tPlano * tPlano * (3f - 2f * tPlano);
		AnimarTipo();
		// La mira acompaña la accion: prota solo -> punto medio -> dos planos.
		Vector3 mira;
		if (tipo != null && IsInstanceValid(tipo))
		{
			Vector3 pp = protagonista != null ? protagonista.GlobalPosition : posSuelo;
			Vector3 pt = tipo.GlobalPosition;
			mira = plano switch
			{
				0 => pp,
				1 => pp.Lerp(pt, 0.35f) + new Vector3(0, 0.5f, 0),
				_ => pp.Lerp(pt, 0.5f) + new Vector3(0, 0.55f, 0),
			};
		}
		else mira = posSuelo + new Vector3(0, 0.6f, 0);
		if (cineCam != null && IsInstanceValid(cineCam) && desde.Length == 3)
		{
			cineCam.GlobalPosition = desde[plano].Lerp(hasta[plano], s);
			if ((mira - cineCam.GlobalPosition).Length() > 0.05f)
				cineCam.LookAt(mira, Vector3.Up);
		}
		if (lblSub != null)
		{
			int n = Mathf.Clamp((int)(tPlano * PlanoSeg * LetrasPorSeg), 0, guion[plano].Length);
			lblSub.Text = guion[plano].Substring(0, n);
		}
		if (fade != null)
		{
			var c = fade.Color;
			c.A = Mathf.Clamp(1f - tiempo / 1.5f, 0f, 1f);
			fade.Color = c;
		}
		if (tiempo >= guion.Length * PlanoSeg) Terminar();
		// Salto con mantener (un toque sin querer no salta nada).
		if (salto.Procesar(d)) { Terminar(); return; }
		if (lblSaltar != null)
			lblSaltar.Text = salto.Manteniendo ? $"Saltando {salto.Barra()}" : "Mantené ESPACIO o ESC para saltar";
	}

	/// <summary>El tipo entra caminando en el plano 1 y frena junto al prota.</summary>
	void AnimarTipo()
	{
		if (tipo == null || !IsInstanceValid(tipo)) return;
		// Entra entre el seg 5 y el 12 (cubre fin del plano 0 + plano 1).
		float t = Mathf.Clamp((tiempo - 5f) / 7f, 0f, 1f);
		float ts = t * t * (3f - 2f * t);
		Vector3 p = posTipoInicio.Lerp(posTipoFin, ts);
		bool caminando = t > 0f && t < 1f;
		p.Y = ProcPeaton.YVereda + (caminando ? Mathf.Abs(Mathf.Sin(tiempo * 9f)) * 0.05f : 0f);
		tipo.GlobalPosition = p;
		MirarA(tipo, protagonista != null ? protagonista.GlobalPosition : posSuelo);
		float amp = caminando ? Mathf.Sin(tiempo * 9f) * 0.5f : 0f;
		if (pataIzqT != null && IsInstanceValid(pataIzqT)) pataIzqT.Rotation = new Vector3(amp, 0, 0);
		if (pataDerT != null && IsInstanceValid(pataDerT)) pataDerT.Rotation = new Vector3(-amp, 0, 0);
		if (brazoIzqT != null && IsInstanceValid(brazoIzqT)) brazoIzqT.Rotation = new Vector3(-amp * 0.7f, 0, 0);
		if (brazoDerT != null && IsInstanceValid(brazoDerT)) brazoDerT.Rotation = new Vector3(amp * 0.7f, 0, 0);
	}

	/// <summary>_Input para ganarle al Player y a Pausa. Sin click (se
	/// dispara sin querer): solo registra mantener para el SaltoHold.</summary>
	public override void _Input(InputEvent @event)
	{
		if (!Visible || terminada) return;
		if (@event is not InputEventKey k || k.Echo) return;
		if (k.Pressed) salto.AlPresionar(k.PhysicalKeycode);
		else salto.AlSoltar(k.PhysicalKeycode);
	}

	public void Terminar()
	{
		if (terminada) return;
		terminada = true;
		if (camPrevia != null && IsInstanceValid(camPrevia)) camPrevia.MakeCurrent();
		if (cineCam != null && IsInstanceValid(cineCam)) cineCam.QueueFree();
		if (protagonista != null && IsInstanceValid(protagonista)) protagonista.QueueFree();
		if (tipo != null && IsInstanceValid(tipo)) tipo.QueueFree();
		// Restaurar lo que ocultamos: Player real + HUD.
		if (jugadorReal != null && IsInstanceValid(jugadorReal)) jugadorReal.Visible = true;
		if (hudReal != null && IsInstanceValid(hudReal)) hudReal.Visible = true;
		Visible = false;
		if (!pausaSuelta) { UiPila.SoltarPausa(GetTree(), "intro"); pausaSuelta = true; }
		Terminada?.Invoke();
		QueueFree();
	}
}
