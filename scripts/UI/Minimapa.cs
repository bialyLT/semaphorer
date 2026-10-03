using Godot;

/// <summary>
/// Minimapa del cruce (esquina superior izquierda del HUD): calles en cruz,
/// tu posición con dirección y los 4 semáforos en su carril. Los colores de
/// los semáforos (verde el que da paso, rojo el resto) solo se ven con la
/// mejora "mapa" comprada; sin ella salen grises. Todo por código.
/// </summary>
public partial class Minimapa : Control
{
	/// <summary>Posición de cada semáforo en su carril (línea de detención).</summary>
	static readonly Vector2[] PosLuces = {
		new(-2, -14), // carril 1
		new(2, 14),   // carril 2
		new(-14, 2),  // carril 3
		new(14, -2),   // carril 4
	};

	const float AlcanceM = 30f;
	const float LadoPx = 180f;

	Node3D? jugador;
	readonly TrafficLight?[] luces = new TrafficLight?[4];
	Mejoras? tienda;
	double acum;

	public void Inicializar(Node3D? jugadorNuevo, TrafficLight?[] lucesNuevas, Mejoras? tiendaNueva)
	{
		jugador = jugadorNuevo;
		for (int i = 0; i < 4 && i < lucesNuevas.Length; i++) luces[i] = lucesNuevas[i];
		tienda = tiendaNueva;
		QueueRedraw();
	}

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(LadoPx, LadoPx);
		Size = new Vector2(LadoPx, LadoPx);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		// 10Hz alcanza: el jugador y las luces no necesitan 60fps acá.
		acum += delta;
		if (acum < 0.1) return;
		acum = 0;
		QueueRedraw();
	}

	bool MapaActivado() => tienda != null && tienda.NivelDe("mapa") > 0;

	Vector2 AMapa(Vector2 mundo)
	{
		float escala = (LadoPx - 20f) / (AlcanceM * 2f);
		return new Vector2(LadoPx / 2f + mundo.X * escala, LadoPx / 2f + mundo.Y * escala);
	}

	public override void _Draw()
	{
		var lado = new Rect2(Vector2.Zero, new Vector2(LadoPx, LadoPx));
		DrawRect(lado, new Color(0.05f, 0.06f, 0.09f, 0.85f), true);
		// Calles en cruz (8m de ancho, como en el mundo).
		var calle = new Color(0.32f, 0.32f, 0.34f);
		DrawRect(new Rect2(AMapa(new Vector2(-4, -30)), AMapa(new Vector2(4, 30)) - AMapa(new Vector2(-4, -30))), calle, true);
		DrawRect(new Rect2(AMapa(new Vector2(-30, -4)), AMapa(new Vector2(30, 4)) - AMapa(new Vector2(-30, -4))), calle, true);
		DrawRect(lado, new Color(1, 0.65f, 0.15f, 0.9f), false, 2f);
		// Semáforos: grises sin la mejora, verde/rojo con ella.
		bool activo = MapaActivado();
		for (int i = 0; i < 4; i++)
		{
			var luz = luces[i];
			Color c = new(0.45f, 0.45f, 0.45f);
			if (activo && luz != null && IsInstanceValid(luz))
				c = luz.Estado == "VERDE" ? new Color(0.3f, 1, 0.35f) : new Color(1, 0.3f, 0.25f);
			Vector2 p = AMapa(PosLuces[i]);
			DrawCircle(p, 5f, new Color(0, 0, 0, 0.9f));
			DrawCircle(p, 4f, c);
		}
		// Jugador: punto naranja con tick de dirección.
		if (jugador != null && IsInstanceValid(jugador))
		{
			var jp = jugador.GlobalPosition;
			Vector2 p = AMapa(new Vector2(jp.X, jp.Z));
			DrawCircle(p, 4.5f, Colors.White);
			DrawCircle(p, 3f, new Color(1, 0.55f, 0.1f));
			var f = -jugador.GlobalTransform.Basis.Z;
			var d = new Vector2(f.X, f.Z);
			if (d.LengthSquared() > 0.001f) DrawLine(p, p + d.Normalized() * 8f, Colors.White, 2f);
		}
	}
}
