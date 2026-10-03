using Godot;
using System;

/// <summary>
/// Secuencia final (Fase 6): el Tipo vuelve al cruce y ofrece la tirada
/// final (sale el millón) → fundido a blanco: todo fue un sueño →
/// despertar en el dormitorio con diálogos de vida normal → apuesta al
/// fútbol desde el celular → CONTINUARÁ. Liviana (código, sin assets),
/// pausa con UiPila, Esc salta todo (igual guarda final_visto).
/// </summary>
public partial class FinalCinematica : CanvasLayer
{
	public event Action? Terminada;

	const float LetrasPorSeg = 32f; // (subtítulos de fase, si hiciera falta)
	static readonly Vector3 DormitorioEn = new(200, 0, 200);

	Camera3D? cineCam;
	Camera3D? camPrevia;
	CanvasLayer? hudReal;
	AudioManager? audio;
	ColorRect? fade;
	ColorRect? barraSup;
	ColorRect? barraInf;
	Label? lblTitulo;
	Label? lblSaltar;

	Node3D? tipo;
	Node3D? prota;
	Node3D? dormitorio;
	DialogoUI? dialogo;
	TiradaFinalUI? tirada;

	int fase = -1;
	float tFase;
	float tTotal;
	bool terminada;
	bool pausaSuelta;
	readonly SaltoHold salto = new();
	Node3D? jugadorMovido;
	Vector3 posJugador = Vector3.Zero;
	Vector3 miraCruce = Vector3.Zero;
	Vector3 posCamA = Vector3.Zero;

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

		lblTitulo = new Label { HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
		lblTitulo.AddThemeFontSizeOverride("font_size", 44);
		lblTitulo.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
		lblTitulo.SetAnchorsPreset(Control.LayoutPreset.Center);
		lblTitulo.OffsetLeft = -400; lblTitulo.OffsetRight = 400;
		lblTitulo.OffsetTop = -60; lblTitulo.OffsetBottom = 60;
		AddChild(lblTitulo);

		lblSaltar = new Label { Text = "Mantené ESPACIO o ESC para saltar" };
		lblSaltar.AddThemeFontSizeOverride("font_size", 14);
		lblSaltar.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		lblSaltar.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		lblSaltar.HorizontalAlignment = HorizontalAlignment.Center;
		lblSaltar.OffsetTop = -78; lblSaltar.OffsetBottom = -54;
		AddChild(lblSaltar);

		fade = new ColorRect { Color = new Color(0, 0, 0, 0f) };
		fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fade);
	}

	public void Mostrar()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		UiPila.PedirPausa(GetTree(), "final");
		Node? raiz = GetParent() ?? GetTree()?.CurrentScene;
		hudReal = raiz?.GetNodeOrNull<CanvasLayer>("HUD");
		if (hudReal != null && hudReal.Visible) hudReal.Visible = false;
		else hudReal = null;
		audio = raiz?.GetNodeOrNull<AudioManager>("Audio");
		camPrevia = GetViewport()?.GetCamera3D();
		cineCam = new Camera3D { Name = "CineFinal", Fov = 58f };
		(raiz ?? this).AddChild(cineCam);
		cineCam.MakeCurrent();
		SpawnearCruce(raiz);
		dormitorio = Dormitorio.Build(DormitorioEn);
		raiz?.AddChild(dormitorio);
		AcostarProta();
		FaseTipo();
	}

	/// <summary>El Tipo aparece junto al jugador, en el set verificado.</summary>
	void SpawnearCruce(Node? raiz)
	{
		// Teleport al set fijo (CineSet): donde esté el jugador puede haber
		// un árbol en medio. Se restaura en Terminar (el autosave no corre:
		// el árbol está pausado durante la cinemática).
		jugadorMovido = raiz?.GetNodeOrNull<Node3D>("Player");
		if (jugadorMovido == null) jugadorMovido = GetTree()?.CurrentScene?.GetNodeOrNull<Node3D>("Player");
		if (jugadorMovido != null)
		{
			posJugador = jugadorMovido.GlobalPosition;
			jugadorMovido.GlobalPosition = new Vector3(CineSet.Foco.X, 0.5f, CineSet.Foco.Z);
		}
		Vector3 base_ = CineSet.Foco;
		tipo = ProcPeaton.Build(
			new Color(0.16f, 0.16f, 0.2f), new Color(0.1f, 0.1f, 0.13f),
			new Color(0.8f, 0.58f, 0.42f), true, new Color(0.05f, 0.05f, 0.07f));
		tipo.Name = "TipoFinal";
		raiz?.AddChild(tipo);
		tipo.GlobalPosition = base_ + new Vector3(-1.8f, 0, 1.3f);
		MirarA(tipo, base_);
		miraCruce = base_ + new Vector3(-0.9f, 1f, 0.65f);
		posCamA = base_ + new Vector3(2.6f, 2.1f, 3.4f);
		if (cineCam != null)
		{
			cineCam.GlobalPosition = posCamA;
			cineCam.LookAt(miraCruce, Vector3.Up);
		}
	}

	static void MirarA(Node3D n, Vector3 objetivo)
	{
		var t = new Vector3(objetivo.X, n.GlobalPosition.Y, objetivo.Z);
		if ((t - n.GlobalPosition).Length() > 0.05f)
			n.LookAt(t, Vector3.Up);
	}

	/// <summary>Protagonista dormido en la cama (se muestra al despertar).</summary>
	void AcostarProta()
	{
		prota = ProcPeaton.Build(
			new Color(0.5f, 0.5f, 0.55f), new Color(0.3f, 0.32f, 0.38f),
			new Color(0.85f, 0.6f, 0.45f), false);
		prota.Name = "ProtaDormido";
		dormitorio?.AddChild(prota);
		// Cama contra pared oeste: acostado a lo largo (cabeza al norte).
		prota.Position = new Vector3(-1.65f, Dormitorio.AltoColchon + 0.14f, 0.1f);
		prota.RotationDegrees = new Vector3(90, 0, 90);
	}

	void FaseTipo()
	{
		fase = 0;
		tFase = 0f;
		tTotal = 0f;
		if (fade != null) fade.Color = new Color(0, 0, 0, 1f); // tapa el teleport
		dialogo = new DialogoUI();
		AddChild(dialogo);
		dialogo.Terminada += OnTipoDialogo;
		dialogo.MostrarLineas(new (string, string)[]
		{
			("El Tipo", "Nos volvemos a ver... Completaste todo en La Capital. Te parece una nueva tirada? Tal vez ahora tengas mas suerte..."),
			("Vos", "¿Qué tirada?"),
			("El Tipo", "La tirada final. La que te puede hacer millonario. ¿Querés probar?"),
			("Vos", "¿Otra vez? Bueno... dale, no pierdo nada."),
			("El Tipo", "Perfecto. A ver si esta vez la suerte te acompaña..."),
		});
	}

	void OnTipoDialogo()
	{
		if (terminada) return;
		LiberarDialogo();
		fase = 1;
		tFase = 0f;
		tirada = new TiradaFinalUI();
		AddChild(tirada);
		tirada.Terminada += OnMillon;
		tirada.Mostrar();
	}

	void OnMillon(string _id)
	{
		if (terminada) return;
		if (tirada != null && IsInstanceValid(tirada)) tirada.QueueFree();
		tirada = null;
		audio?.Play("monedas", 1.1f);
		fase = 2; // fundido a blanco = salir del sueño
		tFase = 0f;
	}

	public override void _Process(double delta)
	{
		if (!Visible || terminada) return;
		float d = (float)delta;
		tFase += d;
		tTotal += d;
		// Salto con mantener (un Esc sin querer ya no salta nada). En fase 4
		// (ya saltando) se deja correr el fundido hasta Terminar.
		if (fase != 4 && salto.Procesar(d)) { TerminarTodo(); return; }
		if (lblSaltar != null)
			lblSaltar.Text = salto.Manteniendo ? $"Saltando {salto.Barra()}" : "Mantené ESPACIO o ESC para saltar";
		// Fundido de entrada: tapa el teleport al set (solo al inicio).
		if (tTotal < 0.8f && (fase == 0 || fase == 1))
			PonerFade(Colors.Black, Mathf.Clamp(1f - tTotal / 0.8f, 0f, 1f));
		if (fase == 0 && cineCam != null && IsInstanceValid(cineCam))
		{
			// Push lento hacia el Tipo mientras habla.
			cineCam.GlobalPosition = cineCam.GlobalPosition.Lerp(posCamA + new Vector3(-0.7f, -0.3f, -0.8f), d * 0.12f);
			if ((miraCruce - cineCam.GlobalPosition).Length() > 0.05f)
				cineCam.LookAt(miraCruce, Vector3.Up);
		}
		else if (fase == 2)
		{
			PonerFade(Colors.White, Mathf.Clamp(tFase / 1.2f, 0f, 1f));
			if (tFase >= 1.3f) FaseDespertar();
		}
		else if (fase == 3)
		{
			PonerFade(Colors.White, Mathf.Clamp(1f - tFase / 1.5f, 0f, 1f));
			AnimarDormitorio(d);
		}
		else if (fase == 4)
		{
			PonerFade(Colors.Black, Mathf.Clamp(tFase / 1.2f, 0f, 1f));
			if (lblTitulo != null)
			{
				lblTitulo.Visible = tFase > 1.2f;
				lblTitulo.Text = "CONTINUARÁ...";
			}
			if (tFase >= 3.7f) Terminar();
		}
	}

	void PonerFade(Color c, float a)
	{
		if (fade == null) return;
		fade.Color = new Color(c.R, c.G, c.B, a);
	}

	void FaseDespertar()
	{
		fase = 3;
		tFase = 0f;
		if (tipo != null && IsInstanceValid(tipo)) tipo.QueueFree();
		tipo = null;
		// Cámara del dormitorio: plano abierto de la cama y la ventana.
		if (cineCam != null && IsInstanceValid(cineCam))
		{
			cineCam.GlobalPosition = DormitorioEn + new Vector3(1.7f, 1.8f, 1.4f);
			cineCam.LookAt(DormitorioEn + new Vector3(-1.2f, 0.7f, -0.3f), Vector3.Up);
		}
		dialogo = new DialogoUI();
		AddChild(dialogo);
		dialogo.Terminada += OnManianaTerminada;
		dialogo.MostrarLineas(new (string, string)[]
		{
			("Vos", "Mmm... que sueño mas raro... ¿qué hora es? Las 7. Otro lunes."),
			("Vos", "El colectivo sale en 20 minutos. Llego tarde al laburo de vuelta..."),
			("Vos", "(miro el celular) A ver qué hay... ¡hoy juega el Rojo! Paga x3."),
			("Celular", "Apuesta hecha: gana el Rojo. Que sea lo que Dios quiera."),
			("Vos", "Bueno dale que pierdo el colectivo."),
		});
	}

	/// <summary>Al llegar a la apuesta: pantalla encendida + plano al celular.</summary>
	void AnimarDormitorio(float d)
	{
		if (dialogo == null || !IsInstanceValid(dialogo)) return;
		if (dialogo.LineaActual >= 3)
		{
			Dormitorio.SetPantalla(dormitorio, true);
			if (cineCam != null && IsInstanceValid(cineCam))
			{
				var cerca = DormitorioEn + new Vector3(-0.1f, 1.2f, 0.1f);
				var miraCelu = DormitorioEn + new Vector3(-0.8f, 0.55f, -0.75f);
				cineCam.GlobalPosition = cineCam.GlobalPosition.Lerp(cerca, d * 0.8f);
				if ((miraCelu - cineCam.GlobalPosition).Length() > 0.05f)
					cineCam.LookAt(miraCelu, Vector3.Up);
			}
		}
	}

	void OnManianaTerminada()
	{
		if (terminada) return;
		LiberarDialogo();
		fase = 4;
		tFase = 0f;
	}

	void LiberarDialogo()
	{
		if (dialogo != null)
		{
			if (IsInstanceValid(dialogo))
			{
				dialogo.Terminada -= OnTipoDialogo;
				dialogo.Terminada -= OnManianaTerminada;
				dialogo.QueueFree();
			}
			dialogo = null;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible || terminada) return;
		// Esc/otros solo registran mantener (el skip global es con hold).
		// Los hijos no manejan Esc a propósito.
		if (@event is not InputEventKey k || k.Echo) return;
		if (k.Pressed) salto.AlPresionar(k.PhysicalKeycode);
		else salto.AlSoltar(k.PhysicalKeycode);
	}

	/// <summary>Skip: cierra UIs, va al negro y termina (igual guarda el final).</summary>
	void TerminarTodo()
	{
		LiberarDialogo();
		if (tirada != null)
		{
			if (IsInstanceValid(tirada)) tirada.QueueFree();
			tirada = null;
		}
		if (tipo != null && IsInstanceValid(tipo)) tipo.QueueFree();
		tipo = null;
		fase = 4;
		tFase = 0.8f; // entra casi directo al cartel
	}

	public void Terminar()
	{
		if (terminada) return;
		terminada = true;
		SaveSystem.GuardarHistoria(null, 0, null, null, true);
		// El jugador vuelve EXACTO a donde estaba (teleport solo visual).
		if (jugadorMovido != null && IsInstanceValid(jugadorMovido))
			jugadorMovido.GlobalPosition = posJugador;
		if (camPrevia != null && IsInstanceValid(camPrevia)) camPrevia.MakeCurrent();
		if (cineCam != null && IsInstanceValid(cineCam)) cineCam.QueueFree();
		if (tipo != null && IsInstanceValid(tipo)) tipo.QueueFree();
		if (prota != null && IsInstanceValid(prota)) prota.QueueFree();
		if (dormitorio != null && IsInstanceValid(dormitorio)) dormitorio.QueueFree();
		LiberarDialogo();
		if (tirada != null && IsInstanceValid(tirada)) tirada.QueueFree();
		if (hudReal != null && IsInstanceValid(hudReal)) hudReal.Visible = true;
		Visible = false;
		if (!pausaSuelta) { UiPila.SoltarPausa(GetTree(), "final"); pausaSuelta = true; }
		Terminada?.Invoke();
		QueueFree();
	}
}
