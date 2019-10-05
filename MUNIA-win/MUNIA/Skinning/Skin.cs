using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System;
using System.Runtime.InteropServices;
using MUNIA.Controllers;

namespace MUNIA.Skinning {

	public abstract class Skin {
        // Get a handle to an application window.
        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        // Activate an application window.
        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public string Name { get; set; }
		public string Path { get; set; }
		public SkinLoadResult LoadResult { get; protected set; }
		public List<ControllerType> Controllers { get; } = new List<ControllerType>();
		public abstract Size DefaultSize { get; }

		public abstract void Render(int width, int height, bool force = false);

		public bool UpdateState(IController controller) {
			var oldState = State;
			State = controller?.GetState();
			return !Equals(oldState, State);
		}
		public void UpdateState(ControllerState state) {
			State = state;
		}
		protected ControllerState State;

        private bool autoSplitState = true; // Default to true

        private bool PrevSplitButtonState = false;
        private bool PrevStartButtonState = false;
        private bool PrevToggleButtonState = false;

        private readonly String windowName = "LiveSplit";
        private readonly String splitKey = "{F12}"; // The space key, in this case.

        int splitCombo = 0; // For autoSplit() method

        public void HandleSplit()
        {
            if (State == null) return;

            var toggleButtonState = State.Buttons[5]; // Represents the Z button

            if (toggleButtonState && !PrevToggleButtonState) // Check for leading edge of Z button press
            {
                if (autoSplitState)
                {
                    autoSplitState = false;
                    MessageBox.Show("Auto Splitting Disabled");
                } else
                {
                    autoSplitState = true;
                    MessageBox.Show("Auto Splitting Enabled");
                }

            }

            PrevToggleButtonState = toggleButtonState;

            if (autoSplitState)
                AutoSplit();
            //else
                ManualSplit(); // No else statement so that y always splits
        }


        private void ManualSplit()
        {
            var splitButtonState = State.Buttons[3]; // Represents the Y button

            if (splitButtonState == PrevSplitButtonState) return;

            PrevSplitButtonState = splitButtonState;
            if (!splitButtonState) return;  // We only care about the down press

            IntPtr livesplitHandle = FindWindow(null, windowName);
            if (livesplitHandle != IntPtr.Zero)
            {
                SetForegroundWindow(livesplitHandle);
                SendKeys.Send(splitKey);
            }
            PrevSplitButtonState = splitButtonState;
        }

        private void AutoSplit()
        {
            double axisPos = State.Axes[1]; // Vertical reading of the analog stick
            double upThreshold = -0.39; // Minimum experimental value to go up
            double downThreshold = -0.23; // Minimum experimental value to stop going up

            var aButtonState = State.Buttons[0];
            var bButtonState = State.Buttons[1];
            var startButtonState = State.Buttons[4];

            //SendKeys.Send(splitCombo.ToString());
            //String sAxisPos = axisPos.ToString();

            if (bButtonState) // If the B button is pressed, reset the combo to 0.
                splitCombo = 0;

            if (startButtonState && startButtonState != PrevStartButtonState) // Leading edge of a start button press
            {
                if (splitCombo == 0)
                    splitCombo = 1;
                else
                    splitCombo = 0;
            }

            if (axisPos <= upThreshold)
                if (splitCombo == 1 || splitCombo == 3)
                    splitCombo++;

            if (axisPos >= downThreshold && splitCombo == 2)
                splitCombo = 3;

            if (aButtonState)
            {
                if (splitCombo == 4)
                {
                    SetForegroundWindow(FindWindow(null, windowName)); // Apparently this doesn't throw an error?
                    SendKeys.Send(splitKey);
                }

                splitCombo = 0;
            }

            PrevStartButtonState = startButtonState;
        }


        public abstract void Activate();
		public abstract void Deactivate();

		public static Skin Clone(Skin skin) {
			if (skin is SvgSkin svg) {
				var clone = new SvgSkin();
				clone.Load(svg.Path);
				return clone;
			}
			else if (skin is NintendoSpySkin nspy) {
				var clone = new NintendoSpySkin();
				clone.Load(nspy.Path);
				return clone;
			}
			else if (skin is PadpyghtSkin ppyght) {
				var clone = new PadpyghtSkin();
				clone.Load(ppyght.Path);
				return clone;
			}

			return null;
		}

		public abstract void GetMaxElementNumber(out int maxButtonNum, out int maxAxisNum);

		public abstract bool GetElementsAtLocation(Point location, Size skinSize,
			List<ControllerMapping.Button> buttons, List<ControllerMapping.Axis[]> axes);
	}

	public enum SkinLoadResult {
		Fail,
		Ok,
	}
}
