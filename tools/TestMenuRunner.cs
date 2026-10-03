using Godot;

// Harness headless del modal de borrado (dev-only, no borra nada):
// abre con X, cancela con botón, reabre y cancela con Esc.
public partial class TestMenuRunner : Node
{
	Menu? menu;
	int frames;
	int fase;

	static Button? BuscarBoton(Node n, string texto)
	{
		if (n is Button b && b.Text == texto) return b;
		foreach (var h in n.GetChildren())
		{
			var r = BuscarBoton(h, texto);
			if (r != null) return r;
		}
		return null;
	}

	static InputEventKey Tecla(Key k) => new() { PhysicalKeycode = k, Pressed = true };

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		var escena = GD.Load<PackedScene>("res://scenes/Menu.tscn");
		menu = (Menu?)escena.Instantiate();
		AddChild(menu);
		GD.Print("TEST-MENU menu listo");
	}

	public override void _Process(double _d)
	{
		frames++;
		if (menu == null || !IsInstanceValid(menu) || frames < 10) return;
		if (fase == 0)
		{
			fase = 1;
			var x = BuscarBoton(menu, "X");
			if (x == null) { GD.Print("TEST-MENU omitido (sin partidas)"); GetTree().Quit(); return; }
			x.EmitSignal(Button.SignalName.Pressed);
			var modal = menu.GetNodeOrNull<ColorRect>("ModalBorrarFondo");
			GD.Print("TEST-MENU abre=", modal != null && modal.Visible);
		}
		else if (fase == 1 && frames >= 20)
		{
			fase = 2;
			var c = BuscarBoton(menu, "Cancelar");
			if (c == null) { GD.Print("TEST-MENU FALLA sin-cancelar"); GetTree().Quit(); return; }
			c.EmitSignal(Button.SignalName.Pressed);
			var modal = menu.GetNodeOrNull<ColorRect>("ModalBorrarFondo");
			GD.Print("TEST-MENU cancela=", modal != null && !modal.Visible);
		}
		else if (fase == 2 && frames >= 30)
		{
			fase = 3;
			var x = BuscarBoton(menu, "X");
			x?.EmitSignal(Button.SignalName.Pressed);
			menu._Input(Tecla(Key.Escape));
			var modal = menu.GetNodeOrNull<ColorRect>("ModalBorrarFondo");
			GD.Print("TEST-MENU esc=", modal != null && !modal.Visible);
			GetTree().Quit();
		}
	}
}
