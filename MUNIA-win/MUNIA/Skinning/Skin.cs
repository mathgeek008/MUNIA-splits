using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
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
        private readonly String splitKey = "{F12}"; // Default split key

        private readonly Stopwatch timer = new Stopwatch();

        int splitCombo = 0; // For autoSplit() method

        public void HandleSplit()
        {
            if (State == null) return;

            var toggleButtonState = State.Buttons[5]; // Represents the Z button

            if (toggleButtonState && !PrevToggleButtonState) // Check for leading edge of Z button press
            {
                if (autoSplitState)
                    autoSplitState = false;
                else
                    autoSplitState = true;
            }

            PrevToggleButtonState = toggleButtonState;

            if (autoSplitState)
                AutoSplit();

            ManualSplit(); // Always be able to press Y to split
        }

        public Boolean isAuto()
        {
            if (autoSplitState)
                return true;
            return false;
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
            double upThreshold = -0.55; // Minimum experimental value to go up
            double downThreshold = -0.32; // Minimum experimental value to stop going up

            var aButtonState = State.Buttons[0];
            var bButtonState = State.Buttons[1];
            var startButtonState = State.Buttons[4];

            if (bButtonState || axisPos > -upThreshold || timer.ElapsedMilliseconds > 1000) // Conditions for a reset
                splitCombo = 0;

            if (startButtonState && startButtonState != PrevStartButtonState) // Leading edge of a start button press
            {
                if (splitCombo == 0)
                {
                    splitCombo = 1;
                }
                else
                    splitCombo = 0;
            }

            if (axisPos <= upThreshold) // Check for up input
                if (splitCombo == 1)
                {
                    splitCombo++;
                    timer.Start();
                }
                else if (splitCombo == 3)
                {
                    if (timer.ElapsedMilliseconds >= 180) // 100% safely assume (unless lag) that you are over "Select Stage"
                    {
                        splitCombo++;
                        timer.Reset();
                    }
                    else if (timer.ElapsedMilliseconds >= 156 && timer.ElapsedMilliseconds <= 179) // Unpredictable it seems
                    {
                        //TODO Figure out what causes this to sometimes go to stage select and other times exit game
                        splitCombo++;
                        timer.Reset();
                    }
                }

            if (axisPos >= downThreshold && splitCombo == 2) // Neutral Input
            {
                splitCombo++;
            }

            if (aButtonState)
            {
                if (splitCombo == 4)
                {
                    SetForegroundWindow(FindWindow(null, windowName)); // Apparently this doesn't throw an error?
                    SendKeys.Send(splitKey);
                }

                splitCombo = 0;
            }

            if (splitCombo == 0) // Reset timer if menu is closed out
                timer.Reset();

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
