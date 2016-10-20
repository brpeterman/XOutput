using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XOutput
{
    public partial class ScpDevice : IDisposable
    {
        public virtual bool IsActive
        {
            get
            {
                return _isActive;
            }
            private set
            {
                _isActive = value;
            }
        }

        public virtual string Path
        {
            get
            {
                return _path;
            }
            private set
            {
                _path = value;
            }
        }

        public ScpDevice(string Class)
        {
            _class = new Guid(Class);
        }


        public virtual bool Open(int Instance = 0)
        {
            string DevicePath = string.Empty;
            _winUsbHandle = (IntPtr)INVALID_HANDLE_VALUE;

            if (Find(_class, ref DevicePath, Instance))
            {
                Open(DevicePath);
            }

            return _isActive;
        }

        public virtual bool Open(string DevicePath)
        {
            _path = DevicePath.ToUpper();
            _winUsbHandle = (IntPtr)INVALID_HANDLE_VALUE;

            if (GetDeviceHandle(_path))
            {
                if (NativeMethods.WinUsb_Initialize(_fileHandle, ref _winUsbHandle))
                {
                    if (InitializeDevice())
                    {
                        _isActive = true;
                    }
                    else
                    {
                        NativeMethods.WinUsb_Free(_winUsbHandle);
                        _winUsbHandle = (IntPtr)INVALID_HANDLE_VALUE;
                    }
                }
                else
                {
                    _fileHandle.Close();
                }
            }

            return _isActive;
        }

        public virtual bool Start()
        {
            return _isActive;
        }

        public virtual bool Stop()
        {
            _isActive = false;

            if (!(_winUsbHandle == (IntPtr)INVALID_HANDLE_VALUE))
            {
                NativeMethods.WinUsb_AbortPipe(_winUsbHandle, _intIn);
                NativeMethods.WinUsb_AbortPipe(_winUsbHandle, _bulkIn);
                NativeMethods.WinUsb_AbortPipe(_winUsbHandle, _bulkOut);

                NativeMethods.WinUsb_Free(_winUsbHandle);
                _winUsbHandle = (IntPtr)INVALID_HANDLE_VALUE;
            }

            if (_fileHandle != null && !_fileHandle.IsInvalid && !_fileHandle.IsClosed)
            {
                _fileHandle.Close();
                _fileHandle = null;
            }

            return true;
        }

        public virtual bool Close()
        {
            return Stop();
        }




        #region Constant and Structure Definitions
        public const int SERVICE_CONTROL_STOP = 0x00000001;
        public const int SERVICE_CONTROL_SHUTDOWN = 0x00000005;
        public const int SERVICE_CONTROL_DEVICEEVENT = 0x0000000B;
        public const int SERVICE_CONTROL_POWEREVENT = 0x0000000D;

        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEQUERYREMOVE = 0x8001;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int DBT_DEVTYP_DEVICEINTERFACE = 0x0005;
        public const int DBT_DEVTYP_HANDLE = 0x0006;

        public const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        public const int PBT_APMSUSPEND = 0x0004;

        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;
        public const int DEVICE_NOTIFY_SERVICE_HANDLE = 0x0001;
        public const int DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x0004;

        public const int WM_DEVICECHANGE = 0x0219;

        public const int DIGCF_PRESENT = 0x0002;
        public const int DIGCF_DEVICEINTERFACE = 0x0010;

        public delegate int ServiceControlHandlerEx(int Control, int Type, IntPtr Data, IntPtr Context);

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_DEVICEINTERFACE
        {
            internal int dbcc_size;
            internal int dbcc_devicetype;
            internal int dbcc_reserved;
            internal Guid dbcc_classguid;
            internal short dbcc_name;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class DEV_BROADCAST_DEVICEINTERFACE_M
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
            public byte[] dbcc_classguid;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 255)]
            public Char[] dbcc_name;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            internal int cbSize;
            internal Guid InterfaceClassGuid;
            internal int Flags;
            internal IntPtr Reserved;
        }

        protected const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        protected const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        protected const uint FILE_SHARE_READ = 1;
        protected const uint FILE_SHARE_WRITE = 2;
        protected const uint GENERIC_READ = 0x80000000;
        protected const uint GENERIC_WRITE = 0x40000000;
        protected const int INVALID_HANDLE_VALUE = -1;
        protected const uint OPEN_EXISTING = 3;
        protected const uint DEVICE_SPEED = 1;
        protected const byte USB_ENDPOINT_DIRECTION_MASK = 0x80;

        internal enum POLICY_TYPE
        {
            SHORT_PACKET_TERMINATE = 1,
            AUTO_CLEAR_STALL = 2,
            PIPE_TRANSFER_TIMEOUT = 3,
            IGNORE_SHORT_PACKETS = 4,
            ALLOW_PARTIAL_READS = 5,
            AUTO_FLUSH = 6,
            RAW_IO = 7,
        }

        internal enum USBD_PIPE_TYPE
        {
            UsbdPipeTypeControl = 0,
            UsbdPipeTypeIsochronous = 1,
            UsbdPipeTypeBulk = 2,
            UsbdPipeTypeInterrupt = 3,
        }

        internal enum USB_DEVICE_SPEED
        {
            UsbLowSpeed = 1,
            UsbFullSpeed = 2,
            UsbHighSpeed = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct USB_CONFIGURATION_DESCRIPTOR
        {
            internal byte bLength;
            internal byte bDescriptorType;
            internal ushort wTotalLength;
            internal byte bNumInterfaces;
            internal byte bConfigurationValue;
            internal byte iConfiguration;
            internal byte bmAttributes;
            internal byte MaxPower;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct USB_INTERFACE_DESCRIPTOR
        {
            internal byte bLength;
            internal byte bDescriptorType;
            internal byte bInterfaceNumber;
            internal byte bAlternateSetting;
            internal byte bNumEndpoints;
            internal byte bInterfaceClass;
            internal byte bInterfaceSubClass;
            internal byte bInterfaceProtocol;
            internal byte iInterface;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINUSB_PIPE_INFORMATION
        {
            internal USBD_PIPE_TYPE PipeType;
            internal byte PipeId;
            internal ushort MaximumPacketSize;
            internal byte Interval;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct WINUSB_SETUP_PACKET
        {
            internal byte RequestType;
            internal byte Request;
            internal ushort Value;
            internal ushort Index;
            internal ushort Length;
        }

        protected const int DIF_PROPERTYCHANGE = 0x12;
        protected const int DICS_ENABLE = 1;
        protected const int DICS_DISABLE = 2;
        protected const int DICS_PROPCHANGE = 3;
        protected const int DICS_FLAG_GLOBAL = 1;

        [StructLayout(LayoutKind.Sequential)]
        internal struct SP_CLASSINSTALL_HEADER
        {
            internal int cbSize;
            internal int InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SP_PROPCHANGE_PARAMS
        {
            internal SP_CLASSINSTALL_HEADER ClassInstallHeader;
            internal int StateChange;
            internal int Scope;
            internal int HwProfile;
        }
        #endregion

        #region Protected Data Members
        protected Guid _class = Guid.Empty;
        protected string _path = string.Empty;

        protected SafeFileHandle _fileHandle = null;
        protected IntPtr _winUsbHandle = IntPtr.Zero;

        protected byte _intIn = 0xFF;
        protected byte _intOut = 0xFF;
        protected byte _bulkIn = 0xFF;
        protected byte _bulkOut = 0xFF;

        protected bool _isActive;
        #endregion

        #region Static Helper Methods
        public enum Notified { Ignore = 0x0000, Arrival = 0x8000, QueryRemove = 0x8001, Removal = 0x8004 };

        public static bool RegisterNotify(IntPtr Form, Guid Class, ref IntPtr Handle, bool Window = true)
        {
            IntPtr devBroadcastDeviceInterfaceBuffer = IntPtr.Zero;

            try
            {
                DEV_BROADCAST_DEVICEINTERFACE devBroadcastDeviceInterface = new DEV_BROADCAST_DEVICEINTERFACE();
                int Size = Marshal.SizeOf(devBroadcastDeviceInterface);

                devBroadcastDeviceInterface.dbcc_size = Size;
                devBroadcastDeviceInterface.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                devBroadcastDeviceInterface.dbcc_reserved = 0;
                devBroadcastDeviceInterface.dbcc_classguid = Class;

                devBroadcastDeviceInterfaceBuffer = Marshal.AllocHGlobal(Size);
                Marshal.StructureToPtr(devBroadcastDeviceInterface, devBroadcastDeviceInterfaceBuffer, true);

                Handle = NativeMethods.RegisterDeviceNotification(Form, devBroadcastDeviceInterfaceBuffer, Window ? DEVICE_NOTIFY_WINDOW_HANDLE : DEVICE_NOTIFY_SERVICE_HANDLE);

                Marshal.PtrToStructure(devBroadcastDeviceInterfaceBuffer, devBroadcastDeviceInterface);

                return Handle != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                throw;
            }
            finally
            {
                if (devBroadcastDeviceInterfaceBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(devBroadcastDeviceInterfaceBuffer);
                }
            }
        }

        public static bool UnregisterNotify(IntPtr Handle)
        {
            try
            {
                return NativeMethods.UnregisterDeviceNotification(Handle);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                throw;
            }
        }
        #endregion

        #region Protected Methods
        protected virtual bool Find(Guid Target, ref string Path, int Instance = 0)
        {
            IntPtr detailDataBuffer = IntPtr.Zero;
            IntPtr deviceInfoSet = IntPtr.Zero;

            try
            {
                SP_DEVICE_INTERFACE_DATA DeviceInterfaceData = new SP_DEVICE_INTERFACE_DATA(), da = new SP_DEVICE_INTERFACE_DATA();
                int bufferSize = 0, memberIndex = 0;

                deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref Target, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

                DeviceInterfaceData.cbSize = da.cbSize = Marshal.SizeOf(DeviceInterfaceData);

                while (NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref Target, memberIndex, ref DeviceInterfaceData))
                {
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref DeviceInterfaceData, IntPtr.Zero, 0, ref bufferSize, ref da);
                    {
                        detailDataBuffer = Marshal.AllocHGlobal(bufferSize);

                        Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);

                        if (NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref DeviceInterfaceData, detailDataBuffer, bufferSize, ref bufferSize, ref da))
                        {
                            IntPtr pDevicePathName = new IntPtr(IntPtr.Size == 4 ? detailDataBuffer.ToInt32() + 4 : detailDataBuffer.ToInt64() + 4);

                            Path = Marshal.PtrToStringAuto(pDevicePathName).ToUpper();
                            Marshal.FreeHGlobal(detailDataBuffer);

                            if (memberIndex == Instance) return true;
                        }
                        else Marshal.FreeHGlobal(detailDataBuffer);
                    }

                    memberIndex++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                throw;
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero)
                {
                    NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }

            return false;
        }


        protected virtual bool GetDeviceHandle(string Path)
        {
            _fileHandle = NativeMethods.CreateFile(Path, (GENERIC_WRITE | GENERIC_READ), FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED, UIntPtr.Zero);

            return !_fileHandle.IsInvalid;
        }

        protected virtual bool UsbEndpointDirectionIn(int addr)
        {
            return (addr & 0x80) == 0x80;
        }

        protected virtual bool UsbEndpointDirectionOut(int addr)
        {
            return (addr & 0x80) == 0x00;
        }

        protected virtual bool InitializeDevice()
        {
            try
            {
                USB_INTERFACE_DESCRIPTOR ifaceDescriptor = new USB_INTERFACE_DESCRIPTOR();
                WINUSB_PIPE_INFORMATION pipeInfo = new WINUSB_PIPE_INFORMATION();

                if (NativeMethods.WinUsb_QueryInterfaceSettings(_winUsbHandle, 0, ref ifaceDescriptor))
                {
                    for (int i = 0; i < ifaceDescriptor.bNumEndpoints; i++)
                    {
                        NativeMethods.WinUsb_QueryPipe(_winUsbHandle, 0, System.Convert.ToByte(i), ref pipeInfo);

                        if (((pipeInfo.PipeType == USBD_PIPE_TYPE.UsbdPipeTypeBulk) & UsbEndpointDirectionIn(pipeInfo.PipeId)))
                        {
                            _bulkIn = pipeInfo.PipeId;
                            NativeMethods.WinUsb_FlushPipe(_winUsbHandle, _bulkIn);
                        }
                        else if (((pipeInfo.PipeType == USBD_PIPE_TYPE.UsbdPipeTypeBulk) & UsbEndpointDirectionOut(pipeInfo.PipeId)))
                        {
                            _bulkOut = pipeInfo.PipeId;
                            NativeMethods.WinUsb_FlushPipe(_winUsbHandle, _bulkOut);
                        }
                        else if ((pipeInfo.PipeType == USBD_PIPE_TYPE.UsbdPipeTypeInterrupt) & UsbEndpointDirectionIn(pipeInfo.PipeId))
                        {
                            _intIn = pipeInfo.PipeId;
                            NativeMethods.WinUsb_FlushPipe(_winUsbHandle, _intIn);
                        }
                        else if ((pipeInfo.PipeType == USBD_PIPE_TYPE.UsbdPipeTypeInterrupt) & UsbEndpointDirectionOut(pipeInfo.PipeId))
                        {
                            _intOut = pipeInfo.PipeId;
                            NativeMethods.WinUsb_FlushPipe(_winUsbHandle, _intOut);
                        }
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                throw;
            }
        }

        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
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

                disposedValue = true;
            }
        }
        
        ~ScpDevice() {
          // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
          Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
