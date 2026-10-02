using Godot;

/// <summary>
/// Fábrica procedural del limpiavidrios: mango + barra + goma (+ esponja
/// en skins pro). Skins según la esponja: "basico" (madera), "rojo" (Nv1+),
/// "dorado" (Nv3). El balde va aparte y sube de nivel por su cuenta
/// (BuildBalde): Nv1 plástico, Nv2 acero, Nv3 oro. El pivote va en el
/// agarre, la cabeza hacia +Z.
/// </summary>
public static class Limpiavidrios
{
	public static Node3D Build(string skin)
	{
		var root = new Node3D { Name = $"Limpiavidrios_{skin}" };

		Color mango = new(0.5f, 0.35f, 0.2f);
		float metal = 0f;
		bool conEsponja = false;
		Color colEsponja = new(0.9f, 0.8f, 0.2f);
		switch (skin)
		{
			case "dorado":
				mango = new Color(0.9f, 0.7f, 0.2f);
				metal = 0.6f;
				conEsponja = true;
				break;
			case "rojo":
				mango = new Color(0.8f, 0.15f, 0.15f);
				conEsponja = true;
				colEsponja = new Color(0.7f, 0.7f, 0.72f);
				break;
		}

		Parte(root, "Mango", new BoxMesh { Size = new Vector3(0.07f, 0.07f, 0.5f) },
			new Vector3(0, 0, 0.05f),
			new StandardMaterial3D { AlbedoColor = mango, Roughness = 0.5f, Metallic = metal });
		// Cabezal: barra + goma abajo (+ esponja arriba en skins pro)
		Parte(root, "Barra", new BoxMesh { Size = new Vector3(0.4f, 0.06f, 0.07f) },
			new Vector3(0, 0, 0.32f),
			new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.17f), Roughness = 0.5f });
		Parte(root, "Goma", new BoxMesh { Size = new Vector3(0.38f, 0.14f, 0.025f) },
			new Vector3(0, -0.08f, 0.33f),
			new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f), Roughness = 0.9f });
		if (conEsponja)
			Parte(root, "Esponja", new BoxMesh { Size = new Vector3(0.34f, 0.09f, 0.12f) },
				new Vector3(0, 0.07f, 0.32f),
				new StandardMaterial3D { AlbedoColor = colEsponja, Roughness = 0.95f });
		return root;
	}

	static void Parte(Node3D root, string nombre, Mesh mesh, Vector3 pos, Material mat)
	{
		var m = new MeshInstance3D { Name = nombre, Mesh = mesh, Position = pos };
		m.SetSurfaceOverrideMaterial(0, mat);
		root.AddChild(m);
	}

	/// <summary>
	/// Balde en la otra mano: aparece al comprar la mejora y sube de
	/// nivel con ella (Nv1 plástico azul, Nv2 acero, Nv3 oro).
	/// Devuelve null si todavía no se compró.
	/// </summary>
	public static Node3D? BuildBalde(int nv)
	{
		if (nv <= 0) return null;
		var root = new Node3D { Name = $"Balde_{nv}" };

		Color cuerpo;
		float metal;
		switch (nv)
		{
			case 1:
				cuerpo = new Color(0.15f, 0.35f, 0.75f);
				metal = 0f;
				break;
			case 2:
				cuerpo = new Color(0.6f, 0.62f, 0.65f);
				metal = 0.85f;
				break;
			default:
				cuerpo = new Color(0.9f, 0.72f, 0.2f);
				metal = 0.95f;
				break;
		}
		Parte(root, "Cuerpo", new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.10f, Height = 0.22f },
			Vector3.Zero,
			new StandardMaterial3D { AlbedoColor = cuerpo, Metallic = metal, Roughness = metal > 0 ? 0.3f : 0.6f });
		Parte(root, "Borde", new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.14f, Height = 0.03f },
			new Vector3(0, 0.11f, 0),
			new StandardMaterial3D { AlbedoColor = cuerpo.Darkened(0.25f), Metallic = metal, Roughness = 0.35f });
		Parte(root, "Agua", new CylinderMesh { TopRadius = 0.115f, BottomRadius = 0.115f, Height = 0.02f },
			new Vector3(0, 0.06f, 0),
			new StandardMaterial3D
			{
				AlbedoColor = new Color(0.35f, 0.6f, 0.85f, 0.75f),
				Roughness = 0.15f,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha
			});
		return root;
	}
}
