using Godot;

// Harness headless de cinemáticas (dev-only, no lo usa el juego).
// Lo corre tools/probar_cinematicas.sh en 2 modos:
// sin args = skip con HOLD de Esc (presiona en 30, suelta en 140) ·
// --full = de corrido. Imprime líneas TEST que el .sh valida.
public partial class TestViajeRunner : Node
{
	ViajeCinematica? v;
	bool reskin;
	int frames;
	bool full;

	public override void _Ready()
	{
		// Always: si no, la pausa de la cinemática congela también al runner.
		ProcessMode = ProcessModeEnum.Always;
		foreach (var a in OS.GetCmdlineUserArgs())
			if (a == "--full") full = true;
		v = new ViajeCinematica();
		AddChild(v);
		v.Mostrar(1, 2, () => { reskin = true; GD.Print("TEST reskin ejecutado"); });
		GD.Print("TEST mostrar ok, full=", full);
		// Garantía pre-negro: Terminar a t=0 igual corre el re-skin.
		var v2 = new ViajeCinematica();
		AddChild(v2);
		v2.Mostrar(2, 3, () => GD.Print("TEST garantia reskin"));
		v2.Terminar();
	}

	public override void _Process(double d)
	{
		frames++;
		bool viva = v != null && IsInstanceValid(v);
		// Hold real: presiona en 30, suelta en 140 (1.83s > 1.5s).
		// En 60 (0.5s de hold) tiene que SEGUIR viva: un toque no salta.
		if (!full && frames == 30 && viva)
		{
			GD.Print("TEST pre-skip reskin=", reskin);
			v!._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });
		}
		if (!full && frames == 60) GD.Print("TEST sigue viva=", viva);
		if (!full && frames == 140 && viva)
		{
			v!._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = false });
			GD.Print("TEST solto");
		}
		if ((!full && (!viva || frames >= 300)) || (full && (!viva || frames > 30000)))
		{
			GD.Print("TEST fin frames=", frames, " reskin=", reskin, " viva=", viva);
			GetTree().Quit();
		}
	}
}
