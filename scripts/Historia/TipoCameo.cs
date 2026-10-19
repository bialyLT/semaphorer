using Godot;
using System;

/// <summary>
/// Cameo del tipo de gala de la intro: tras comprar una mejora, a veces
/// aparece en una vereda cercana, cruza caminando y se pierde a lo lejos.
/// Visual puro (sin colisión ni grupos): no lo ven la policía, el ladrón
/// ni los autos. Se libera solo al alejarse.
/// </summary>
public partial class TipoCameo : Node3D
{
	/// <summary>Se emite cuando llega junto al jugador para ofrecer la tirada.</summary>
	public event Action? OfertaLista;

	Node3D? jugador;
	Node3D? pataIzq, pataDer, brazoIzq, brazoDer;
	Vector3 direccion = Vector3.Forward;
	float velocidad = 1.6f;
	float vidaMax = 22f;
	float tiempo;
	float fase;
	bool listo;
	bool ofrecido;
	bool esperando;

	/// <summary>Arma el visual y el recorrido (llamar tras AddChild).</summary>
	public void Preparar(Node3D? jugadorNuevo, Vector3 posJugador)
	{
		jugador = jugadorNuevo;
		var visual = ProcPeaton.Build(
			new Color(0.08f, 0.08f, 0.1f),
			new Color(0.07f, 0.07f, 0.09f),
			new Color(0.8f, 0.58f, 0.42f),
			false);
		visual.Name = "CameoTipo";
		IntroCinematica.VestirGala(visual);
		AddChild(visual);
		// Aparece en un anillo de 9-12m del jugador, empujado a la vereda.
		float ang = GD.Randf() * Mathf.Tau;
		float radio = 9f + GD.Randf() * 3f;
		Vector3 p = posJugador + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * radio;
		p.X = Mathf.Clamp(p.X, -26f, 26f);
		p.Z = Mathf.Clamp(p.Z, -26f, 26f);
		EmpujarAVereda(ref p);
		GlobalPosition = new Vector3(p.X, AlturaEn(p), p.Z);
		// Cruza pasando junto al jugador y sigue hasta perderse.
		direccion = (posJugador - GlobalPosition);
		direccion.Y = 0;
		if (direccion.LengthSquared() < 0.01f)
			direccion = new Vector3(1, 0, 0);
		direccion = direccion.Normalized();
		Mirar(direccion);
		pataIzq = visual.GetNodeOrNull<Node3D>("Cuerpo/PataIzq");
		pataDer = visual.GetNodeOrNull<Node3D>("Cuerpo/PataDer");
		brazoIzq = visual.GetNodeOrNull<Node3D>("Cuerpo/BrazoIzq");
		brazoDer = visual.GetNodeOrNull<Node3D>("Cuerpo/BrazoDer");
		listo = true;
	}

	/// <summary>Si cae en la calle lo corre a la vereda más cercana.</summary>
	static void EmpujarAVereda(ref Vector3 p)
	{
		if (Mathf.Abs(p.X) < 6f) p.X = (p.X >= 0f ? 1f : -1f) * 6f;
		if (Mathf.Abs(p.Z) < 6f) p.Z = (p.Z >= 0f ? 1f : -1f) * 6f;
	}

	/// <summary>Altura del suelo: vereda 0.3, calle 0.12.</summary>
	static float AlturaEn(Vector3 p) =>
		Mathf.Abs(p.X) < 4.2f || Mathf.Abs(p.Z) < 4.2f ? 0.12f : ProcPeaton.YVereda;

	void Mirar(Vector3 dir)
	{
		var t = GlobalPosition + dir;
		if ((t - GlobalPosition).Length() > 0.05f)
			LookAt(t, Vector3.Up);
	}

	public override void _Process(double delta)
	{
		if (!listo) return;
		float d = (float)delta;
		tiempo += d;
		if (esperando)
		{
			// Frenó junto al jugador: lo mira y espera la tirada.
			if (jugador != null && IsInstanceValid(jugador))
			{
				var j = jugador.GlobalPosition - GlobalPosition;
				j.Y = 0;
				if (j.LengthSquared() > 0.01f) Mirar(j.Normalized());
			}
			Quieto();
		}
		else
		{
			GlobalPosition += direccion * velocidad * d;
			// Suaviza el cordón al pasar de calle a vereda (o al revés).
			var p = GlobalPosition;
			p.Y = Mathf.Lerp(p.Y, AlturaEn(p), Mathf.Min(1f, d * 6f));
			GlobalPosition = p;
			Mirar(direccion);
			// Balanceo al caminar.
			fase += d * 9f;
			float amp = Mathf.Sin(fase) * 0.5f;
			if (pataIzq != null && IsInstanceValid(pataIzq)) pataIzq.Rotation = new Vector3(amp, 0, 0);
			if (pataDer != null && IsInstanceValid(pataDer)) pataDer.Rotation = new Vector3(-amp, 0, 0);
			if (brazoIzq != null && IsInstanceValid(brazoIzq)) brazoIzq.Rotation = new Vector3(-amp * 0.7f, 0, 0);
			if (brazoDer != null && IsInstanceValid(brazoDer)) brazoDer.Rotation = new Vector3(amp * 0.7f, 0, 0);
			RevisarOferta();
		}
		// Se fue lejos o tardó mucho: sale de escena sin ruido.
		var q = GlobalPosition;
		if (tiempo > vidaMax || Mathf.Abs(q.X) > 30f || Mathf.Abs(q.Z) > 30f)
			QueueFree();
	}

	/// <summary>
	/// Si llega a 3.5m del jugador, frena y avisa para ofrecer la tirada
	/// (una sola vez por cameo).
	/// </summary>
	void RevisarOferta()
	{
		if (ofrecido || jugador == null || !IsInstanceValid(jugador)) return;
		var j = jugador.GlobalPosition - GlobalPosition;
		j.Y = 0;
		if (j.Length() > 3.5f) return;
		ofrecido = true;
		esperando = true;
		Quieto();
		OfertaLista?.Invoke();
	}

	/// <summary>Afloja los brazos (parado o esperando).</summary>
	void Quieto()
	{
		if (pataIzq != null && IsInstanceValid(pataIzq)) pataIzq.Rotation = Vector3.Zero;
		if (pataDer != null && IsInstanceValid(pataDer)) pataDer.Rotation = Vector3.Zero;
		if (brazoIzq != null && IsInstanceValid(brazoIzq)) brazoIzq.Rotation = Vector3.Zero;
		if (brazoDer != null && IsInstanceValid(brazoDer)) brazoDer.Rotation = Vector3.Zero;
	}

	/// <summary>La tirada terminó: sigue caminando hasta perderse.</summary>
	public void Despedir() => esperando = false;

	/// <summary>No se pudo ofrecer (tienda/pausa/cine): sigue y reintenta.</summary>
	public void Reintentar()
	{
		ofrecido = false;
		esperando = false;
	}
}
