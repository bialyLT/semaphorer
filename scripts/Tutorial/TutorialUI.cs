using Godot;
using System;

/// <summary>
/// Interfaz del tutorial: muestra pasos con texto explicativo en un panel
/// overlay. Se usa tanto en el menú de inicio como durante la primera partida.
/// </summary>
public partial class TutorialUI : CanvasLayer
{
	PanelContainer? panel;
	ColorRect? fondo;
	VBoxContainer? cajaContenido;
	Label? lblTitulo;
	Label? lblDescripcion;
	Label? lblProgreso;
	Button? btnSiguiente;
	Button? btnAtras;
	Button? btnSaltar;
	int pasoActual = 0;

	// --- Modo interactivo (toast abajo, sin pausa) ---
	// TEXTOS DEL TUTORIAL JUGABLE: editar en ObtenerTextoInteractivo() abajo.
	public bool ModoInteractivo { get; private set; } = false;
	PanelContainer? toast;
	Label? lblToastTitulo;
	Label? lblToastSub;
	Label? lblToastProg;
	Button? btnToastSaltar;
	Button? btnToastOk;

	public bool EnJuego { get; set; } = false;

	public override void _Ready()
	{
		Layer = 100;
		// El tutorial frena el juego, así este nodo debe seguir procesando
		// input con el árbol pausado o los botones y el Escape no responden.
		ProcessMode = ProcessModeEnum.Always;
		ConstruirUI();
		Visible = false;
	}

	/// <summary>
	/// _Input (no _UnhandledInput) para ganarle al Player, que también
	/// escucha Escape para la pausa. Marcar el evento como manejado evita
	/// que se abra el menú de pausa detrás del tutorial.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
		if (ModoInteractivo)
		{
			// En juego las teclas son del Player (WASD/E/F/G/Espacio):
			// solo Esc salta el tutorial. En el paso final, Space/Enter/E
			// también lo cierran pero SIN consumir (el juego las recibe igual).
			if (k.PhysicalKeycode == Key.Escape)
			{
				OnSaltarInteractivo();
				GetViewport().SetInputAsHandled();
			}
			else if (pasoActual >= TutorialManager.TotalInteractivo - 1 &&
				(k.PhysicalKeycode == Key.Space || k.PhysicalKeycode == Key.Enter ||
				k.PhysicalKeycode == Key.KpEnter || k.PhysicalKeycode == Key.E))
			{
				CompletarInteractivo();
			}
			return;
		}
		switch (k.PhysicalKeycode)
		{
			case Key.Escape:
				OnSaltar();
				GetViewport().SetInputAsHandled();
				break;
			case Key.Space:
			case Key.Enter:
			case Key.KpEnter:
				OnSiguiente();
				GetViewport().SetInputAsHandled();
				break;
			case Key.A:
			case Key.Left:
				OnAtras();
				GetViewport().SetInputAsHandled();
				break;
		}
	}

	void ConstruirUI()
	{
		fondo = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.7f)
		};
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fondo);

		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		UiTheme.Centrar(this, panel, new Vector2(540, 0));

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 14);
		panel.AddChild(caja);

		lblTitulo = new Label();
		lblTitulo.AddThemeFontSizeOverride("font_size", 32);
		lblTitulo.AddThemeColorOverride("font_color", new Color(1, 0.75f, 0.2f));
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblTitulo);

		lblDescripcion = new Label();
		lblDescripcion.AddThemeFontSizeOverride("font_size", 18);
		lblDescripcion.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		lblDescripcion.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblDescripcion.CustomMinimumSize = new Vector2(460, 0);
		caja.AddChild(lblDescripcion);

		lblProgreso = new Label();
		lblProgreso.AddThemeFontSizeOverride("font_size", 14);
		lblProgreso.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		lblProgreso.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblProgreso);

		var separador = new HSeparator();
		caja.AddChild(separador);

		var cajaBotones = new HBoxContainer();
		cajaBotones.AddThemeConstantOverride("separation", 16);
		cajaBotones.Alignment = BoxContainer.AlignmentMode.Center;
		caja.AddChild(cajaBotones);

		btnAtras = new Button { Text = "← Atrás", CustomMinimumSize = new Vector2(120, 44) };
		btnAtras.Pressed += OnAtras;
		cajaBotones.AddChild(btnAtras);

		btnSiguiente = new Button { Text = "Siguiente →", CustomMinimumSize = new Vector2(140, 44) };
		btnSiguiente.Pressed += OnSiguiente;
		cajaBotones.AddChild(btnSiguiente);

		btnSaltar = new Button { Text = "Saltar tutorial", CustomMinimumSize = new Vector2(140, 44) };
		btnSaltar.Pressed += OnSaltar;
		cajaBotones.AddChild(btnSaltar);
		UiTheme.BotonesFoco(btnAtras, btnSiguiente, btnSaltar);
		ConstruirToast();
	}

	/// <summary>Banner inferior no-modal: no tapa ni pausa el juego.
	/// Los clicks pasan al juego salvo en los botones (panel IGNORE).</summary>
	void ConstruirToast()
	{
		toast = new PanelContainer();
		toast.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		toast.AnchorLeft = 0.5f; toast.AnchorRight = 0.5f;
		toast.AnchorTop = 1f; toast.AnchorBottom = 1f;
		toast.OffsetLeft = -290; toast.OffsetRight = 290;
		toast.OffsetTop = -168; toast.OffsetBottom = -16;
		toast.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(toast);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 4);
		caja.MouseFilter = Control.MouseFilterEnum.Ignore;
		toast.AddChild(caja);

		lblToastTitulo = new Label();
		lblToastTitulo.AddThemeFontSizeOverride("font_size", 20);
		lblToastTitulo.AddThemeColorOverride("font_color", UiTheme.NaranjaClaro);
		lblToastTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		lblToastTitulo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblToastTitulo.MouseFilter = Control.MouseFilterEnum.Ignore;
		caja.AddChild(lblToastTitulo);

		lblToastSub = new Label();
		lblToastSub.AddThemeFontSizeOverride("font_size", 15);
		lblToastSub.AddThemeColorOverride("font_color", UiTheme.Gris);
		lblToastSub.HorizontalAlignment = HorizontalAlignment.Center;
		lblToastSub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblToastSub.MouseFilter = Control.MouseFilterEnum.Ignore;
		caja.AddChild(lblToastSub);

		lblToastProg = new Label();
		lblToastProg.AddThemeFontSizeOverride("font_size", 13);
		lblToastProg.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
		lblToastProg.HorizontalAlignment = HorizontalAlignment.Center;
		lblToastProg.MouseFilter = Control.MouseFilterEnum.Ignore;
		caja.AddChild(lblToastProg);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 10);
		caja.AddChild(fila);

		btnToastSaltar = new Button { Text = "Saltar (Esc)", CustomMinimumSize = new Vector2(120, 32) };
		btnToastSaltar.FocusMode = Control.FocusModeEnum.None;
		btnToastSaltar.Pressed += OnSaltarInteractivo;
		fila.AddChild(btnToastSaltar);

		btnToastOk = new Button { Text = "¡A laburar!", CustomMinimumSize = new Vector2(140, 32) };
		btnToastOk.FocusMode = Control.FocusModeEnum.None;
		btnToastOk.Pressed += CompletarInteractivo;
		fila.AddChild(btnToastOk);

		toast.Visible = false;
	}

	bool pausaPedida;

	public void Mostrar(int paso = 0)
	{
		ModoInteractivo = false;
		if (toast != null) toast.Visible = false;
		pasoActual = paso;
		ActualizarContenido();
		if (panel != null) panel.Visible = true;
		if (fondo != null) fondo.Visible = true;
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		// Leyendo el tutorial no debería costarte un decomiso: el juego
		// espera (y el HUD sigue debajo del overlay).
		if (EnJuego && !pausaPedida)
		{
			UiPila.PedirPausa(GetTree(), "tutorial");
			pausaPedida = true;
		}
		if (btnSiguiente != null) UiTheme.FocoInicial(btnSiguiente);
	}

	public void Ocultar()
	{
		Visible = false;
		ModoInteractivo = false;
		if (toast != null) toast.Visible = false;
		if (EnJuego && pausaPedida)
		{
			UiPila.SoltarPausa(GetTree(), "tutorial");
			pausaPedida = false;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	// --- Tutorial jugable (sin pausa): un paso = una acción en el mundo ---
	// Los textos viven en ObtenerTextoInteractivo() para cambiarlos fácil.

	public void MostrarInteractivo(int paso = 0)
	{
		ModoInteractivo = true;
		EnJuego = true;
		pasoActual = paso;
		// El juego SIGUE (sin UiPila, sin cambiar el mouse): el toast no tapa.
		if (panel != null) panel.Visible = false;
		if (fondo != null) fondo.Visible = false;
		if (toast != null) toast.Visible = true;
		Visible = true;
		ActualizarInteractivo();
	}

	public void OcultarInteractivo()
	{
		if (toast != null) toast.Visible = false;
		Visible = false;
		ModoInteractivo = false;
		// Sin pausa pedida acá: no se toca UiPila ni el mouse.
	}

	/// <summary>Avanza el toast al paso dado; si se pasa del final, completa.</summary>
	public void AvanzarInteractivo(int nuevoPaso)
	{
		if (!ModoInteractivo) return;
		if (nuevoPaso >= TutorialManager.TotalInteractivo)
		{
			CompletarInteractivo();
			return;
		}
		pasoActual = nuevoPaso;
		TutorialManager.PasoActual = nuevoPaso;
		ActualizarInteractivo();
	}

	public void CompletarInteractivo()
	{
		if (!TutorialManager.TutorialActivo) { OcultarInteractivo(); return; }
		TutorialManager.CompletarTutorial();
		OcultarInteractivo();
	}

	void OnSaltarInteractivo()
	{
		if (!TutorialManager.TutorialActivo) { OcultarInteractivo(); return; }
		TutorialManager.SaltarTutorial();
		OcultarInteractivo();
	}

	void ActualizarInteractivo()
	{
		if (lblToastTitulo == null || lblToastSub == null || lblToastProg == null) return;
		var (titulo, sub) = ObtenerTextoInteractivo(pasoActual);
		lblToastTitulo.Text = titulo;
		lblToastSub.Text = sub;
		lblToastSub.Visible = !string.IsNullOrEmpty(sub);
		lblToastProg.Text = $"Paso {pasoActual + 1} de {TutorialManager.TotalInteractivo} · Esc salta";
		bool esFinal = pasoActual >= TutorialManager.TotalInteractivo - 1;
		if (btnToastOk != null) btnToastOk.Visible = esFinal;
		if (btnToastSaltar != null) btnToastSaltar.Visible = !esFinal;
	}

	/// <summary>
	/// TEXTOS del tutorial jugable (título + subtítulo por paso).
	/// Paso 0 calle · 1 auto en rojo · 2 cobrar con E · 3 vereda con Espacio ·
	/// 4 mochila con G · 5 tienda con F · 6 aviso del tutorial del menú.
	/// </summary>
	public static (string titulo, string sub) ObtenerTextoInteractivo(int paso)
	{
		return paso switch
		{
			0 => ("Bajá a la calle con WASD", ""),
			1 => ("Acercate a un auto frenado en Rojo",
				"Vale patrulla también si está frenado."),
			2 => ("Apretá E hasta cobrar", ""),
			3 => ("Apretá Espacio para saltar y subir a la vereda", ""),
			4 => ("Buscá la mochila azul en la esquina este",
				"Parate al lado y apretá G para guardar"),
			5 => ("Apretá F para ver tus mejoras disponibles",
				"Todavía no tenés plata suficiente, ¡pero chusmeá cuál vas primero!"),
			6 => ("¡Listo! El tutorial completo está en el menú de inicio",
				"Botón Tutorial en el menú principal"),
			_ => ("", "")
		};
	}

	void ActualizarContenido()
	{
		if (lblTitulo == null || lblDescripcion == null || lblProgreso == null || btnSiguiente == null)
			return;

		var (titulo, descripcion, tamano) = ObtenerContenidoPaso(pasoActual);
		lblTitulo.Text = titulo;
		lblDescripcion.Text = descripcion;
		lblDescripcion.AddThemeFontSizeOverride("font_size", tamano);
		lblProgreso.Text = $"Paso {pasoActual + 1} de {TutorialManager.TotalPasos}";

		if (pasoActual >= TutorialManager.TotalPasos - 1)
		{
			btnSiguiente.Text = "¡Empezar a laburar!";
		}
		else
		{
			btnSiguiente.Text = "Siguiente →";
		}
		if (btnAtras != null) btnAtras.Disabled = pasoActual <= 0;
	}

	/// <summary>Título, cuerpo y tamaño de fuente de cada paso (el último
	/// va en 15 porque tiene más texto).</summary>
	(string titulo, string desc, int tamano) ObtenerContenidoPaso(int paso)
	{
		return paso switch
		{
			0 => ("🔧 Controles básicos",
				"• WASD o flechas: moverte\n" +
				"• Mouse: mover cámara\n" +
				"• V: cambiar cámara (1ra/3ra persona)\n" +
				"• Espacio: saltar (subir a la vereda)\n" +
				"• Esc: pausa",
				18),

			1 => TextoComoTrabajar(),

			2 => ("😎 Trato con conductores",
				"Los conductores tienen HUMOR:\n" +
				"• Amable: te da propina extra en verde\n" +
				"• Apurado: si tardás mucho, se va sin pagar\n" +
				"• Enojado: puede rechazar el pago\n" +
				"• Con mal humor y el semáforo en VERDE, el auto se va sin pagar\n" +
				"• Mantené el ritmo y cobrá rápido",
				18),

			3 => ("🚨 Trato con la policía",
				"• No te quedes en la CALLE\n" +
				"• La barra roja sube si te ven mucho tiempo en la calle\n" +
				"• Si llega al 100%: DECOMISO\n" +
				"• Perdés toda la plata encima y las mejoras\n" +
				"• Volvé a la VEREDA para bajar la alerta",
				18),

			4 => ("🎒 La mochila / escondite",
				"• Está en la vereda este, cerca de la esquina\n" +
				"• Es la MOCHILA AZUL en el piso\n" +
				"• Presioná G cerca para usarla\n" +
				"• Guardás toda la plata que tenés encima\n" +
				"• La policía NO puede decomisar lo guardado\n" +
				"• Usá I para ver tu inventario",
				18),

			5 => TextoMejoras(),

			6 => TextoOficio(),

			_ => ("", "", 18)
		};
	}

	/// <summary>
	/// Paso 1 según TU oficio (antes decía "Cómo limpiar vidrios" para todos
	/// y los malabaristas/vendedores veían mensajes de otro rubro).
	/// </summary>
	static (string titulo, string desc, int tamano) TextoComoTrabajar()
	{
		return SaveSystem.CargarOficio() switch
		{
			"malabarista" => ("🎾 Cómo hacer malabares",
				"• Parate frente a un auto DETENIDO en ROJO\n" +
				"• Presioná E para actuar\n" +
				"• Mantené el RITMO: ni muy rápido ni muy lento",
				18),
			"vendedor" => ("🥬 Cómo ofrecer",
				"• Acercate a un auto DETENIDO en ROJO\n" +
				"• Presioná E para ofrecer\n" +
				"• Mantené el RITMO de pregón: ni muy rápido ni muy lento",
				18),
			_ => ("🫧 Cómo limpiar vidrios",
				"• Acercate a un vehículo DETENIDO en ROJO\n" +
				"• Presioná E para limpiar\n" +
				"• Mantené el RITMO: ni muy rápido ni muy lento",
				18),
		};
	}

	/// <summary>Paso de mejoras: solo las de tu oficio + las compartidas.</summary>
	static (string titulo, string desc, int tamano) TextoMejoras()
	{
		string equipo = SaveSystem.CargarOficio() switch
		{
			"malabarista" => "• Pelotas: cada acto de malabares vale más\n",
			"vendedor" => "",
			_ => "• Esponja: cada toque de E sirve más, menos golpes\n" +
				"• Balde: +$ fijo por cada parabrisas que cobrás\n",
		};
		string calle = "• Zapas: caminás más rápido\n" +
			"• Labia: apurados y el amable esperan más tiempo\n" +
			(SaveSystem.CargarOficio() == "limpiavidrios" ? "" : "• Pregón: +$ propina en malabares y venta\n");
		string negocio = "• Tráfico: más autos frenan en tu semáforo (pide Labia)\n" +
			"• Sigilo: la policía te ve desde más lejos (pide Zapas o Labia)\n" +
			(SaveSystem.CargarOficio() switch
			{
				"vendedor" => "• Canasta: +$ por venta (pide Pregón)\n",
				_ when SaveSystem.CargarOficio() == "limpiavidrios" => "• Tarifa: +$ por servicio (pide Esponja o Balde)\n",
				_ => "",
			});
		return ("⭐ El árbol de mejoras",
			"• F abre la tienda (solo tu oficio + compartidas)\n" +
			"\nEQUIPO — tu herramienta\n" + equipo +
			"\nCALLE — cómo te movés\n" + calle +
			"\nNEGOCIO — cuánto ganás\n" + negocio +
			"\n• Con candado: primero comprás el nivel que pide\n" +
			"• Comprás con click o con las teclas 1-9",
			15);
	}
	static (string titulo, string desc, int tamano) TextoOficio()
	{
		return SaveSystem.CargarOficio() switch
		{
			"malabarista" => ("🎾 Tu oficio: malabarista",
				"• Parate frente a un auto DETENIDO en ROJO\n" +
				"• Presioná E con RITMO: 5 actos por función\n" +
				"• Ni muy rápido ni muy lento (si te dormís, se cae la pelota)\n" +
				"• Buena función = propina; el enojado puede no pagar\n" +
				"• Pelotas pro: cada acto vale más · Pregón: +$ propina",
				18),
			"vendedor" => ("🥬 Tu oficio: vendedor",
				"• Acercate a un auto DETENIDO en ROJO\n" +
				"• Presioná E para ofrecer: 3 ofertas por venta\n" +
				"• Con ritmo de pregón: ni atolondrado ni dormido\n" +
				"• Venta rápida y chica; el apurado no espera mucho\n" +
				"• Canasta: +$ por venta · Pregón: +$ propina",
				18),
			_ => ("🧽 Tu oficio: limpiavidrios",
				"• Acercate a un auto DETENIDO en ROJO\n" +
				"• Presioná E con RITMO: 6 toques por parabrisas\n" +
				"• El amable da changüí en verde; el apurado se va si tardás\n" +
				"• Esponja: menos toques · Balde y Tarifa: +$ por servicio",
				18),
		};
	}

	void OnAtras()
	{
		if (pasoActual <= 0) return;
		pasoActual--;
		TutorialManager.PasoActual = pasoActual;
		ActualizarContenido();
	}

	void OnSiguiente()
	{
		if (pasoActual < TutorialManager.TotalPasos - 1)
		{
			pasoActual++;
			TutorialManager.PasoActual = pasoActual;
			ActualizarContenido();
		}
		else
		{
			TutorialManager.CompletarTutorial();
			Ocultar();
		}
	}

	void OnSaltar()
	{
		TutorialManager.SaltarTutorial();
		Ocultar();
	}
}
