using Godot;
using System;

/// <summary>
/// Diálogos livianos: panel inferior con nombre + texto typewriter.
/// Avanza con E/click/Espacio/Enter. Esc NO lo maneja (es del orquestador,
/// que lo usa como skip global). Emite Terminada al agotar las líneas.
/// No pide pausa: la pide quien lo orquesta.
/// </summary>
public partial class DialogoUI : CanvasLayer
{
	public event Action? Terminada;

	/// <summary>Línea en pantalla (para que el orquestador dispare cosas
	/// a mitad del diálogo, ej. encender el celular al apostar).</summary>
	public int LineaActual => idx;
	public int TotalLineas => lineas.Length;

	(string nombre, string texto)[] lineas = Array.Empty<(string, string)>();
	int idx;
	int letras;
	float acum;
	const float LetrasPorSeg = 40f;

	PanelContainer? panel;
	Label? lblNombre;
	Label? lblTexto;
	Label? lblHint;

	public override void _Ready()
	{
		Layer = 96;
		ProcessMode = ProcessModeEnum.Always;
		ConstruirUI();
		Visible = false;
	}

	void ConstruirUI()
	{
		panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBase());
		panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		panel.OffsetLeft = 140; panel.OffsetRight = -140;
		panel.OffsetTop = -230; panel.OffsetBottom = -100;
		AddChild(panel);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 6);
		panel.AddChild(caja);

		lblNombre = new Label();
		lblNombre.AddThemeFontSizeOverride("font_size", 18);
		lblNombre.AddThemeColorOverride("font_color", UiTheme.NaranjaClaro);
		caja.AddChild(lblNombre);

		lblTexto = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		lblTexto.AddThemeFontSizeOverride("font_size", 20);
		lblTexto.AddThemeColorOverride("font_color", UiTheme.Texto);
		lblTexto.CustomMinimumSize = new Vector2(0, 60);
		caja.AddChild(lblTexto);

		lblHint = new Label { Text = "E / click ▸" };
		lblHint.AddThemeFontSizeOverride("font_size", 13);
		lblHint.AddThemeColorOverride("font_color", UiTheme.Gris);
		lblHint.HorizontalAlignment = HorizontalAlignment.Right;
		caja.AddChild(lblHint);
	}

	public void MostrarLineas((string nombre, string texto)[] nuevas)
	{
		lineas = nuevas ?? Array.Empty<(string, string)>();
		idx = 0;
		letras = 0;
		acum = 0f;
		Visible = true;
		Pintar();
	}

	public override void _Process(double delta)
	{
		if (!Visible || idx >= lineas.Length) return;
		string texto = lineas[idx].texto;
		if (letras < texto.Length)
		{
			acum += (float)delta * LetrasPorSeg;
			int n = Mathf.Min(texto.Length, (int)acum);
			if (n != letras)
			{
				letras = n;
				Pintar();
			}
		}
	}

	void Pintar()
	{
		if (idx >= lineas.Length) return;
		if (lblNombre != null) lblNombre.Text = lineas[idx].nombre;
		if (lblTexto != null) lblTexto.Text = lineas[idx].texto.Substring(0, letras);
	}

	void Avanzar()
	{
		if (!Visible || idx >= lineas.Length) return;
		// Primer toque completa la línea; el segundo pasa a la siguiente.
		if (letras < lineas[idx].texto.Length)
		{
			letras = lineas[idx].texto.Length;
			Pintar();
			return;
		}
		idx++;
		letras = 0;
		acum = 0f;
		if (idx >= lineas.Length)
		{
			Visible = false;
			Terminada?.Invoke();
			return;
		}
		Pintar();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is InputEventMouseButton mb && mb.Pressed &&
			(mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
		{
			Avanzar();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
		if (k.PhysicalKeycode == Key.E || k.PhysicalKeycode == Key.Space ||
			k.PhysicalKeycode == Key.Enter || k.PhysicalKeycode == Key.KpEnter)
		{
			Avanzar();
			GetViewport().SetInputAsHandled();
		}
	}
}
