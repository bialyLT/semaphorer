using Godot;

/// <summary>
/// Bloque C: juguito barato con Tweens. Todo vuelve a reposo solo,
/// escala por importancia y no toca la simulación (solo visual).
/// </summary>
public static class UiJuice
{
	/// <summary>Golpecito de botón: escala 1.15→1.0 con rebote.</summary>
	public static void Punch(Control? c, float pico = 1.15f)
	{
		if (c == null || !GodotObject.IsInstanceValid(c)) return;
		c.PivotOffset = c.Size / 2f;
		c.Scale = Vector2.One * pico;
		var tw = c.CreateTween();
		tw.TweenProperty(c, "scale", Vector2.One, 0.22)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	/// <summary>Negación: sacudida lateral ±8px para "te falta plata".</summary>
	public static void ShakeX(Control? c, float px = 8f)
	{
		if (c == null || !GodotObject.IsInstanceValid(c)) return;
		float x0 = c.Position.X;
		var tw = c.CreateTween();
		tw.TweenProperty(c, "position:x", x0 - px, 0.05);
		tw.TweenProperty(c, "position:x", x0 + px, 0.08);
		tw.TweenProperty(c, "position:x", x0, 0.08)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	/// <summary>Apertura de panel: 0.9→1.0 + aparece (sin snap).</summary>
	public static void PopIn(Control? panel)
	{
		if (panel == null || !GodotObject.IsInstanceValid(panel)) return;
		panel.PivotOffset = panel.Size / 2f;
		panel.Scale = Vector2.One * 0.9f;
		panel.Modulate = new Color(1, 1, 1, 0.4f);
		var tw = panel.CreateTween().SetParallel(true);
		tw.TweenProperty(panel, "scale", Vector2.One, 0.22)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(panel, "modulate:a", 1f, 0.18);
	}
}
