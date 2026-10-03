using Godot;
using System.Collections.Generic;

/// <summary>
/// Menú principal de Semaphorer: título, 3 slots de partida
/// (jugar / nueva con confirmación / borrar), opciones y salir.
/// UI 100% por código. Es la escena inicial del juego.
/// </summary>
public partial class Menu : CanvasLayer
{
	PanelContainer? panel;
	VBoxContainer? cajaSlots;
	VBoxContainer? cajaOpciones;
	Label? lblOpciones;
	readonly Dictionary<Button, int> armados = new();

	// --- Modal de borrado (la X armada se tocaba sin querer) ---
	ColorRect? modalFondo;
	Label? modalTitulo;
	Label? modalDetalle;
	Button? btnModalBorrar;
	Button? btnModalCancelar;
	int slotPendiente;

	TutorialUI? tutorial;

	public override void _Ready()
	{
		Ajustes.Cargar();
		Ajustes.AplicarAlIniciarEscena();
		SaveSystem.MigrarLegado();
		ConstruirUI();
		RefrescarSlots();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		tutorial = new TutorialUI();
		AddChild(tutorial);
		PonerMusicaMenu();
	}

	/// <summary>Música de fondo del menú (se libera sola al cambiar de escena).</summary>
	void PonerMusicaMenu()
	{
		var musica = GD.Load<AudioStream>("res://audio/musica_menu.wav");
		if (musica == null)
		{
			GD.PushWarning("[Menu] falta audio/musica_menu.wav (generar con tools/generar_audio.py)");
			return;
		}
		AudioManager.AplicarLoop(musica);
		var p = new AudioStreamPlayer { Bus = "Master", VolumeDb = -14f, Stream = musica };
		AddChild(p);
		p.Play();
	}

	void ConstruirUI()
	{
		var fondo = new ColorRect { Color = new Color(0.07f, 0.08f, 0.12f) };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fondo);

		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		UiTheme.Centrar(this, panel, new Vector2(520, 0));

		var margen = new MarginContainer();
		margen.AddThemeConstantOverride("margin_left", 24);
		margen.AddThemeConstantOverride("margin_right", 24);
		margen.AddThemeConstantOverride("margin_top", 18);
		margen.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margen);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 10);
		margen.AddChild(caja);

		var titulo = new Label { Text = "SEMAPHORER" };
		titulo.AddThemeFontSizeOverride("font_size", UiTheme.TituloGrande);
		titulo.AddThemeColorOverride("font_color", UiTheme.Naranja);
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(titulo);

		var sub = new Label { Text = "limpiavidrios · malabares · venta — esquivá a la policía · llená la mochila" };
		UiTheme.CuerpoGris(sub, 15);
		caja.AddChild(sub);

		cajaSlots = new VBoxContainer();
		cajaSlots.AddThemeConstantOverride("separation", 6);
		caja.AddChild(cajaSlots);

		var filaMenu = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		filaMenu.AddThemeConstantOverride("separation", 12);
		caja.AddChild(filaMenu);

		var bTutorial = new Button { Text = "Tutorial" };
		bTutorial.Pressed += () => tutorial?.Mostrar(0);
		filaMenu.AddChild(bTutorial);
		var bOpc = new Button { Text = "Opciones" };
		bOpc.Pressed += () =>
		{
			if (cajaOpciones != null) cajaOpciones.Visible = !cajaOpciones.Visible;
			if (lblOpciones != null) lblOpciones.Visible = cajaOpciones?.Visible ?? false;
			if (cajaOpciones?.Visible ?? false) UiTheme.FocoInicial(bTutorial);
		};
		filaMenu.AddChild(bOpc);
		var bSalir = new Button { Text = "Salir" };
		bSalir.Pressed += () => GetTree()?.Quit();
		filaMenu.AddChild(bSalir);
		UiTheme.BotonesFoco(bTutorial, bOpc, bSalir);
		UiTheme.FocoInicial(bTutorial);

		lblOpciones = new Label { Text = "— Opciones —", Visible = false };
		lblOpciones.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(lblOpciones);
		cajaOpciones = Ajustes.CrearPanel();
		cajaOpciones.Visible = false;
		caja.AddChild(cajaOpciones);

		var ayuda = new Label { Text = "WASD moverse · E trabajar (según tu oficio) · F mejoras · G escondite · Espacio saltar" };
		ayuda.AddThemeFontSizeOverride("font_size", 13);
		ayuda.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
		ayuda.HorizontalAlignment = HorizontalAlignment.Center;
		ayuda.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		caja.AddChild(ayuda);

		var version = new Label { Text = $"v{VersionJuego.Actual}" };
		version.AddThemeFontSizeOverride("font_size", 13);
		version.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
		version.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(version);

		ConstruirModalBorrar();
	}

	/// <summary>Modal de confirmación para borrar (último, así dibuja arriba).</summary>
	void ConstruirModalBorrar()
	{
		modalFondo = new ColorRect { Name = "ModalBorrarFondo", Color = new Color(0, 0, 0, 0.7f), Visible = false };
		modalFondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(modalFondo);

		var centro = new CenterContainer();
		centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		modalFondo.AddChild(centro);

		var mp = new PanelContainer();
		mp.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		mp.CustomMinimumSize = new Vector2(440, 0);
		centro.AddChild(mp);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 10);
		mp.AddChild(caja);

		modalTitulo = new Label { Text = "¿Borrar partida?" };
		modalTitulo.AddThemeFontSizeOverride("font_size", 26);
		modalTitulo.AddThemeColorOverride("font_color", UiTheme.Mal);
		modalTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(modalTitulo);

		modalDetalle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		modalDetalle.AddThemeFontSizeOverride("font_size", 16);
		modalDetalle.AddThemeColorOverride("font_color", UiTheme.Texto);
		modalDetalle.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(modalDetalle);

		var aviso = new Label { Text = "Se pierde todo: plata, mejoras y progreso." };
		aviso.AddThemeFontSizeOverride("font_size", 14);
		aviso.AddThemeColorOverride("font_color", UiTheme.Gris);
		aviso.HorizontalAlignment = HorizontalAlignment.Center;
		caja.AddChild(aviso);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 16);
		caja.AddChild(fila);

		btnModalBorrar = new Button { Text = "Borrar", CustomMinimumSize = new Vector2(130, 44) };
		btnModalBorrar.AddThemeColorOverride("font_color", UiTheme.Mal);
		btnModalBorrar.Pressed += OnModalBorrar;
		fila.AddChild(btnModalBorrar);

		btnModalCancelar = new Button { Text = "Cancelar", CustomMinimumSize = new Vector2(130, 44) };
		btnModalCancelar.Pressed += OcultarModalBorrar;
		fila.AddChild(btnModalCancelar);
		UiTheme.BotonesFoco(btnModalBorrar, btnModalCancelar);
	}

	void MostrarModalBorrar(int slot, string detalle)
	{
		slotPendiente = slot;
		if (modalTitulo != null) modalTitulo.Text = $"¿Borrar partida {slot}?";
		if (modalDetalle != null) modalDetalle.Text = detalle;
		if (modalFondo != null) modalFondo.Visible = true;
		// Foco en lo seguro: Enter cancela, hay que elegir Borrar a propósito.
		if (btnModalCancelar != null) UiTheme.FocoInicial(btnModalCancelar);
	}

	void OcultarModalBorrar()
	{
		if (modalFondo != null) modalFondo.Visible = false;
		slotPendiente = 0;
	}

	void OnModalBorrar()
	{
		if (slotPendiente < 1) { OcultarModalBorrar(); return; }
		SaveSystem.BorrarSlot(slotPendiente);
		OcultarModalBorrar();
		RefrescarSlots();
	}

	/// <summary>Esc cierra el modal (no hace nada más en el menú).</summary>
	public override void _Input(InputEvent @event)
	{
		if (modalFondo == null || !modalFondo.Visible) return;
		if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.Escape)
		{
			OcultarModalBorrar();
			GetViewport().SetInputAsHandled();
		}
	}

	void RefrescarSlots()
	{
		if (cajaSlots == null) return;
		foreach (var h in cajaSlots.GetChildren()) h.QueueFree();
		armados.Clear();
		for (int i = 1; i <= SaveSystem.Slots; i++)
		{
			int slot = i;
			var res = SaveSystem.LeerResumen(slot);
			var fila = new HBoxContainer();
			fila.AddThemeConstantOverride("separation", 8);
			cajaSlots.AddChild(fila);

			string detalle = res.Existe
				? $"Partida {slot}: {EmojiOficio(res.Oficio)} {NombreOficio(res.Oficio)} · {Ciudades.Nombre(res.Ciudad)} · ${res.Coins} · {res.Items} mejoras · escondite ${res.Guardado}{(res.FinalVisto ? " · ★ sueño cumplido" : "")}"
				: $"Partida {slot}: vacía";
			var info = new Label
			{
				Text = detalle,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			info.AddThemeFontSizeOverride("font_size", 17);
			fila.AddChild(info);

			var bJugar = new Button { Text = "Jugar", Disabled = !res.Existe };
			bJugar.Pressed += () => { SaveSystem.SlotActual = slot; Jugar(); };
			bJugar.FocusMode = Control.FocusModeEnum.All;
			fila.AddChild(bJugar);

			var bNueva = new Button { Text = "Nueva" };
			bNueva.FocusMode = Control.FocusModeEnum.All;
			bNueva.Pressed += () =>
			{
				if (res.Existe && !Confirmado(bNueva, "¿Seguro?")) return; // queda armado
				SaveSystem.BorrarSlot(slot);
				SaveSystem.SlotActual = slot;
				Jugar();
			};
			fila.AddChild(bNueva);

			if (res.Existe)
			{
				var bBorrar = new Button { Text = "X" };
				bBorrar.FocusMode = Control.FocusModeEnum.All;
				bBorrar.Pressed += () => MostrarModalBorrar(slot, detalle);
				fila.AddChild(bBorrar);
			}
		}
	}

	/// <summary>Emoji y nombre del oficio guardado en el slot.</summary>
	static string EmojiOficio(string id) => id switch
	{
		"malabarista" => "🎾",
		"vendedor" => "🥬",
		_ => "🧽",
	};

	static string NombreOficio(string id) => id switch
	{
		"malabarista" => "Malabarista",
		"vendedor" => "Vendedor",
		_ => "Limpiavidrios",
	};

	/// <summary>Doble click para confirmar acciones destructivas.</summary>
	bool Confirmado(Button b, string texto)
	{
		if (armados.TryGetValue(b, out int n) && n > 0) return true;
		armados[b] = 1;
		b.Text = texto;
		return false;
	}

	void Jugar()
	{
		Ajustes.SincronizarDesdeVentana();
		GetTree()?.ChangeSceneToFile("res://scenes/Cruce.tscn");
	}
}
