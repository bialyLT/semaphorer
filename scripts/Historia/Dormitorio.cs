using Godot;

/// <summary>
/// Dormitorio procedural low-poly para el despertar del final:
/// piso, 4 paredes, cama, mesita con celular (pantalla emisiva),
/// puerta y ventana. Sin assets, puro BoxMesh (barato en GL Compatibility).
/// Se instancia lejos del cruce (ej. 200,0,200) y la cámara va ahí.
/// </summary>
public static class Dormitorio
{
	const float Ancho = 4.5f;   // X
	const float Largo = 3.6f;   // Z
	const float Alto = 2.6f;
	const float Grosor = 0.15f;

	/// <summary>Altura del colchón (para acostar al protagonista).</summary>
	public const float AltoColchon = 0.62f;

	public static Node3D Build(Vector3 origen)
	{
		var raiz = new Node3D { Name = "Dormitorio", Position = origen };
		var matPiso = Mat(new Color(0.45f, 0.32f, 0.2f));
		var matPared = Mat(new Color(0.82f, 0.8f, 0.75f));
		var matMadera = Mat(new Color(0.35f, 0.24f, 0.15f));
		var matColchon = Mat(new Color(0.9f, 0.88f, 0.84f));
		var matFrazada = Mat(new Color(1, 0.45f, 0.08f));
		var matAlmohada = Mat(new Color(0.95f, 0.93f, 0.88f));
		var matPuerta = Mat(new Color(0.5f, 0.36f, 0.22f));
		var matVidrio = Mat(new Color(0.65f, 0.82f, 0.95f));
		var matCelu = Mat(new Color(0.08f, 0.08f, 0.1f));

		Caja(raiz, "Piso", new Vector3(Ancho, 0.1f, Largo), new Vector3(0, -0.05f, 0), matPiso);
		Caja(raiz, "ParedN", new Vector3(Ancho, Alto, Grosor), new Vector3(0, Alto / 2f, -Largo / 2f), matPared);
		Caja(raiz, "ParedS", new Vector3(Ancho, Alto, Grosor), new Vector3(0, Alto / 2f, Largo / 2f), matPared);
		Caja(raiz, "ParedO", new Vector3(Grosor, Alto, Largo), new Vector3(-Ancho / 2f, Alto / 2f, 0), matPared);
		Caja(raiz, "ParedE", new Vector3(Grosor, Alto, Largo), new Vector3(Ancho / 2f, Alto / 2f, 0), matPared);

		// Cama contra la pared oeste (largo en Z).
		float cx = -Ancho / 2f + 0.6f;
		Caja(raiz, "CamaBase", new Vector3(0.95f, 0.3f, 2.0f), new Vector3(cx, 0.25f, 0), matMadera);
		Caja(raiz, "Colchon", new Vector3(0.9f, 0.22f, 1.95f), new Vector3(cx, 0.51f, 0), matColchon);
		Caja(raiz, "Almohada", new Vector3(0.6f, 0.12f, 0.35f), new Vector3(cx, AltoColchon, -0.75f), matAlmohada);
		Caja(raiz, "Frazada", new Vector3(0.92f, 0.06f, 1.1f), new Vector3(cx, AltoColchon + 0.02f, 0.35f), matFrazada);

		// Mesita + celular al lado de la cama.
		float mx = cx + 0.85f;
		Caja(raiz, "Mesita", new Vector3(0.45f, 0.5f, 0.45f), new Vector3(mx, 0.25f, -0.75f), matMadera);
		Caja(raiz, "Celu", new Vector3(0.09f, 0.02f, 0.18f), new Vector3(mx, 0.52f, -0.75f), matCelu);
		var pantalla = new MeshInstance3D
		{
			Name = "Pantalla",
			Mesh = new BoxMesh { Size = new Vector3(0.075f, 0.005f, 0.15f) },
			Position = new Vector3(mx, 0.532f, -0.75f)
		};
		var matPantalla = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.4f, 0.8f, 1f),
			EmissionEnabled = true,
			Emission = new Color(0.4f, 0.8f, 1f),
			EmissionEnergyMultiplier = 0.4f
		};
		pantalla.SetSurfaceOverrideMaterial(0, matPantalla);
		raiz.AddChild(pantalla);

		// Puerta (plana, pared sur) y ventana (pared norte).
		Caja(raiz, "Puerta", new Vector3(0.9f, 2.0f, 0.06f), new Vector3(1.2f, 1.0f, Largo / 2f - 0.05f), matPuerta);
		Caja(raiz, "VentanaMarco", new Vector3(1.2f, 1.0f, 0.08f), new Vector3(-0.6f, 1.6f, -Largo / 2f + 0.04f), matMadera);
		Caja(raiz, "VentanaVidrio", new Vector3(1.0f, 0.8f, 0.1f), new Vector3(-0.6f, 1.6f, -Largo / 2f + 0.04f), matVidrio);
		return raiz;
	}

	/// <summary>La pantalla se enciende fuerte al hacer la apuesta.</summary>
	public static void SetPantalla(Node? dormitorio, bool encendida)
	{
		var p = dormitorio?.GetNodeOrNull<MeshInstance3D>("Pantalla");
		var m = p?.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
		if (m != null) m.EmissionEnergyMultiplier = encendida ? 2.2f : 0.4f;
	}

	static StandardMaterial3D Mat(Color c) => new() { AlbedoColor = c, Roughness = 0.9f };

	static void Caja(Node3D padre, string nombre, Vector3 tam, Vector3 pos, Material mat)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = new BoxMesh { Size = tam }, Position = pos };
		m.SetSurfaceOverrideMaterial(0, mat);
		padre.AddChild(m);
	}
}
