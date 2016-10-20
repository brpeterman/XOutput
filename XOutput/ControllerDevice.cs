using System;
using SlimDX.DirectInput;

namespace XOutput
{

    public struct OutputState
    {
        public byte LX, LY, RX, RY, L2, R2;
        public bool A, B, X, Y, Start, Back, L1, R1, L3, R3, Home;
        public bool DpadUp, DpadRight, DpadDown, DpadLeft;
    }

    public class ControllerDevice
    {
        #region private fields
        private OutputState _outputState;
        #endregion

        #region public properties
        public Joystick Joystick
        {
            get;
            set;
        }

        public int DeviceNumber
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public bool Enabled
        {
            get;
            set;
        }

        public OutputState Output
        {
            get
            {
                return _outputState;
            }
            set
            {
                _outputState = value;
            }
        }

        public byte[] Mapping
        {
            get;
            set;
        }

        public bool[] Buttons
        {
            get;
            set;
        }

        public int[] DPads
        {
            get;
            set;
        }

        public int[] Analogs
        {
            get;
            set;
        }
        #endregion

        delegate byte input(byte subType, byte num);

        #region constructors
        public ControllerDevice(Joystick joy, int num)
        {
            Joystick = joy;
            DeviceNumber = num;
            Name = Joystick.Information.InstanceName;
            Output = new OutputState();
            for (int i = 0; i < 42; i++)
            {
                Mapping[i] = 255; //Changed default mapping to blank
            }
            byte[] saveData = SaveManager.Load(joy.Information.ProductName.ToString());
            if (saveData != null)
                Mapping = saveData;
        }
        #endregion

        #region Utility Functions

        public void Save()
        {
            SaveManager.Save(Joystick.Information.ProductName, Mapping);
        }

        private int[] GetAxes(JoystickState jstate)
        {
            return new int[] { jstate.X, jstate.Y, jstate.Z, jstate.RotationX, jstate.RotationY, jstate.RotationZ };
        }

        private unsafe byte ToByte(bool n)
        {
            return *((byte*)(&n));
        }

        private bool[] GetPov(byte n)
        {
            bool[] b = new bool[4];
            int i = DPads[n];
            switch (i)
            {
                case -1: b[0] = false; b[1] = false; b[2] = false; b[3] = false; break;
                case 0: b[0] = true; b[1] = false; b[2] = false; b[3] = false; break;
                case 4500: b[0] = true; b[1] = false; b[2] = false; b[3] = true; break;
                case 9000: b[0] = false; b[1] = false; b[2] = false; b[3] = true; break;
                case 13500: b[0] = false; b[1] = true; b[2] = false; b[3] = true; break;
                case 18000: b[0] = false; b[1] = true; b[2] = false; b[3] = false; break;
                case 22500: b[0] = false; b[1] = true; b[2] = true; b[3] = false; break;
                case 27000: b[0] = false; b[1] = false; b[2] = true; b[3] = false; break;
                case 31500: b[0] = true; b[1] = false; b[2] = true; b[3] = false; break;
            }
            return b;
        }

        public void ChangeNumber(int n)
        {
            DeviceNumber = n;
        }

        #endregion

        #region Input Types

        byte Button(byte subType, byte num)
        {
            int i = ToByte(Buttons[num]) * 255;
            return (byte)i;
        }

        byte Analog(byte subType, byte num)
        {
            int p = Analogs[num];
            switch (subType)
            {
                case 0: //Normal
                    return (byte)(p / 256);
                case 1: //Inverted
                    return (byte)((65535 - p) / 256);
                case 2: //Half
                    int m = (p - 32767) / 129;
                    if (m < 0)
                    {
                        m = 0;
                    }
                    return (byte)m;
                case 3: //Inverted Half
                    m = (p - 32767) / 129;
                    if (-m < 0)
                    {
                        m = 0;
                    }
                    return (byte)-m;
            }
            return 0;
        }

        byte DPad(byte subType, byte num)
        {
            int i = ToByte(GetPov(num)[subType]) * 255;
            return (byte)i;
        }

        #endregion

        private void UpdateInput()
        {
            Joystick.Poll();
            JoystickState jState = Joystick.GetCurrentState();
            Buttons = jState.GetButtons();
            DPads = jState.GetPointOfViewControllers();
            Analogs = GetAxes(jState);

            input funcButton = Button;
            input funcAnalog = Analog;
            input funcDPad = DPad;
            input[] funcArray = new input[] { funcButton, funcAnalog, funcDPad };

            byte[] output = new byte[21];
            for (int i = 0; i < 21; i++)
            {
                if (Mapping[i * 2] == 255)
                {
                    continue;
                }
                byte subtype = (byte)(Mapping[i * 2] & 0x0F);
                byte type = (byte)((Mapping[i * 2] & 0xF0) >> 4);
                byte num = Mapping[(i * 2) + 1];
                output[i] = funcArray[type](subtype, num);
            }

            _outputState.A = output[0] != 0;
            _outputState.B = output[1] != 0;
            _outputState.X = output[2] != 0;
            _outputState.Y = output[3] != 0;

            _outputState.DpadUp = output[4] != 0;
            _outputState.DpadDown = output[5] != 0;
            _outputState.DpadLeft = output[6] != 0;
            _outputState.DpadRight = output[7] != 0;

            _outputState.L2 = output[9];
            _outputState.R2 = output[8];

            _outputState.L1 = output[10] != 0;
            _outputState.R1 = output[11] != 0;

            _outputState.L3 = output[12] != 0;
            _outputState.R3 = output[13] != 0;

            _outputState.Home = output[14] != 0;
            _outputState.Start = output[15] != 0;
            _outputState.Back = output[16] != 0;

            _outputState.LY = output[17];
            _outputState.LX = output[18];
            _outputState.RY = output[19];
            _outputState.RX = output[20];
            
        }


        public byte[] getoutput()
        {
            UpdateInput();
            byte[] report = new byte[64];
            report[1] = 0x02;
            report[2] = 0x05;
            report[3] = 0x12;

            report[10] = (byte)(
                ((Output.Back ? 1 : 0) << 0) |
                ((Output.L3 ? 1 : 0) << 1) |
                ((Output.R3 ? 1 : 0) << 2) |
                ((Output.Start ? 1 : 0) << 3) |
                ((Output.DpadUp ? 1 : 0) << 4) |
                ((Output.DpadRight ? 1 : 0) << 5) |
                ((Output.DpadDown ? 1 : 0) << 6) |
                ((Output.DpadLeft ? 1 : 0) << 7));

            report[11] = (byte)(
                ((Output.L1 ? 1 : 0) << 2) |
                ((Output.R1 ? 1 : 0) << 3) |
                ((Output.Y ? 1 : 0) << 4) |
                ((Output.B ? 1 : 0) << 5) |
                ((Output.A ? 1 : 0) << 6) |
                ((Output.X ? 1 : 0) << 7));

            //Guide
            report[12] = (byte)(Output.Home ? 0xFF : 0x00);


            report[14] = Output.LX; //Left Stick X


            report[15] = Output.LY; //Left Stick Y


            report[16] = Output.RX; //Right Stick X


            report[17] = Output.RY; //Right Stick Y

            report[26] = Output.R2;
            report[27] = Output.L2;

            return report;
        }


    }
}
