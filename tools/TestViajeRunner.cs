using Godot;

// Harness headless de cinemáticas (dev-only, no lo usa el juego).
// Lo corre tools/probar_cinematicas.sh en 2 modos:
// sin args = skip con Esc antes del negro · --full = de corrido.
// Imprime líneas TEST que el .sh valida.
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
	}

	public override void _Process(double d)
	{
		frames++;
		// Skip ANTES del negro (frame 30 ~= 0.5s < 1.2s): Terminar debe
		// garantizar el re-skin igual.
		if (!full && frames == 30 && v != null && IsInstanceValid(v))
		{
			GD.Print("TEST pre-skip reskin=", reskin);
			v._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });
			GD.Print("TEST skip enviado, reskin=", reskin);
		}
		bool viva = v != null && IsInstanceValid(v);
		if ((!full && frames >= 60) || (full && (frames > 30000 || !viva)))
		{
			GD.Print("TEST fin frames=", frames, " reskin=", reskin, " viva=", viva);
			GetTree().Quit();
		}
	}
}
