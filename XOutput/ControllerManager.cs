using SlimDX.DirectInput;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;


namespace XOutput
{

    class ControllerManager : ScpDevice, IDisposable
    {
        public const string BUS_CLASS_GUID = "{F679F562-3164-42CE-A4DB-E7DDBE723909}";

        private class ContData
        {
            public byte[] parsedData = new byte[28];
            public byte[] output = new byte[8];
        }

        #region Private fields
        private ControllerDevice[] _devices;
        private Thread[] _workers = new Thread[4];
        private ContData[] _processingData = new ContData[4];
        private Control _handle;
        private object[] _ds4locks = new object[4];
        #endregion

        #region Public properties
        public bool Running
        {
            get;
            private set;
        }

        public bool IsExclusive
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        public ControllerManager(Control _handle)
            : base(BUS_CLASS_GUID)
        {
            _devices = new ControllerDevice[4];
            this._handle = _handle;
            _ds4locks[0] = new object();
            _ds4locks[1] = new object();
            _ds4locks[2] = new object();
            _ds4locks[3] = new object();
        }
        #endregion

        #region Utility Functions

        public void ChangeExclusive(bool e)
        {
            IsExclusive = e;
            for (int i = 0; i < 4; i++)
            {
                if (_devices[i] != null)
                {
                    if (IsExclusive)
                    {
                        _devices[i].Joystick.Unacquire();
                        _devices[i].Joystick.SetCooperativeLevel(_handle, CooperativeLevel.Exclusive | CooperativeLevel.Background);
                        _devices[i].Joystick.Acquire();
                    }
                    else
                    {
                        _devices[i].Joystick.Unacquire();
                        _devices[i].Joystick.SetCooperativeLevel(_handle, CooperativeLevel.Nonexclusive | CooperativeLevel.Background);
                        _devices[i].Joystick.Acquire();
                    }
                }
            }
        }

        public ControllerDevice GetController(int n)
        {
            return _devices[n];
        }

        public void Swap(int i, int p)
        {
            if (true)//devices[i - 1] != null && devices[p - 1] != null)
            {

                ControllerDevice s = _devices[i - 1];
                _devices[i - 1] = _devices[p - 1];
                _devices[p - 1] = s;
                _devices[p - 1].ChangeNumber(p);

                if (_devices[i - 1] != null)
                    _devices[i - 1].ChangeNumber(i);

            }
        }

        public void SetControllerEnabled(int i, bool b)
        {
            _devices[i].Enabled = b;
        }

        private int Scale(int Value, bool Flip)
        {
            Value -= 0x80;

            if (Value == -128) Value = -127;
            if (Flip) Value *= -1;

            return (int)(Value * 258.00787401574803149606299212599f);
        }

        #endregion

        public override bool Open(int Instance = 0)
        {
            return base.Open(Instance);
        }

        public override bool Open(string DevicePath)
        {
            _path = DevicePath;
            _winUsbHandle = (IntPtr)INVALID_HANDLE_VALUE;

            if (GetDeviceHandle(_path))
            {

                _isActive = true;

            }
            return true;
        }

        public override bool Start()
        {
            Console.WriteLine(Process.GetCurrentProcess().MainWindowHandle);
            Open();
            detectControllers();
            Running = true;
            for (int i = 0; i < 4; i++)
            {
                if (_devices[i] != null && _devices[i].Enabled)
                {
                    Running = true;
                    _processingData[i] = new ContData();
                    Console.WriteLine("Plug " + i);
                    Plugin(i + 1);
                    int t = i;
                    _workers[i] = new Thread(() =>
                    { ProcessData(t); });
                    _workers[i].Start();
                }
            }

            return Running;
        }

        public ControllerDevice[] detectControllers()
        {
            DirectInput directInput = new DirectInput();

            for (int i = 0; i < 4; i++) //Remove disconnected controllers
            {
                if (_devices[i] != null && !directInput.IsDeviceAttached(_devices[i].Joystick.Information.InstanceGuid))
                {
                    Console.WriteLine(_devices[i].Joystick.Properties.InstanceName + " Removed");
                    _devices[i] = null;
                    _workers[i].Abort();
                    _workers[i] = null;
                    Unplug(i + 1);
                }
            }

            foreach (var deviceInstance in directInput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly))
            {
                Joystick joystick = new Joystick(directInput, deviceInstance.InstanceGuid);

                if (joystick.Information.ProductGuid.ToString() == "028e045e-0000-0000-0000-504944564944") //If its an emulated controller skip it
                    continue;

                if (joystick.Capabilities.ButtonCount < 1 && joystick.Capabilities.AxesCount < 1) //Skip if it doesn't have any button and axes
                    continue;

                int spot = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (_devices[i] == null)
                    {
                        if (spot == -1)
                        {
                            spot = i;
                            Console.WriteLine("Open Spot " + i.ToString());
                        }
                    }
                    else if (_devices[i] != null && _devices[i].Joystick.Information.InstanceGuid == deviceInstance.InstanceGuid) //If the device is already initialized skip it
                    {
                        Console.WriteLine("Controller Already Acquired " + i.ToString() + " " + deviceInstance.InstanceName);
                        spot = -1;
                        break;
                    }
                }

                if (spot == -1)
                    continue;

                if (IsExclusive)
                {
                    joystick.SetCooperativeLevel(_handle, CooperativeLevel.Exclusive | CooperativeLevel.Background);
                }
                else
                {
                    joystick.SetCooperativeLevel(_handle, CooperativeLevel.Nonexclusive | CooperativeLevel.Background);
                }
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();

                _devices[spot] = new ControllerDevice(joystick, spot + 1);
                if (IsActive)
                {
                    _processingData[spot] = new ContData();
                    Console.WriteLine("Plug " + spot);
                    Plugin(spot + 1);
                    int t = spot;
                    _workers[spot] = new Thread(() =>
                    { ProcessData(t); });
                    _workers[spot].Start();
                }
            }

            if (directInput != null)
            {
                directInput.Dispose();
            }

            return _devices;
        }

        public override bool Stop()
        {
            if (Running)
            {
                Running = false;
                for (int i = 0; i < 4; i++)
                {
                    if (_devices[i] != null && _devices[i].Enabled)
                    {
                        Console.WriteLine(i);
                        _workers[i].Abort();
                        _workers[i] = null;
                        Unplug(i + 1);
                    }
                }

            }
            return base.Stop();
        }

        public bool Plugin(int Serial)
        {
            if (IsActive)
            {
                int transferred = 0;
                byte[] buffer = new byte[16];

                buffer[0] = 0x10;
                buffer[1] = 0x00;
                buffer[2] = 0x00;
                buffer[3] = 0x00;

                buffer[4] = (byte)((Serial >> 0) & 0xFF);
                buffer[5] = (byte)((Serial >> 8) & 0xFF);
                buffer[6] = (byte)((Serial >> 16) & 0xFF);
                buffer[7] = (byte)((Serial >> 24) & 0xFF);

                return NativeMethods.DeviceIoControl(_fileHandle, 0x2A4000, buffer, buffer.Length, null, 0, ref transferred, IntPtr.Zero);
            }

            return false;
        }

        public bool Unplug(int Serial)
        {
            if (IsActive)
            {
                int Transfered = 0;
                byte[] Buffer = new byte[16];

                Buffer[0] = 0x10;
                Buffer[1] = 0x00;
                Buffer[2] = 0x00;
                Buffer[3] = 0x00;

                Buffer[4] = (byte)((Serial >> 0) & 0xFF);
                Buffer[5] = (byte)((Serial >> 8) & 0xFF);
                Buffer[6] = (byte)((Serial >> 16) & 0xFF);
                Buffer[7] = (byte)((Serial >> 24) & 0xFF);

                return NativeMethods.DeviceIoControl(_fileHandle, 0x2A4004, Buffer, Buffer.Length, null, 0, ref Transfered, IntPtr.Zero);
            }

            return false;
        }

        private void ProcessData(int n)
        {
            while (IsActive)
            {
                lock (_ds4locks[n])
                {
                    if (_devices[n] == null)
                    {
                        //Console.WriteLine("die" + n.ToString());
                        //continue;
                    }
                    byte[] data = _devices[n].getoutput();
                    if (data != null && _devices[n].Enabled)
                    {

                        data[0] = (byte)n;
                        Parse(data, _processingData[n].parsedData);
                        Report(_processingData[n].parsedData, _processingData[n].output);
                    }
                    Thread.Sleep(1);
                }
            }
        }

        public bool Report(byte[] Input, byte[] Output)
        {
            if (IsActive)
            {
                int Transfered = 0;


                bool result = NativeMethods.DeviceIoControl(_fileHandle, 0x2A400C, Input, Input.Length, Output, Output.Length, ref Transfered, IntPtr.Zero) && Transfered > 0;
                int deviceInd = Input[4] - 1;
                return result;

            }
            return false;
        }

        public void Parse(byte[] Input, byte[] Output)
        {
            byte Serial = (byte)(Input[0] + 1);

            for (Int32 Index = 0; Index < 28; Index++) Output[Index] = 0x00;

            Output[0] = 0x1C;
            Output[4] = (byte)(Input[0] + 1);
            Output[9] = 0x14;

            if (true)//Input[1] == 0x02) // Pad is active
            {

                uint Buttons = (uint)((Input[10] << 0) | (Input[11] << 8) | (Input[12] << 16) | (Input[13] << 24));

                if ((Buttons & (0x1 << 0)) > 0) Output[10] |= (byte)(1 << 5); // Back
                if ((Buttons & (0x1 << 1)) > 0) Output[10] |= (byte)(1 << 6); // Left  Thumb
                if ((Buttons & (0x1 << 2)) > 0) Output[10] |= (byte)(1 << 7); // Right Thumb
                if ((Buttons & (0x1 << 3)) > 0) Output[10] |= (byte)(1 << 4); // Start

                if ((Buttons & (0x1 << 4)) > 0) Output[10] |= (byte)(1 << 0); // Up
                if ((Buttons & (0x1 << 5)) > 0) Output[10] |= (byte)(1 << 3); // Down
                if ((Buttons & (0x1 << 6)) > 0) Output[10] |= (byte)(1 << 1); // Right
                if ((Buttons & (0x1 << 7)) > 0) Output[10] |= (byte)(1 << 2); // Left

                if ((Buttons & (0x1 << 10)) > 0) Output[11] |= (byte)(1 << 0); // Left  Shoulder
                if ((Buttons & (0x1 << 11)) > 0) Output[11] |= (byte)(1 << 1); // Right Shoulder

                if ((Buttons & (0x1 << 12)) > 0) Output[11] |= (byte)(1 << 7); // Y
                if ((Buttons & (0x1 << 13)) > 0) Output[11] |= (byte)(1 << 5); // B
                if ((Buttons & (0x1 << 14)) > 0) Output[11] |= (byte)(1 << 4); // A
                if ((Buttons & (0x1 << 15)) > 0) Output[11] |= (byte)(1 << 6); // X

                if ((Buttons & (0x1 << 16)) > 16) Output[11] |= (byte)(1 << 2); // Guide     

                Output[12] = Input[26]; // Left Trigger
                Output[13] = Input[27]; // Right Trigger

                int ThumbLX = Scale(Input[14], false);
                int ThumbLY = -Scale(Input[15], false);
                int ThumbRX = Scale(Input[16], false);
                int ThumbRY = -Scale(Input[17], false);

                Output[14] = (byte)((ThumbLX >> 0) & 0xFF); // LX
                Output[15] = (byte)((ThumbLX >> 8) & 0xFF);

                Output[16] = (byte)((ThumbLY >> 0) & 0xFF); // LY
                Output[17] = (byte)((ThumbLY >> 8) & 0xFF);

                Output[18] = (byte)((ThumbRX >> 0) & 0xFF); // RX
                Output[19] = (byte)((ThumbRX >> 8) & 0xFF);

                Output[20] = (byte)((ThumbRY >> 0) & 0xFF); // RY
                Output[21] = (byte)((ThumbRY >> 8) & 0xFF);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_fileHandle != null)
                    {
                        _fileHandle.Dispose();
                    }
                }

                base.Dispose(disposing);

                disposedValue = true;
            }
        }
        #endregion

    }
}
