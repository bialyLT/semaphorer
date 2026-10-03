using Godot;
using System;

/// <summary>
/// Cinemática de viaje entre ciudades: fundido a negro (ahí corre el
/// re-skin de edificios sin pop visible), cartel origen → destino,
/// travelling aéreo y subtítulo con el pago de la ciudad nueva.
/// Todo por código (liviano y compatible, como la intro). Pausa con
/// UiPila, skipeable (Esc/Espacio/Enter/click). Al terminar emite
/// Terminada; si se salta antes del negro, el re-skin corre igual.
/// </summary>
public partial class ViajeCinematica : CanvasLayer
{
	public event Action? Terminada;

	const float TNegro = 1.2f;   // fundido de ida completo: momento del re-skin
	const float TCartel = 2.5f;  // cartel sobre negro
	const float TFin = 10.5f;    // duración total
	const float LetrasPorSeg = 32f;

	Camera3D? cineCam;
	Camera3D? camPrevia;
	CanvasLayer? hudReal;
	ColorRect? fade;
	ColorRect? barraSup;
	ColorRect? barraInf;
	Label? lblTitulo;
	Label? lblSub;
	Label? lblSaltar;

	string subA = "";
	string subB = "";
	float tiempo;
	bool terminada;
	bool pausaSuelta;
	readonly SaltoHold salto = new();
	bool reskinHecho;
	Action? alNegro;
	readonly Vector3 mira = new(0, 1f, 0);

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

		lblTitulo = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		lblTitulo.AddThemeFontSizeOverride("font_size", 40);
		lblTitulo.AddThemeColorOverride("font_color", new Color(1, 0.75f, 0.2f));
		lblTitulo.SetAnchorsPreset(Control.LayoutPreset.Center);
		lblTitulo.OffsetLeft = -400; lblTitulo.OffsetRight = 400;
		lblTitulo.OffsetTop = -60; lblTitulo.OffsetBottom = 60;
		AddChild(lblTitulo);

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

		fade = new ColorRect { Color = new Color(0, 0, 0, 0f) };
		fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fade);
	}

	/// <summary>Muestra el viaje origen → destino; alNegro corre bajo negro total.</summary>
	public void Mostrar(int origen, int destino, Action? alNegroCb)
	{
		alNegro = alNegroCb;
		if (lblTitulo != null)
			lblTitulo.Text = $"🚂 {Ciudades.Nombre(origen)} → {Ciudades.Nombre(destino)}";
		subA = $"Llegaste a {Ciudades.Nombre(destino)}.";
		float mult = Ciudades.Mult(destino);
		subB = mult > 1f ? $"Acá cada laburo paga x{mult}." : "Nuevas calles, misma lucha.";
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		UiPila.PedirPausa(GetTree(), "viaje");
		Node? raiz = GetParent() ?? GetTree()?.CurrentScene;
		hudReal = raiz?.GetNodeOrNull<CanvasLayer>("HUD");
		if (hudReal != null && hudReal.Visible) hudReal.Visible = false;
		else hudReal = null;
		camPrevia = GetViewport()?.GetCamera3D();
		cineCam = new Camera3D { Name = "CineViaje", Fov = 55f };
		(raiz ?? this).AddChild(cineCam);
		cineCam.MakeCurrent();
		tiempo = 0f;
	}

	public override void _Process(double delta)
	{
		if (!Visible || terminada) return;
		tiempo += (float)delta;
		// Re-skin bajo negro total (una sola vez, aunque se salte).
		if (!reskinHecho && tiempo >= TNegro)
		{
			reskinHecho = true;
			try { alNegro?.Invoke(); }
			catch (Exception e) { GD.PushWarning($"[Viaje] re-skin falló: {e.Message}"); }
		}
		if (fade != null)
		{
			var c = fade.Color;
			if (tiempo < TCartel) c.A = Mathf.Clamp(tiempo / TNegro, 0f, 1f);
			else c.A = Mathf.Clamp(1f - (tiempo - TCartel) / 1.5f, 0f, 1f);
			fade.Color = c;
		}
		if (lblTitulo != null) lblTitulo.Visible = tiempo < TCartel + 0.8f;
		// Travelling aéreo en 2 tramos (solo se ve tras el fundido).
		if (cineCam != null && IsInstanceValid(cineCam))
		{
			float t = Mathf.Clamp((tiempo - TCartel) / (TFin - TCartel), 0f, 1f);
			Vector3 pos = t < 0.5f
				? new Vector3(13, 11, 13).Lerp(new Vector3(-9, 8, 9), t * 2f)
				: new Vector3(7, 4.5f, 9).Lerp(new Vector3(-7, 4, -9), (t - 0.5f) * 2f);
			cineCam.GlobalPosition = pos;
			if ((mira - pos).Length() > 0.05f) cineCam.LookAt(mira, Vector3.Up);
		}
		// Subtítulos con typewriter según tramo.
		if (lblSub != null)
		{
			string cual = tiempo < 6.5f ? subA : subB;
			float base_ = tiempo < 6.5f ? TCartel : 6.5f;
			int n = Mathf.Clamp((int)((tiempo - base_) * LetrasPorSeg), 0, cual.Length);
			lblSub.Text = n > 0 ? cual.Substring(0, n) : "";
		}
		if (tiempo >= TFin) Terminar();
		if (salto.Procesar((float)delta)) { Terminar(); return; }
		if (lblSaltar != null)
			lblSaltar.Text = salto.Manteniendo ? $"Saltando {salto.Barra()}" : "Mantené ESPACIO o ESC para saltar";
	}

	/// <summary>Sin click (se dispara sin querer): solo registra mantener.</summary>
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
		if (!reskinHecho)
		{
			reskinHecho = true;
			try { alNegro?.Invoke(); }
			catch (Exception e) { GD.PushWarning($"[Viaje] re-skin falló: {e.Message}"); }
		}
		if (camPrevia != null && IsInstanceValid(camPrevia)) camPrevia.MakeCurrent();
		if (cineCam != null && IsInstanceValid(cineCam)) cineCam.QueueFree();
		if (hudReal != null && IsInstanceValid(hudReal)) hudReal.Visible = true;
		Visible = false;
		if (!pausaSuelta) { UiPila.SoltarPausa(GetTree(), "viaje"); pausaSuelta = true; }
		Terminada?.Invoke();
		QueueFree();
	}
}
