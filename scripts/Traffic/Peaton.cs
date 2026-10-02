using Godot;
using System.Collections.Generic;

/// <summary>
/// Peatón ambiente: recorre un grafo de esquinas que vive TODO sobre las
/// veredas (nunca por la calzada salvo por la senda) y cruza únicamente
/// cuando el semáforo de ese eje está en ROJO. Antes de cruzar para en el
/// pie de la senda y mira a los dos lados; si cae el verde a mitad de
/// camino se apura, y si tarda demasiado se cansa de esperar y da media
/// vuelta. Al terminar la ruta se va y el spawner trae otro.
/// </summary>
public partial class Peaton : Node3D
{
	public enum Estado { Caminando, Mirando, Cruzando }

	// --- Semáforos del cruce: Semaforo/Semaforo2manejan el eje Z (calle NS),
	//     Semaforo3/Semaforo4 el eje X (transversal). Para cruzar hay que ver
	//     los DOS de su eje en rojo.
	public TrafficLight? Semaforo, Semaforo2, Semaforo3, Semaforo4;
	/// <summary>Para mirar la calle: si hay un auto en el corredor, no cruza.</summary>
	public CarSpawner? Trafico;

	public float Velocidad = 1.6f;
	public float VelocidadCruzando = 1.9f;
	/// <summary>Si el verde cae mientras cruza, corre a esta velocidad.</summary>
	public float VelocidadApurado = 3.2f;
	/// <summary>Segundos mirando antes de cruzar (mínimo).</summary>
	public float MirarSeg = 1.2f;
	/// <summary>Cada cuánto cambia la cabeza de lado a lado.</summary>
	public float MirarPeriodoSeg = 2.5f;
	/// <summary>Si espera más que esto, se cansa y vuelve por donde vino.</summary>
	public float PacienciaSeg = 20f;
	/// <summary>Medio ancho del corredor de la senda deemed "con auto".</summary>
	public float MargenAuto = 2.5f;

	/// <summary>Ids de nodos del grafo a seguir (PeatonSpawner los sortea).</summary>
	public string[] Ruta = System.Array.Empty<string>();
	/// <summary>Aspecto (lo sortea el spawner).</summary>
	public Color Ropa = Colors.White, Pantalon = new(0.2f, 0.25f, 0.4f), Piel = new(0.85f, 0.6f, 0.45f);
	public bool Gorro = true;

	public Estado Actual { get; private set; } = Estado.Caminando;

	// --- Geometría de las veredas (ver ProcCalle): la calle mide 8 de ancho,
	//     así que "calzada" es |x|<4 o |z|<4 y la vereda va de 4 a 14.
	const float Borde = 4f;
	/// <summary>Altura de la calzada (caja de 0.22 centrada en 0 + sendas).</summary>
	const float YCalzada = 0.12f;
	const float VeredaCerca = 6f;   // línea de la vereda pegada a la calle
	const float VeredaLejos = 11f;  // fondo de la esquina (donde dan media vuelta)

	public class Nodo
	{
		public string Id = "";
		public Vector3 Pos;
		/// <summary>Pie de senda (borde de la calzada, no vereda lisa).</summary>
		public bool Senda;
		/// <summary>La senda que une este pie cruza la calle NS (se camina en X).</summary>
		public bool CruzaNS;
	}

	/// <summary>Esquinas: A=interior, B/C=finales de cuadra, S1/S2=pies de senda.</summary>
	public static readonly Dictionary<string, Nodo> Grafo = new();
	/// <summary>Nodo -> vecinos por vereda o senda.</summary>
	public static readonly Dictionary<string, string[]> Vecinos = new();

	static Peaton()
	{
		// sx/sz = signo de la esquina. NE(+,+), NO(-,+), SE(+,-), SO(-,-).
		foreach (string q in new[] { "NE", "NO", "SE", "SO" })
		{
			float sx = q == "NE" || q == "SE" ? 1f : -1f;
			float sz = q == "NE" || q == "NO" ? 1f : -1f;
			string A = $"{q}A", B = $"{q}B", C = $"{q}C", S1 = $"{q}S1", S2 = $"{q}S2";
			Agregar(A, sx * VeredaCerca, sz * VeredaCerca, false, false);
			Agregar(B, sx * VeredaCerca, sz * VeredaLejos, false, false);
			Agregar(C, sx * VeredaLejos, sz * VeredaCerca, false, false);
			Agregar(S1, sx * VeredaCerca, sz * 9f, true, true);   // cruza la calle NS
			Agregar(S2, sx * 9f, sz * VeredaCerca, true, false);  // cruza la transversal
			// La vereda son dos "L": A-S1-B (a lo largo de Z) y A-S2-C (a lo largo de X).
			Enlazar(A, S1); Enlazar(S1, B);
			Enlazar(A, S2); Enlazar(S2, C);
		}
		// Sendas: une pies de esquinas opuestas cruzando la calzada.
		Enlazar("NES1", "NOS1");
		Enlazar("SES1", "SOS1");
		Enlazar("NES2", "SES2");
		Enlazar("NOS2", "SOS2");
	}

	static void Agregar(string id, float x, float z, bool senda, bool cruzaNS)
	{
		Grafo[id] = new Nodo
		{
			Id = id,
			Pos = new Vector3(x, ProcPeaton.YVereda, z),
			Senda = senda,
			CruzaNS = cruzaNS
		};
		Vecinos[id] = System.Array.Empty<string>();
	}

	static void Enlazar(string a, string b)
	{
		if (!Vecinos.ContainsKey(a) || !Vecinos.ContainsKey(b)) return;
		var va = new List<string>(Vecinos[a]) { b };
		Vecinos[a] = va.ToArray();
		var vb = new List<string>(Vecinos[b]) { a };
		Vecinos[b] = vb.ToArray();
	}

	public static Nodo? NodoEn(string id) => Grafo.TryGetValue(id, out var n) ? n : null;

	/// <summary>
	/// Ruta al azar: paseo por el grafo sin repetir neighbours ni dar media
	/// vuelta (salvo en callejón sin salida, que es donde dan vuelta).
	/// </summary>
	public static string[] RutaAleatoria(int pasos, RandomNumberGenerator rng)
	{
		var ids = new List<string>(Grafo.Keys);
		string actual = ids[rng.RandiRange(0, ids.Count - 1)];
		string previo = "";
		var ruta = new List<string> { actual };
		for (int i = 0; i < pasos; i++)
		{
			var vecinos = Vecinos[actual];
			if (vecinos.Length == 0) break;
			var opciones = new List<string>();
			foreach (var v in vecinos) if (v != previo) opciones.Add(v);
			if (opciones.Count == 0) opciones.Add(previo); // callejón: vuelve
			string siguiente = opciones[rng.RandiRange(0, opciones.Count - 1)];
			previo = actual;
			actual = siguiente;
			ruta.Add(actual);
		}
		return ruta.ToArray();
	}

	int indice = 0;
	float mirado, espera, camino;
	bool ladoIzq;

	Node3D? cuerpo, pataIzq, pataDer, brazoIzq, brazoDer, cabeza;

	public override void _Ready()
	{
		var visual = ProcPeaton.Build(Ropa, Pantalon, Piel, Gorro);
		AddChild(visual);
		cuerpo = visual.GetNodeOrNull<Node3D>("Cuerpo");
		pataIzq = cuerpo?.GetNodeOrNull<Node3D>("PataIzq");
		pataDer = cuerpo?.GetNodeOrNull<Node3D>("PataDer");
		brazoIzq = cuerpo?.GetNodeOrNull<Node3D>("BrazoIzq");
		brazoDer = cuerpo?.GetNodeOrNull<Node3D>("BrazoDer");
		cabeza = cuerpo?.GetNodeOrNull<Node3D>("Cabeza");
	}

	/// <summary>Que mire de entrada hacia el segundo nodo (lo usa el spawner).</summary>
	public void MirarInstantanea(Vector3 hacia)
	{
		hacia.Y = 0;
		if (hacia.LengthSquared() < 0.0001f) return;
		Rotation = new Vector3(0, Mathf.Atan2(hacia.X, hacia.Z), 0);
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		switch (Actual)
		{
			case Estado.Caminando: Caminar(d); break;
			case Estado.Mirando: Mirar(d); break;
			case Estado.Cruzando: Cruzar(d); break;
		}
		Animar(d);
	}

	void Caminar(float d)
	{
		if (indice >= Ruta.Length) { QueueFree(); return; }
		var destino = NodoEn(Ruta[indice]);
		if (destino == null) { indice++; return; }
		// Si el tramo que arranca acá es una senda: parar en el pie y mirar.
		if (indice > 0 && EsCruce(NodoEn(Ruta[indice - 1]), destino))
		{
			Actual = Estado.Mirando;
			mirado = 0;
			espera = 0;
			return;
		}
		Avanzar(destino.Pos, Velocidad, d);
		if (DistPlano(Plano(), destino.Pos) < 0.25f)
		{
			indice++;
			if (indice >= Ruta.Length) QueueFree();
		}
	}

	void Mirar(float d)
	{
		mirado += d;
		espera += d;
		if (mirado >= MirarPeriodoSeg)
		{
			mirado = 0;
			ladoIzq = !ladoIzq;
		}
		if (mirado >= MirarSeg && PuedeCruzar())
		{
			Actual = Estado.Cruzando;
			return;
		}
		// No espera para siempre: vuelve por donde vino y el spawner trae otro.
		if (espera >= PacienciaSeg && indice > 0)
		{
			Ruta = new[] { Ruta[indice - 1] };
			indice = 0;
			Actual = Estado.Caminando;
		}
	}

	void Cruzar(float d)
	{
		var destino = NodoEn(Ruta[indice]);
		if (destino == null) { Terminar(); return; }
		// Si el verde cae a mitad de camino, sale corriendo.
		float v = EjeEnRojo(destino.CruzaNS) ? VelocidadCruzando : VelocidadApurado;
		Avanzar(destino.Pos, v, d);
		if (DistPlano(Plano(), destino.Pos) < 0.25f) Terminar();
	}

	void Terminar()
	{
		indice++;
		Actual = Estado.Caminando;
		if (indice >= Ruta.Length) QueueFree();
	}

	/// <summary>¿El tramo une dos pies de senda de la misma calzada?</summary>
	static bool EsCruce(Nodo? a, Nodo? b) =>
		a != null && b != null && a.Senda && b.Senda && a.CruzaNS == b.CruzaNS;

	/// <summary>Los dos semáforos del eje que hay que cruzar, en rojo.</summary>
	bool EjeEnRojo(bool cruzaNS)
	{
		if (cruzaNS)
		{
			if (Semaforo == null || Semaforo2 == null) return false;
			return Semaforo.EsRojo() && Semaforo2.EsRojo();
		}
		if (Semaforo3 == null || Semaforo4 == null) return false;
		return Semaforo3.EsRojo() && Semaforo4.EsRojo();
	}

	/// <summary>Rojo del eje + calle despejada: recién ahí pisa la calzada.</summary>
	bool PuedeCruzar()
	{
		var destino = NodoEn(Ruta[indice]);
		if (destino == null || !EjeEnRojo(destino.CruzaNS)) return false;
		// Que el rojo aguante lo que tarda en cruzar: si no, ni entra y
		// espera el siguiente (así no lo agarra el verde en media senda).
		float necesario = DistPlano(Plano(), destino.Pos) / Mathf.Max(0.2f, VelocidadCruzando) + 1.5f;
		if (RojoRestante(destino.CruzaNS) < necesario) return false;
		return Trafico == null || !Trafico.HayAutoEnCorredor(Plano(), destino.Pos, MargenAuto);
	}

	/// <summary>Segundos que le quedan al rojo del eje (0 si no hay luces).</summary>
	float RojoRestante(bool cruzaNS)
	{
		float min = float.MaxValue;
		foreach (var luz in cruzaNS ? new[] { Semaforo, Semaforo2 } : new[] { Semaforo3, Semaforo4 })
		{
			if (luz == null) continue;
			min = Mathf.Min(min, luz.TiempoRestante);
		}
		return min == float.MaxValue ? 0f : min;
	}

	void Avanzar(Vector3 destino, float v, float d)
	{
		Vector3 dir = destino - Plano();
		dir.Y = 0;
		if (dir.LengthSquared() < 0.0001f) return;
		dir = dir.Normalized();
		Position += dir * v * d;
		// Sube/baja del cordón según esté en la vereda o en la calzada.
		float y = EnCalzada() ? YCalzada : ProcPeaton.YVereda;
		Position = new Vector3(Position.X, Mathf.Lerp(Position.Y, y, Mathf.Min(1f, 12f * d)), Position.Z);
		// El frente (+Z local) apunta a la marcha.
		float objetivo = Mathf.Atan2(dir.X, dir.Z);
		Rotation = new Vector3(0, (float)Mathf.LerpAngle(Rotation.Y, objetivo, Mathf.Min(1f, 8f * d)), 0);
		camino += v * d;
	}

	Vector3 Plano() => new(Position.X, 0, Position.Z);
	/// <summary>Distancia en el piso: los nodos están a y=0.3 (vereda).</summary>
	static float DistPlano(Vector3 a, Vector3 b) => new Vector2(b.X - a.X, b.Z - a.Z).Length();
	bool EnCalzada() => Mathf.Abs(Position.X) < Borde || Mathf.Abs(Position.Z) < Borde;

	void Animar(float d)
	{
		bool moviendose = Actual != Estado.Mirando;
		float fase = Mathf.Sin(camino * 3.4f) * (moviendose ? 1f : 0f);
		if (pataIzq != null) pataIzq.Rotation = new Vector3(fase * 0.5f, 0, 0);
		if (pataDer != null) pataDer.Rotation = new Vector3(-fase * 0.5f, 0, 0);
		if (brazoIzq != null) brazoIzq.Rotation = new Vector3(-fase * 0.35f, 0, 0);
		if (brazoDer != null) brazoDer.Rotation = new Vector3(fase * 0.35f, 0, 0);
		if (cuerpo != null) cuerpo.Position = new Vector3(0, Mathf.Abs(fase) * 0.03f, 0);
		if (cabeza == null) return;
		if (Actual == Estado.Mirando)
		{
			// Gira la cabeza de un lado al otro mirando la calle.
			float t = Mathf.PingPong(mirado, MirarPeriodoSeg * 0.5f) / (MirarPeriodoSeg * 0.5f);
			cabeza.RotationDegrees = new Vector3(0, Mathf.Lerp(ladoIzq ? -55f : 55f, ladoIzq ? 55f : -55f, t), 0);
		}
		else
		{
			cabeza.RotationDegrees = Vector3.Zero;
		}
	}
}
