﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using MUNIA.Controllers;
using MUNIA.Util;
using OpenTK.Graphics.OpenGL;
using Svg;

namespace MUNIA.Skins {
	public class SvgSkin : Skin {
		private SvgDocument _svgDocument;
		public List<Button> Buttons = new List<Button>();
		public List<Stick> Sticks = new List<Stick>();
		public List<Trigger> Triggers = new List<Trigger>();
		private SizeF _dimensions;
		private int _baseTexture;

		public void Load(string svgPath) {
			try {
				Path = svgPath;
				_svgDocument = SvgDocument.Open(svgPath);
				_dimensions = _svgDocument.GetDimensions();

				// cleanup
				Buttons.ForEach(b => {
					if (b.PressedTexture != -1) GL.DeleteTexture(b.PressedTexture);
					if (b.Texture != -1) GL.DeleteTexture(b.Texture);
				});
				Sticks.ForEach(s => {
					if (s.Texture != -1) GL.DeleteTexture(s.Texture);
					if (s.PressedTexture != -1) GL.DeleteTexture(s.PressedTexture);
				});
				Triggers.ForEach(t => { if (t.Texture != -1) GL.DeleteTexture(t.Texture); });
				Buttons.Clear();
				Sticks.Clear();
				Triggers.Clear();

				// load button/stick/trigger mapping from svg
				RecursiveGetElements(_svgDocument);
				LoadResult = Controllers.Any() ? SkinLoadResult.Ok : SkinLoadResult.Fail;
			}
			catch { LoadResult = SkinLoadResult.Fail; }
		}

		private void RecursiveGetElements(SvgElement e) {
			foreach (var c in e.Children) {
				if (c.ElementName == "info") {
					if (c.CustomAttributes.ContainsKey("device-name")) {
						string devName = c.CustomAttributes["device-name"];
						// match on name for compatibility reasons
						if (devName == "NinHID SNES") Controllers.Add(ControllerType.SNES);
						else if (devName == "NinHID N64") Controllers.Add(ControllerType.N64);
						else if (devName == "NinHID NGC") Controllers.Add(ControllerType.NGC);
					}

					if (c.CustomAttributes.ContainsKey("device-type")) {
						string devType = c.CustomAttributes["device-type"];
						ControllerType type;
						if (Enum.TryParse(devType, true, out type))
							Controllers.Add(type);
					}

					Name = c.CustomAttributes["skin-name"];
				}

				if (c.ContainsAttribute("stick-id")) {
					var s = c as SvgVisualElement;
					var stick = new Stick {
						Id = int.Parse(c.CustomAttributes["stick-id"]),
						HorizontalAxis = int.Parse(c.CustomAttributes["axis-h"]),
						VerticalAxis = int.Parse(c.CustomAttributes["axis-v"]),
						OffsetScale = float.Parse(c.CustomAttributes["offset-scale"], CultureInfo.InvariantCulture),
						Z = c.ContainsAttribute("z-index") ? int.Parse(c.CustomAttributes["z-index"]) : 1,
						ButtonId = c.ContainsAttribute("button-id") ? int.Parse(c.CustomAttributes["button-id"]) : -1,
					};

					bool pressed = stick.ButtonId != -1 && c.ContainsAttribute("button-state")
						&& c.CustomAttributes["button-state"] == "pressed";
					if (pressed) stick.Pressed = s;
					else stick.Element = s;

					stick.Deadzone = c.ContainsAttribute("deadzone") ? int.Parse(c.CustomAttributes["deadzone"]) : 0;

					Sticks.Add(stick);
				}

				else if (c.ContainsAttribute("button-id")) {
					var b = c as SvgVisualElement;
					bool pressed = c.CustomAttributes["button-state"] == "pressed";
					var button = new Button { Id = int.Parse(c.CustomAttributes["button-id"]) };

					if (c.ContainsAttribute("z-index"))
						button.Z = int.Parse(c.CustomAttributes["z-index"]);

					if (pressed)
						button.Pressed = b;
					else
						button.Element = b;
					Buttons.Add(button);
				}

				else if (c.ContainsAttribute("trigger-id")) {
					var t = c as SvgVisualElement;

					var trigger = new Trigger { Id = int.Parse(c.CustomAttributes["trigger-id"]) };
					trigger.Element = t;
					trigger.Axis = int.Parse(c.CustomAttributes["trigger-axis"]);

					if (c.ContainsAttribute("offset-scale"))
						trigger.OffsetScale = float.Parse(c.CustomAttributes["offset-scale"], CultureInfo.InvariantCulture);

					if (c.ContainsAttribute("z-index"))
						trigger.Z = int.Parse(c.CustomAttributes["z-index"]);

					if (c.ContainsAttribute("trigger-orientation"))
						trigger.Orientation = (TriggerOrientation)Enum.Parse(typeof(TriggerOrientation),
							c.CustomAttributes["trigger-orientation"], true);

					if (c.ContainsAttribute("trigger-type"))
						trigger.Type = (TriggerType)Enum.Parse(typeof(TriggerType),
							c.CustomAttributes["trigger-type"], true);

					if (c.ContainsAttribute("trigger-range"))
						trigger.Range = Range.Parse(c.CustomAttributes["trigger-range"]);

					if (c.ContainsAttribute("trigger-reverse"))
						trigger.Reverse = IniFile.IniSection.TrueValues.Contains(c.CustomAttributes["trigger-reverse"].ToLowerInvariant());

					if (c.ContainsAttribute("trigger-inverse"))
						trigger.Inverse = IniFile.IniSection.TrueValues.Contains(c.CustomAttributes["trigger-inverse"].ToLowerInvariant());

					Triggers.Add(trigger);
				}
				RecursiveGetElements(c);
			}
		}

		public override void Render(int width, int height) {
			if (_svgDocument == null || width == 0 || height == 0) return;
			if (_svgDocument.Height != height || _svgDocument.Width != width) {
				RenderBase(width, height);
			}
			Render();
		}

		public void Render() {
			List<ControllerItem> all = new List<ControllerItem>();
			all.AddRange(Buttons);
			all.AddRange(Sticks);
			all.AddRange(Triggers);
			all.Sort((l, r) => l.Z.CompareTo(r.Z));

			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Texture2D);

			int i, cut = all.FindIndex(item => item.Z >= 0);
			for (i = 0; i < cut; i++)
				RenderItem(all[i]);

			GL.BindTexture(TextureTarget.Texture2D, _baseTexture);
			TextureHelper.RenderTexture(0, _svgDocument.Width, 0, _svgDocument.Height);

			for (; i < all.Count; i++)
				RenderItem(all[i]);

			GL.Disable(EnableCap.Blend);
		}

		private void RenderItem(ControllerItem i) {
			if (i is Button b) RenderButton(b);
			if (i is Stick s) RenderStick(s);
			if (i is Trigger t) RenderTrigger(t);
		}
		private void RenderButton(Button btn) {
			bool pressed = State != null && State.Buttons[btn.Id];
			if (pressed && btn.Pressed != null) {
				GL.BindTexture(TextureTarget.Texture2D, btn.PressedTexture);
				TextureHelper.RenderTexture(btn.PressedBounds);
			}
			else if (!pressed && btn.Element != null) {
				GL.BindTexture(TextureTarget.Texture2D, btn.Texture);
				TextureHelper.RenderTexture(btn.Bounds);
			}
		}
		private void RenderStick(Stick stick) {
			bool pressed = State != null && stick.PressedTexture != -1 && stick.ButtonId != -1 && State.Buttons[stick.ButtonId];
			var r = pressed ? stick.PressedBounds : stick.Bounds;
			float x, y;

			if (State != null) {
				x = State.Axes[stick.HorizontalAxis];
				if (Math.Abs(x) < stick.Deadzone) x = 0;
				y = State.Axes[stick.VerticalAxis];
				if (Math.Abs(y) < stick.Deadzone) y = 0;
			}
			else {
				x = y = 0f;
			}

			SizeF img = GetCorrectedDimensions(new SizeF(_svgDocument.Width, _svgDocument.Height));
			x *= img.Width / _dimensions.Width * stick.OffsetScale;
			y *= img.Height / _dimensions.Height * stick.OffsetScale;
			r.Offset(new PointF(x, y));

			int texture = pressed && stick.PressedTexture != -1 ? stick.PressedTexture : stick.Texture;
			GL.BindTexture(TextureTarget.Texture2D, texture);
			TextureHelper.RenderTexture(r);
		}

		private void RenderTrigger(Trigger trigger) {
			var r = trigger.Bounds;
			float o = State?.Axes[trigger.Axis] ?? 0f;
			o = trigger.Range.Clip(o);

			if (trigger.Type == TriggerType.Slide) {
				SizeF img = GetCorrectedDimensions(new SizeF(_svgDocument.Width, _svgDocument.Height));
				if (trigger.Orientation == TriggerOrientation.Vertical) {
					o *= img.Height / _dimensions.Height * trigger.OffsetScale;
					r.Offset(new PointF(0, o));
				}
				else {
					o *= img.Width / _dimensions.Width * trigger.OffsetScale;
					r.Offset(new PointF(o, 0));
				}
				GL.BindTexture(TextureTarget.Texture2D, trigger.Texture);
				TextureHelper.RenderTexture(r);
			}

			else if (trigger.Type == TriggerType.Bar) {
				RectangleF crop = new RectangleF(PointF.Empty, new SizeF(1.0f, 1.0f));
				float pressRate = (o - trigger.Range.LowerBound) / (trigger.Range.UpperBound - trigger.Range.LowerBound);
				if (trigger.Inverse) pressRate = 1.0f - pressRate;

				if (trigger.Orientation == TriggerOrientation.Horizontal) {
					crop.X = trigger.Reverse ^ trigger.Inverse ? 1.0f - pressRate : 0;
					crop.Width = pressRate;
				}
				else {
					crop.Y = trigger.Reverse ^ trigger.Inverse ? 1.0f - pressRate : 0;
					crop.Height = pressRate;
				}

				// compensate texture size for crop rate
				r.X += r.Width * crop.X;
				r.Y += r.Height * crop.Y;
				r.Width *= crop.Width;
				r.Height *= crop.Height;

				GL.BindTexture(TextureTarget.Texture2D, trigger.Texture);
				TextureHelper.RenderTexture(crop, r);
			}
		}

		private void RenderBase(int width, int height) {
			_svgDocument.Height = height;
			_svgDocument.Width = width;

			// hide just the changable elements first
			SetVisibleRecursive(_svgDocument, true);
			foreach (var b in Buttons) {
				if (b.Pressed != null) b.Pressed.Visible = false;
				if (b.Element != null) b.Element.Visible = false;
			}
			foreach (var s in Sticks) {
				if (s.Element != null) s.Element.Visible = false;
				if (s.Pressed != null) s.Pressed.Visible = false;
			}
			foreach (var t in Triggers) {
				t.Element.Visible = false;
			}

			var baseImg = _svgDocument.Draw();
			_baseTexture = TextureHelper.CreateTexture(baseImg);

			// System.IO.Directory.CreateDirectory(Name);
			// baseImg.Save(Name + "/base_texture.png");

			// hide everything
			SetVisibleRecursive(_svgDocument, false);
			var work = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			foreach (var btn in Buttons) {
				if (btn.Pressed != null) {
					var tb = SvgElementToTexture(work, btn.Pressed);
					btn.PressedTexture = tb.Item1;
					btn.PressedBounds = tb.Item2;
				}
				if (btn.Element != null) {
					var tb = SvgElementToTexture(work, btn.Element);
					btn.Texture = tb.Item1;
					btn.Bounds = tb.Item2;
				}
			}

			foreach (var trig in Triggers) {
				if (trig.Element != null) {
					var tb = SvgElementToTexture(work, trig.Element);
					trig.Texture = tb.Item1;
					trig.Bounds = tb.Item2;
				}
			}

			foreach (var stick in Sticks) {
				if (stick.Element != null) {
					var tb = SvgElementToTexture(work, stick.Element);
					stick.Texture = tb.Item1;
					stick.Bounds = tb.Item2;
				}
				if (stick.Pressed != null) {
					var tb = SvgElementToTexture(work, stick.Pressed);
					stick.PressedTexture = tb.Item1;
					stick.PressedBounds = tb.Item2;
				}
			}
		}

		private Tuple<int, RectangleF> SvgElementToTexture(Bitmap work, SvgVisualElement e) {
			var bounds = CalcBounds(e);
			var l = Unproject(bounds.Location);
			var s = Unproject(new PointF(bounds.Right, bounds.Bottom));
			var boundsScaled = RectangleF.FromLTRB(l.X, l.Y, s.X, s.Y);
			boundsScaled.Inflate(10f, 10f); // small margin for rounding error
			boundsScaled.Intersect(new RectangleF(0f, 0f, work.Width, work.Height));
			int ret = -1;
			if (!boundsScaled.IsEmpty) {
				// unhide temporarily
				SetVisibleToRoot(e, true);
				SetVisibleRecursive(e, true);

				_svgDocument.Draw(work);
				// work.Clone(boundsScaled, work.PixelFormat).Save(Name + "/" + e.ID + ".png");

				ret = TextureHelper.CreateTexture(work.Clone(boundsScaled, work.PixelFormat));
				using (Graphics g = Graphics.FromImage(work))
					g.Clear(Color.Transparent);

				SetVisibleRecursive(e, false);
				SetVisibleToRoot(e, false);
			}
			return Tuple.Create(ret, boundsScaled);
		}

		private void SetVisibleRecursive(SvgElement e, bool visible) {
			if (e is SvgVisualElement) {
				((SvgVisualElement)e).Visible = visible;
			}
			foreach (var c in e.Children)
				SetVisibleRecursive(c, visible);
		}

		private void SetVisibleToRoot(SvgElement e, bool visible) {
			if (e is SvgVisualElement)
				((SvgVisualElement)e).Visible = visible;
			if (e.Parent != null)
				SetVisibleToRoot(e.Parent, visible);
		}

		private PointF Project(PointF p, SizeF dim) {
			float svgAR = _dimensions.Width / _dimensions.Height;
			float imgAR = dim.Width / dim.Height;
			if (svgAR > imgAR) {
				// compensate for black box
				p.Y -= ((dim.Height - dim.Width / svgAR) / 2f);
				// adjust ratio
				dim.Height = dim.Width / svgAR;
			}
			else {
				// compensate for black box
				p.X -= ((dim.Width - dim.Height * svgAR) / 2f);
				// adjust ratio
				dim.Width = dim.Height * svgAR;
			}

			var x = p.X / dim.Width * _dimensions.Width;
			var y = p.Y / dim.Height * _dimensions.Height;
			return new PointF(x, y);
		}

		private SizeF GetCorrectedDimensions(SizeF dim) {
			// find real width/height, compensating for black box
			float svgAR = _dimensions.Width / _dimensions.Height;
			float imgAR = dim.Width / dim.Height;
			if (svgAR > imgAR)
				dim.Height = dim.Width / svgAR;
			else
				dim.Width = dim.Height * svgAR;
			return dim;
		}

		internal PointF Unproject(PointF p) {
			float svgAR = _dimensions.Width / _dimensions.Height;
			float imgAR = _svgDocument.Width / _svgDocument.Height;
			float width = _svgDocument.Width;
			float height = _svgDocument.Height;
			if (svgAR > imgAR)
				height = width / svgAR;
			else
				width = height * svgAR;

			var x = p.X / _dimensions.Width * width;
			var y = p.Y / _dimensions.Height * height;

			if (svgAR > imgAR)
				y += (_svgDocument.Height - _svgDocument.Width / svgAR) / 2f;
			else
				x += (_svgDocument.Width - _svgDocument.Height * svgAR) / 2f;

			return new PointF(x, y);
		}

		public RectangleF CalcBounds(SvgElement x) {
			var b = (x as ISvgBoundable).Bounds;
			var points = new PointF[4];
			points[0] = new PointF(b.Left - x.StrokeWidth / 2, b.Top - x.StrokeWidth / 2);
			points[1] = new PointF(b.Right + x.StrokeWidth / 2, b.Top - x.StrokeWidth / 2);
			points[2] = new PointF(b.Left - x.StrokeWidth / 2, b.Bottom + x.StrokeWidth / 2);
			points[3] = new PointF(b.Right + x.StrokeWidth / 2, b.Bottom + x.StrokeWidth / 2);

			// x = x.Parent;
			while (x is SvgVisualElement) {
				var m = x.Transforms.GetMatrix();
				m.TransformPoints(points);
				x = x.Parent;
			}
			float minX = points.Min(p => p.X);
			float minY = points.Min(p => p.Y);
			float maxX = points.Max(p => p.X);
			float maxY = points.Max(p => p.Y);
			return RectangleF.FromLTRB(minX, minY, maxX, maxY);
		}

		public class ControllerItem {
			public int Id; // stick, button or trigger Id on controller
			public SvgVisualElement Element;
			public RectangleF Bounds;
			public int Z = 0;
			public int Texture = -1;
		}
		public class Button : ControllerItem {
			public SvgVisualElement Pressed;
			public int PressedTexture = -1;
			public RectangleF PressedBounds;
		}
		public class Stick : ControllerItem {
			public float OffsetScale;
			public int HorizontalAxis;
			public int VerticalAxis;
			public int ButtonId = -1; // pressed only, released will not hide stick
			public SvgVisualElement Pressed;
			public int PressedTexture = -1;
			public RectangleF PressedBounds;
			public int Deadzone;
		}

		public enum TriggerType {
			Slide,
			Bar
		}
		public enum TriggerOrientation {
			Vertical,
			Horizontal
		}
		public class Trigger : ControllerItem {
			public float OffsetScale;
			public int Axis;
			public TriggerType Type = TriggerType.Slide;
			public TriggerOrientation Orientation = TriggerOrientation.Vertical;
			public bool Reverse = false; // applies to 'Bar', normally fills left-to-right; Reverse-->right-to-left
			public bool Inverse = false; // applies to 'Bar', normally fills 0% when not pressed
			public Range Range = new Range(0, 255);
		}
	}
}
