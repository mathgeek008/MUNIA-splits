﻿using HidSharp;
using MUNIA.Util;

namespace MUNIA.Controllers {
    public class MusiaPS2 : MuniaController {
        public MusiaPS2(HidDevice hidDevice) : base(hidDevice) {
            _buttons.EnsureSize(16);
            _hats.EnsureSize(1);
            _axes.EnsureSize(4);
        }

        public override ControllerType Type => ControllerType.PS2;
        public override string Name => string.IsNullOrEmpty(base.Name) ? "MUSIA PS2" : base.Name;

        protected override bool Parse(byte[] ev) {
            byte reportId = ev[0];
            if (reportId != 1) return false;

            // cross square circle triangle select start lstick rstick
            int i = 0, mask = 0x01;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[1] & mask) != 0; mask <<= 1;

            // l1, r1, l2, r2
            mask = 0x01;
            _buttons[i++] = (ev[2] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[2] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[2] & mask) != 0; mask <<= 1;
            _buttons[i++] = (ev[2] & mask) != 0; mask <<= 1;

            // UP DOWN LEFT RIGHT
            Hat hat = ControllerState.HatLookup[(byte)(ev[2] >> 4)];
            _hats[0] = hat;

            _buttons[i++] = hat.HasFlag(Hat.Up);
            _buttons[i++] = hat.HasFlag(Hat.Down);
            _buttons[i++] = hat.HasFlag(Hat.Left);
            _buttons[i++] = hat.HasFlag(Hat.Right);

            _axes[0] = ev[3] - 128;
            _axes[1] = ev[4] - 128;
            _axes[2] = ev[5] - 128;
            _axes[3] = ev[6] - 128;

            return true;
        }

    }
}