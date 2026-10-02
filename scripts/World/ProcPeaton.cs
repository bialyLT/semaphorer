using Godot;

/// <summary>
/// Fábrica procedural de peatónes low-poly: piernas con pivote en la cadera
/// (para el balanceo al caminar), torso, brazos y cabeza con pivote (para
/// mirar a los lados antes de cruzar). Gorra opcional. Frente = +Z.
/// Sin Blender, sin imports.
/// </summary>
public static class ProcPeaton
{
	/// <summary>Altura de la vereda (caja de 0.4 centrada en 0.1).</summary>
	public const float YVereda = 0.3f;

	/// <summary>
	/// Visual del peatón. Pivotes con nombre fijo (los usa Peaton para animar):
	/// "Cuerpo", "PataIzq", "PataDer", "BrazoIzq", "BrazoDer", "Cabeza".
	/// colorGorra = null → sale del tono de la ropa.
	/// </summary>
	public static Node3D Build(Color ropa, Color pantalon, Color piel, bool gorro, Color? colorGorra = null)
	{
		var raiz = new Node3D { Name = "Visual" };
		var cuerpo = new Node3D { Name = "Cuerpo" };
		raiz.AddChild(cuerpo);

		var matRopa = new StandardMaterial3D { AlbedoColor = ropa, Roughness = 0.9f };
		var matPantalon = new StandardMaterial3D { AlbedoColor = pantalon, Roughness = 0.9f };
		var matPiel = new StandardMaterial3D { AlbedoColor = piel, Roughness = 0.85f };
		var matGorra = new StandardMaterial3D { AlbedoColor = colorGorra ?? ropa.Darkened(0.35f), Roughness = 0.9f };

		// Piernas: pivote en la cadera (y 0.85), la caja cuelga hacia abajo.
		PivoteCaja(cuerpo, "PataIzq", new Vector3(-0.11f, 0.85f, 0),
			new Vector3(0.17f, 0.85f, 0.2f), new Vector3(0, -0.42f, 0), matPantalon);
		PivoteCaja(cuerpo, "PataDer", new Vector3(0.11f, 0.85f, 0),
			new Vector3(0.17f, 0.85f, 0.2f), new Vector3(0, -0.42f, 0), matPantalon);
		// Torso
		Caja(cuerpo, "Torso", new Vector3(0.44f, 0.7f, 0.24f), new Vector3(0, 1.2f, 0), matRopa);
		// Brazos: pivote en el hombro, la caja cuelga.
		PivoteCaja(cuerpo, "BrazoIzq", new Vector3(-0.28f, 1.5f, 0),
			new Vector3(0.13f, 0.6f, 0.15f), new Vector3(0, -0.3f, 0), matRopa);
		PivoteCaja(cuerpo, "BrazoDer", new Vector3(0.28f, 1.5f, 0),
			new Vector3(0.13f, 0.6f, 0.15f), new Vector3(0, -0.3f, 0), matRopa);
		// Cabeza con pivote (gira al mirar la calle)
		var cabeza = new Node3D { Name = "Cabeza", Position = new Vector3(0, 1.62f, 0) };
		cuerpo.AddChild(cabeza);
		var esfera = new MeshInstance3D
		{
			Name = "Coco",
			Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f },
			Position = new Vector3(0, 0.12f, 0)
		};
		esfera.SetSurfaceOverrideMaterial(0, matPiel);
		cabeza.AddChild(esfera);
		if (gorro)
		{
			var g = new MeshInstance3D
			{
				Name = "Gorra",
				Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f },
				Position = new Vector3(0, 0.16f, -0.01f),
				Scale = new Vector3(1f, 0.6f, 1f)
			};
			g.SetSurfaceOverrideMaterial(0, matGorra);
			cabeza.AddChild(g);
			Caja(cabeza, "Visera", new Vector3(0.24f, 0.04f, 0.2f), new Vector3(0, 0.14f, 0.15f), matGorra);
		}
		return raiz;
	}

	static void Caja(Node3D padre, string nombre, Vector3 tam, Vector3 pos, Material mat)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = new BoxMesh { Size = tam }, Position = pos };
		m.SetSurfaceOverrideMaterial(0, mat);
		padre.AddChild(m);
	}

	/// <summary>Pivote (cadera/hombro) con una caja colgando: gira al andar.</summary>
	static void PivoteCaja(Node3D padre, string nombre, Vector3 pos, Vector3 tam, Vector3 centroHijo, Material mat)
	{
		var p = new Node3D { Name = nombre, Position = pos };
		padre.AddChild(p);
		Caja(p, "M", tam, centroHijo, mat);
	}
}
