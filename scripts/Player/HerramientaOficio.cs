using Godot;

/// <summary>
/// Fase 3: herramientas visibles de malabarista y vendedor (el
/// limpiavidrios sigue en Limpiavidrios.cs). Todo procedural, mismo estilo.
/// </summary>
public static class HerramientaOficio
{
	static void Parte(Node3D root, string nombre, Mesh mesh, Vector3 pos, Material mat)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = mesh, Position = pos };
		m.SetSurfaceOverrideMaterial(0, mat);
		root.AddChild(m);
	}

	static StandardMaterial3D Mat(Color c, float rough = 0.7f) =>
		new() { AlbedoColor = c, Roughness = rough };

	/// <summary>3 pelotas de tenis en la mano (nv 2+: colores más vivos).</summary>
	public static Node3D BuildPelotas(int nv)
	{
		var root = new Node3D { Name = $"Pelotas_{nv}" };
		Color[] cols = nv >= 2
			? new[] { new Color(0.75f, 1f, 0.1f), new Color(1f, 0.25f, 0.2f), new Color(0.2f, 0.6f, 1f) }
			: new[] { new Color(0.6f, 0.75f, 0.3f), new Color(0.75f, 0.6f, 0.35f), new Color(0.55f, 0.65f, 0.5f) };
		for (int i = 0; i < 3; i++)
			Parte(root, $"Pelota{i}", new SphereMesh { Radius = 0.09f, Height = 0.18f },
				new Vector3((i - 1) * 0.16f, (i % 2) * 0.08f, 0), Mat(cols[i], 0.9f));
		return root;
	}

	/// <summary>Palo al hombro con atados: limones (amarillo) y morrones (rojo/verde).</summary>
	public static Node3D BuildPalo()
	{
		var root = new Node3D { Name = "Palo" };
		Parte(root, "Palo", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 1.3f },
			new Vector3(0, 0.3f, 0), Mat(new Color(0.5f, 0.35f, 0.2f), 0.85f));
		// Atado de limones (adelante) y de morrones (atrás).
		for (int i = 0; i < 3; i++)
			Parte(root, $"Limon{i}", new SphereMesh { Radius = 0.06f, Height = 0.12f },
				new Vector3(-0.08f + i * 0.08f, 0.85f, 0.02f), Mat(new Color(0.95f, 0.85f, 0.2f), 0.8f));
		Parte(root, "MorronR", new BoxMesh { Size = new Vector3(0.09f, 0.12f, 0.09f) },
			new Vector3(-0.06f, -0.25f, 0), Mat(new Color(0.85f, 0.15f, 0.12f), 0.8f));
		Parte(root, "MorronV", new BoxMesh { Size = new Vector3(0.09f, 0.12f, 0.09f) },
			new Vector3(0.06f, -0.25f, 0), Mat(new Color(0.2f, 0.6f, 0.25f), 0.8f));
		return root;
	}

	/// <summary>Canasta en la otra mano (null si no comprada). Sube con su nivel.</summary>
	public static Node3D? BuildCanasta(int nv)
	{
		if (nv <= 0) return null;
		var root = new Node3D { Name = $"Canasta_{nv}" };
		Color mimbre = nv >= 2 ? new Color(0.65f, 0.48f, 0.25f) : new Color(0.5f, 0.38f, 0.22f);
		Parte(root, "Cuerpo", new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.12f, Height = 0.18f },
			Vector3.Zero, Mat(mimbre, 0.9f));
		Parte(root, "Limon", new SphereMesh { Radius = 0.06f, Height = 0.12f },
			new Vector3(-0.05f, 0.1f, 0), Mat(new Color(0.95f, 0.85f, 0.2f), 0.8f));
		Parte(root, "Morron", new BoxMesh { Size = new Vector3(0.09f, 0.11f, 0.09f) },
			new Vector3(0.06f, 0.1f, 0), Mat(new Color(0.85f, 0.15f, 0.12f), 0.8f));
		return root;
	}
}
