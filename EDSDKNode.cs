#region usings
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

using VVVV.Core.Logging;

using EDSDKLib;

using FeralTic.DX11.Resources;

#endregion usings

using System.Threading;

/*
TODO:
test alternatives: https://github.com/esskar/Canon.Eos.Framework (better enums)
use several cameras & disconnected one
*/

namespace VVVV.DX11.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "EDSDK", AutoEvaluate = true, Category = "Canon", Help = "", Tags = "")]
	#endregion PluginInfo
	public class EDSDKNode : IPluginEvaluate, IDX11ResourceProvider, IDisposable
	{
		#region fields & pins
		[Input("Device ID")]
		public ISpread<int> FInDeviceID;
		
		[Input("Tv", EnumName = "Tv")]
		public IDiffSpread<EnumEntry> FInTv;
		
		[Input("Av", EnumName = "Av")]
		public IDiffSpread<EnumEntry> FInAv;
		
		[Input("ISO", EnumName = "ISO")]
		public IDiffSpread<EnumEntry> FInISO;
		
		[Input("WB", DefaultEnumEntry = "None")]
		public IDiffSpread<WB> FInWB;
		
		[Input("Drive Focus", DefaultEnumEntry = "None")]
		public IDiffSpread<DriveFocus> FInFocusSpeed;
		
		[Input("Bulb (s)")]
		public ISpread<int> FInBulb;
		
		[Input("Live View")]
		public IDiffSpread<bool> FInLiveView;
		
		[Input("Save To", DefaultEnumEntry = "Computer")]
		public IDiffSpread<SaveTo> FInSaveTo;
		
		[Input("Save Path", StringType = StringType.Directory)]
		public IDiffSpread<string> FInSavePath;
		
		[Input("Focus Mode", DefaultEnumEntry = "None")]
		public IDiffSpread<FocusMode> FInFocusMode;
		
		[Input("Force Focus", IsBang = true)]
		public ISpread<bool> FInForceFocus;

        [Input("Use Autofocus", DefaultBoolean = false)]
		public ISpread<bool> FInUseAutofocus;
		
		[Input("Take Photo", IsBang = true)]
		public ISpread<bool> FInTakePhoto;
		
		[Input("Take Video")]
		public IDiffSpread<bool> FInTakeVideo;
		
		[Input("Update", IsBang = true)]
		public ISpread<bool> FInUpdate;
		
		[Input("Enable")]
		public IDiffSpread<bool> FInEnable;
		
		
		
		[Output("Name")]
		public ISpread<string> FOutName;
		
		[Output("Port")]
		public ISpread<string> FOutPort;
		
		
		
		[Output("Progress")]
		public ISpread<int> FOutProgress;
		
		[Output("Texture Out")]
		protected Pin<DX11Resource<DX11DynamicTexture2D>> FTextureOutput;
		
		[Output("Saved", IsBang = true)]
		public ISpread<bool> FOutSaved;
		
		[Output("Valid")]
		public ISpread<bool> FOutValid;
		
		[Output("Error")]
		public ISpread<string> FOutError;
		
		
		[Import()]
		public ILogger FLogger;
		#endregion fields & pins

        #region enums
        public enum SaveTo
		{
			Computer = 0,
			Camera,
			Both
		}
		
		public enum WB
		{
			None = 0,
			Auto,
			Daylight,
			Cloudy,
			Tangsten,
			Fluorescent,
			Strobe,
			WhitePaper,
			Shade
		};
		
		public enum DriveFocus
		{
			None = 0,
			Near3,
			Near2,
			Near1,
			Far1,
			Far2,
			Far3
		}
		
		public enum FocusMode
		{
			AutoQuick = 0,
			AutoLive,
			AutoLiveFace,
			AutoLiveMulti
		}
        #endregion enums

        #region errors
        public enum EDSDKError
		{
			/*-----------------------------------------------------------------------
ED-SDK Functin Success Code
------------------------------------------------------------------------*/
			EDS_ERR_OK =                                          0x00000000,
			
			/*-----------------------------------------------------------------------
ED-SDK Generic Error IDs
------------------------------------------------------------------------*/
			/* Miscellaneous errors */
			EDS_ERR_UNIMPLEMENTED =                               0x00000001,
			EDS_ERR_INTERNAL_ERROR =                              0x00000002,
			EDS_ERR_MEM_ALLOC_FAILED =                            0x00000003,
			EDS_ERR_MEM_FREE_FAILED =                             0x00000004,
			EDS_ERR_OPERATION_CANCELLED =                         0x00000005,
			EDS_ERR_INCOMPATIBLE_VERSION =                        0x00000006,
			EDS_ERR_NOT_SUPPORTED =                               0x00000007,
			EDS_ERR_UNEXPECTED_EXCEPTION =                        0x00000008,
			EDS_ERR_PROTECTION_VIOLATION =                        0x00000009,
			EDS_ERR_MISSING_SUBCOMPONENT =                        0x0000000A,
			EDS_ERR_SELECTION_UNAVAILABLE =                       0x0000000B,
			
			/* File errors */
			EDS_ERR_FILE_IO_ERROR =                               0x00000020,
			EDS_ERR_FILE_TOO_MANY_OPEN =                          0x00000021,
			EDS_ERR_FILE_NOT_FOUND =                              0x00000022,
			EDS_ERR_FILE_OPEN_ERROR =                             0x00000023,
			EDS_ERR_FILE_CLOSE_ERROR =                            0x00000024,
			EDS_ERR_FILE_SEEK_ERROR =                             0x00000025,
			EDS_ERR_FILE_TELL_ERROR =                             0x00000026,
			EDS_ERR_FILE_READ_ERROR =                             0x00000027,
			EDS_ERR_FILE_WRITE_ERROR =                            0x00000028,
			EDS_ERR_FILE_PERMISSION_ERROR =                       0x00000029,
			EDS_ERR_FILE_DISK_FULL_ERROR =                        0x0000002A,
			EDS_ERR_FILE_ALREADY_EXISTS =                         0x0000002B,
			EDS_ERR_FILE_FORMAT_UNRECOGNIZED =                    0x0000002C,
			EDS_ERR_FILE_DATA_CORRUPT =                           0x0000002D,
			EDS_ERR_FILE_NAMING_NA =                              0x0000002E,
			
			/* Directory errors */
			EDS_ERR_DIR_NOT_FOUND =                               0x00000040,
			EDS_ERR_DIR_IO_ERROR =                                0x00000041,
			EDS_ERR_DIR_ENTRY_NOT_FOUND =                         0x00000042,
			EDS_ERR_DIR_ENTRY_EXISTS =                            0x00000043,
			EDS_ERR_DIR_NOT_EMPTY =                               0x00000044,
			
			/* Property errors */
			EDS_ERR_PROPERTIES_UNAVAILABLE =                      0x00000050,
			EDS_ERR_PROPERTIES_MISMATCH =                         0x00000051,
			EDS_ERR_PROPERTIES_NOT_LOADED =                       0x00000053,
			
			/* Function Parameter errors */
			EDS_ERR_INVALID_PARAMETER =                           0x00000060,
			EDS_ERR_INVALID_HANDLE =                              0x00000061,
			EDS_ERR_INVALID_POINTER =                             0x00000062,
			EDS_ERR_INVALID_INDEX =                               0x00000063,
			EDS_ERR_INVALID_LENGTH =                              0x00000064,
			EDS_ERR_INVALID_FN_POINTER =                          0x00000065,
			EDS_ERR_INVALID_SORT_FN =                             0x00000066,
			
			/* Device errors */
			EDS_ERR_DEVICE_NOT_FOUND =                            0x00000080,
			EDS_ERR_DEVICE_BUSY =                                 0x00000081,
			EDS_ERR_DEVICE_INVALID =                              0x00000082,
			EDS_ERR_DEVICE_EMERGENCY =                            0x00000083,
			EDS_ERR_DEVICE_MEMORY_FULL =                          0x00000084,
			EDS_ERR_DEVICE_INTERNAL_ERROR =                       0x00000085,
			EDS_ERR_DEVICE_INVALID_PARAMETER =                    0x00000086,
			EDS_ERR_DEVICE_NO_DISK =                              0x00000087,
			EDS_ERR_DEVICE_DISK_ERROR =                           0x00000088,
			EDS_ERR_DEVICE_CF_GATE_CHANGED =                      0x00000089,
			EDS_ERR_DEVICE_DIAL_CHANGED =                         0x0000008A,
			EDS_ERR_DEVICE_NOT_INSTALLED =                        0x0000008B,
			EDS_ERR_DEVICE_STAY_AWAKE =                           0x0000008C,
			EDS_ERR_DEVICE_NOT_RELEASED =                         0x0000008D,
			
			/* Stream errors */
			EDS_ERR_STREAM_IO_ERROR =                             0x000000A0,
			EDS_ERR_STREAM_NOT_OPEN =                             0x000000A1,
			EDS_ERR_STREAM_ALREADY_OPEN =                         0x000000A2,
			EDS_ERR_STREAM_OPEN_ERROR =                           0x000000A3,
			EDS_ERR_STREAM_CLOSE_ERROR =                          0x000000A4,
			EDS_ERR_STREAM_SEEK_ERROR =                           0x000000A5,
			EDS_ERR_STREAM_TELL_ERROR =                           0x000000A6,
			EDS_ERR_STREAM_READ_ERROR =                           0x000000A7,
			EDS_ERR_STREAM_WRITE_ERROR =                          0x000000A8,
			EDS_ERR_STREAM_PERMISSION_ERROR =                     0x000000A9,
			EDS_ERR_STREAM_COULDNT_BEGIN_THREAD =                 0x000000AA,
			EDS_ERR_STREAM_BAD_OPTIONS =                          0x000000AB,
			EDS_ERR_STREAM_END_OF_STREAM =                        0x000000AC,
			
			/* Communications errors */
			EDS_ERR_COMM_PORT_IS_IN_USE =                         0x000000C0,
			EDS_ERR_COMM_DISCONNECTED =                           0x000000C1,
			EDS_ERR_COMM_DEVICE_INCOMPATIBLE =                    0x000000C2,
			EDS_ERR_COMM_BUFFER_FULL =                            0x000000C3,
			EDS_ERR_COMM_USB_BUS_ERR =                            0x000000C4,
			
			/* Lock/Unlock */
			EDS_ERR_USB_DEVICE_LOCK_ERROR =                       0x000000D0,
			EDS_ERR_USB_DEVICE_UNLOCK_ERROR =                     0x000000D1,
			
			/* STI/WIA */
			EDS_ERR_STI_UNKNOWN_ERROR =                           0x000000E0,
			EDS_ERR_STI_INTERNAL_ERROR =                          0x000000E1,
			EDS_ERR_STI_DEVICE_CREATE_ERROR =                     0x000000E2,
			EDS_ERR_STI_DEVICE_RELEASE_ERROR =                    0x000000E3,
			EDS_ERR_DEVICE_NOT_LAUNCHED =                         0x000000E4,
			
			EDS_ERR_ENUM_NA =                                     0x000000F0,
			EDS_ERR_INVALID_FN_CALL =                             0x000000F1,
			EDS_ERR_HANDLE_NOT_FOUND =                            0x000000F2,
			EDS_ERR_INVALID_ID =                                  0x000000F3,
			EDS_ERR_WAIT_TIMEOUT_ERROR =                          0x000000F4,
			
			/* PTP */
			EDS_ERR_SESSION_NOT_OPEN =                            0x00002003,
			EDS_ERR_INVALID_TRANSACTIONID =                       0x00002004,
			EDS_ERR_INCOMPLETE_TRANSFER =                         0x00002007,
			EDS_ERR_INVALID_STRAGEID =                            0x00002008,
			EDS_ERR_DEVICEPROP_NOT_SUPPORTED =                    0x0000200A,
			EDS_ERR_INVALID_OBJECTFORMATCODE =                    0x0000200B,
			EDS_ERR_SELF_TEST_FAILED =                            0x00002011,
			EDS_ERR_PARTIAL_DELETION =                            0x00002012,
			EDS_ERR_SPECIFICATION_BY_FORMAT_UNSUPPORTED =         0x00002014,
			EDS_ERR_NO_VALID_OBJECTINFO =                         0x00002015,
			EDS_ERR_INVALID_CODE_FORMAT =                         0x00002016,
			EDS_ERR_UNKNOWN_VENDER_CODE =                         0x00002017,
			EDS_ERR_CAPTURE_ALREADY_TERMINATED =                  0x00002018,
			EDS_ERR_INVALID_PARENTOBJECT =                        0x0000201A,
			EDS_ERR_INVALID_DEVICEPROP_FORMAT =                   0x0000201B,
			EDS_ERR_INVALID_DEVICEPROP_VALUE =                    0x0000201C,
			EDS_ERR_SESSION_ALREADY_OPEN =                        0x0000201E,
			EDS_ERR_TRANSACTION_CANCELLED =                       0x0000201F,
			EDS_ERR_SPECIFICATION_OF_DESTINATION_UNSUPPORTED =    0x00002020,
			EDS_ERR_UNKNOWN_COMMAND =                             0x0000A001,
			EDS_ERR_OPERATION_REFUSED =                           0x0000A005,
			EDS_ERR_LENS_COVER_CLOSE =                            0x0000A006,
			EDS_ERR_LOW_BATTERY =									0x0000A101,
			EDS_ERR_OBJECT_NOTREADY =								0x0000A102,
			
			/* Capture Error */
			EDS_ERR_TAKE_PICTURE_AF_NG =							0x00008D01,
			EDS_ERR_TAKE_PICTURE_RESERVED =						0x00008D02,
			EDS_ERR_TAKE_PICTURE_MIRROR_UP_NG =					0x00008D03,
			EDS_ERR_TAKE_PICTURE_SENSOR_CLEANING_NG =				0x00008D04,
			EDS_ERR_TAKE_PICTURE_SILENCE_NG =						0x00008D05,
			EDS_ERR_TAKE_PICTURE_NO_CARD_NG =						0x00008D06,
			EDS_ERR_TAKE_PICTURE_CARD_NG =						0x00008D07,
			EDS_ERR_TAKE_PICTURE_CARD_PROTECT_NG =				0x00008D08,
			
			
			EDS_ERR_LAST_GENERIC_ERROR_PLUS_ONE =                 0x000000F5
			
		}
		#endregion errors
		
		private SDKHandler CameraHandler;
		
		private bool update;
		private bool init;
		private bool saved;
		
		private object locker = new object();
		
		private Bitmap bmp;
		
		List<int> AvList;
		List<int> TvList;
		List<int> ISOList;
		private List<Camera> CamList;
		
		private bool isChanged;
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			for (int i = 0; i < this.FTextureOutput.SliceCount; i++)
			{
				if (this.FTextureOutput[i] == null) { this.FTextureOutput[i] = new DX11Resource<DX11DynamicTexture2D>(); }
			}
			
			if (!init)
			{
				CameraHandler = new SDKHandler();
				
				CameraHandler.CameraAdded += new SDKHandler.CameraAddedHandler(SDK_CameraAdded);
				CameraHandler.LiveViewUpdated += new SDKHandler.StreamUpdate(SDK_LiveViewUpdated);
				CameraHandler.ProgressChanged += new SDKHandler.ProgressHandler(SDK_ProgressChanged);
				CameraHandler.CameraHasShutdown += SDK_CameraHasShutdown;
				
				EnumManager.UpdateEnum("Tv", "None", new string[] { "None" });
				EnumManager.UpdateEnum("Av", "None", new string[] { "None" });
				EnumManager.UpdateEnum("ISO", "None", new string[] { "None" });
				
				init = true;
			}
			
			if (FInUpdate[0])
			{
				Refresh();
			}
			
			if (FInEnable.IsChanged)
			{
				if (FInEnable[0] && !CameraHandler.CameraSessionOpen) { FLogger.Log(LogType.Debug, "OpenSession"); OpenSession(); isChanged = true; }
				if (!FInEnable[0] && CameraHandler.CameraSessionOpen) { FLogger.Log(LogType.Debug, "CloseSession"); CloseSession(); }
			}
			
			
			if (CameraHandler.CameraSessionOpen)
			{
				if (FInAv.IsChanged || isChanged)
				{
					if (FInAv[0].Name != "None") CameraHandler.SetSetting(EDSDK.PropID_Av, CameraValues.AV(FInAv[0].Name));
				}
				
				if (FInTv.IsChanged || isChanged)
				{
					if (FInTv[0].Name != "None") CameraHandler.SetSetting(EDSDK.PropID_Tv, CameraValues.TV(FInTv[0].Name));
				}
				
				if (FInISO.IsChanged || isChanged)
				{
					if (FInISO[0].Name != "None") CameraHandler.SetSetting(EDSDK.PropID_Tv, CameraValues.TV(FInISO[0].Name));
				}
				
				if (FInFocusSpeed.IsChanged || isChanged)
				{
					if (FInFocusSpeed[0] != DriveFocus.None)
					{
						switch (FInFocusSpeed[0])
						{
							case DriveFocus.Far1: CameraHandler.SetFocus(EDSDK.EvfDriveLens_Far1); break;
							case DriveFocus.Far2: CameraHandler.SetFocus(EDSDK.EvfDriveLens_Far2); break;
							case DriveFocus.Far3: CameraHandler.SetFocus(EDSDK.EvfDriveLens_Far3); break;
							case DriveFocus.Near1: CameraHandler.SetFocus(EDSDK.EvfDriveLens_Near1); break;
							case DriveFocus.Near2: CameraHandler.SetFocus(EDSDK.EvfDriveLens_Near2); break;
							case DriveFocus.Near3: CameraHandler.SetFocus(EDSDK.EvfDriveLens_Near3); break;
						}
					}
				}
				
				if (FInWB.IsChanged || isChanged)
				{
					if (FInWB[0] != WB.None)
					{
						switch (FInWB[0])
						{
							case WB.Auto: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Auto); break;
							case WB.Daylight: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Daylight); break;
							case WB.Cloudy: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Cloudy); break;
							case WB.Tangsten: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Tangsten); break;
							case WB.Fluorescent: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Fluorescent); break;
							case WB.Strobe: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Strobe); break;
							case WB.WhitePaper: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_WhitePaper); break;
							case WB.Shade: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Shade); break;
						}
					}
				}
				
				if (FInSaveTo.IsChanged || FInSavePath.IsChanged || isChanged)
				{
					if (FInSaveTo[0] == SaveTo.Camera)
					{
						CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Camera);
					}
					else
					{
						if (FInSaveTo[0] == SaveTo.Computer) CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Host);
						else if (FInSaveTo[0] == SaveTo.Both) CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
						CameraHandler.SetCapacity();
						
						try
						{
							Directory.CreateDirectory(FInSavePath[0]);
							CameraHandler.ImageSaveDirectory = FInSavePath[0];
						}
						catch(Exception e)
						{
							FOutError[0] = e.Message;
						}
					}
				}
				
				if (FInSavePath.IsChanged || isChanged)
				{
					CameraHandler.ImageSaveDirectory = FInSavePath[0];
					
				}
				
				if (FInTakeVideo.IsChanged || isChanged)
				{
					if (FInTakeVideo[0] && !CameraHandler.IsFilming)
					{
						if (FInSaveTo[0] == SaveTo.Computer || FInSaveTo[0] == SaveTo.Both)
						{
							Directory.CreateDirectory(FInSavePath[0]);
							CameraHandler.StartFilming(FInSavePath[0]);
						}
						else CameraHandler.StartFilming();
					}
					
					else if (!FInTakeVideo[0] && CameraHandler.IsFilming)
					{
						CameraHandler.StopFilming();
					}
				}
				
				if (FInForceFocus[0] || FInFocusMode.IsChanged || isChanged)
				{
					//CameraHandler.SetAFMode((uint)(FInFocusMode[0]-1));
					CameraHandler.SetAF(EDSDK.EdsEvfAf.CameraCommand_EvfAf_OFF);
					CameraHandler.SetSetting(EDSDK.PropID_Evf_AFMode, (uint)(FInFocusMode[0]));
					CameraHandler.SetAF(EDSDK.EdsEvfAf.CameraCommand_EvfAf_ON);
 				}
				
				if (FInTakePhoto[0])
				{
					if (FInTv[0].Name.Equals("Bulb")) CameraHandler.TakePhoto((uint)FInBulb[0]);
					else
					{
                        if (FInUseAutofocus[0]) // manual
						{
							CameraHandler.SetAF(EDSDK.EdsEvfAf.CameraCommand_EvfAf_OFF);
							CameraHandler.TakePhoto();
						}
						else // auto
						{
							CameraHandler.SetAF(EDSDK.EdsEvfAf.CameraCommand_EvfAf_OFF);
							CameraHandler.TakePhotoShutterButton();
						}
					}
				}
	
				if (FInLiveView.IsChanged || isChanged)
				{
					CameraHandler.SetAF(EDSDK.EdsEvfAf.CameraCommand_EvfAf_OFF);

					if (FInLiveView[0] && !CameraHandler.IsLiveViewOn) { FLogger.Log(LogType.Debug, "StartLiveView"); CameraHandler.StartLiveView(); }
					if (!FInLiveView[0] && CameraHandler.IsLiveViewOn) { FLogger.Log(LogType.Debug, "StopLiveView"); CameraHandler.StopLiveView(); }
				}
 				
				isChanged = false;
			}
			
			if (CameraHandler.Error != EDSDK.EDS_ERR_OK)
			{
				FOutError[0] = Enum.GetName(typeof(EDSDKError),0x081) + " (0x" + CameraHandler.Error.ToString("X") + ")";
			}
			
			FOutSaved[0] = saved;
			if (saved) saved = false;
			
			//FLogger.Log(LogType.Debug, "hi tty!");
		}
		
		public void Update(IPluginIO pin, FeralTic.DX11.DX11RenderContext context)
		{
			for (int i = 0; i < this.FTextureOutput.SliceCount; i++)
			{
				if (update == true)
				{
					// recreate texture if pin was disconnected
					if(!this.FTextureOutput[i].Contains(context))
					{
						//FLogger.Log(LogType.Debug, "recreate!");
						this.FTextureOutput[i][context] = new DX11DynamicTexture2D(context, bmp.Width, bmp.Height, SlimDX.DXGI.Format.B8G8R8A8_UNorm);
						this.FOutValid[i] = true;
					}
					
					lock (locker)
					{
						BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
						
						if (bmp.Width * 4 == this.FTextureOutput[i][context].GetRowPitch())
						{
							this.FTextureOutput[i][context].WriteData(data.Scan0, bmp.Width * bmp.Height * 4);
						}
						else
						{
							this.FTextureOutput[i][context].WriteDataPitch(data.Scan0, bmp.Width * bmp.Height * 4);
						}
						
						bmp.UnlockBits(data);
						bmp.Dispose();
						bmp = null;
						
						update = false;
					}
				}
			}
		}
		
		public void Destroy(IPluginIO pin, FeralTic.DX11.DX11RenderContext context, bool force)
		{
			FLogger.Log(LogType.Debug, "Destroy!");
			for (int i = 0; i < this.FTextureOutput.SliceCount; i++)
			{
				if (this.FTextureOutput[i] != null)
				{
					this.FTextureOutput[i].Dispose(context);
				}
			}
		}
		
		public void Dispose()
		{
			FLogger.Log(LogType.Debug, "Dispose!");
			for (int i = 0; i < this.FTextureOutput.SliceCount; i++)
			{
				if (this.FTextureOutput[i] != null)
				{
					this.FTextureOutput[i].Dispose();
				}
			}
			
			if (CameraHandler != null) CameraHandler.Dispose();
		}
		
		private void OpenSession()
		{
			if (FInDeviceID[0] >= 0)
			{
				Refresh();
				
				if (CamList.Count > 0)
				{
					CameraHandler.OpenSession(CamList[FInDeviceID[0]]);
					string cameraname = CameraHandler.MainCamera.Info.szDeviceDescription;
					
					FOutError[0] = (CameraHandler.GetSetting(EDSDK.PropID_AEMode) != EDSDK.AEMode_Manual) ? "Camera is not in manual mode. Some features might not work!" : "";
					
					FOutName.SliceCount = 1;
					FOutPort.SliceCount = 1;
					
					FOutName[0] = CamList[FInDeviceID[0]].Info.szDeviceDescription;
					FOutPort[0] = CamList[FInDeviceID[0]].Info.szPortName;
					
					TvList = CameraHandler.GetSettingsList((uint)EDSDK.PropID_Tv);
					AvList = CameraHandler.GetSettingsList((uint)EDSDK.PropID_Av);
					ISOList = CameraHandler.GetSettingsList((uint)EDSDK.PropID_ISOSpeed);
					
					List<string> tv = new List<string>();
					List<string> av = new List<string>();
					List<string> iso = new List<string>();
					
					tv.Add("None");
					av.Add("None");
					iso.Add("None");
					
					foreach (int Tv in TvList) tv.Add(CameraValues.TV((uint)Tv));
					foreach (int Av in AvList) av.Add(CameraValues.AV((uint)Av));
					foreach (int ISO in ISOList) iso.Add(CameraValues.ISO((uint)ISO));
					
					EnumManager.UpdateEnum("Tv", tv[0], tv.ToArray());
					EnumManager.UpdateEnum("Av", av[0], av.ToArray());
					EnumManager.UpdateEnum("ISO", iso[0], iso.ToArray());
					
					//isChanged = true;
				}
				else
				{
					FOutError[0] = "";
					
					FOutName.SliceCount = 0;
					FOutPort.SliceCount = 0;
				}
				
			}
			
		}
		
		private void CloseSession()
		{
			//if(FInFocusMode[0] != FocusMode.None) 
			//CameraHandler.SetAF(EDSDK.EdsEvfAf.CameraCommand_EvfAf_OFF);
        			
			//if( CameraHandler.IsLiveViewOn) CameraHandler.StopLiveView();
			CameraHandler.CloseSession();
		}
		
		private void Refresh()
		{
			//if (CameraHandler.CameraSessionOpen) CloseSession();
			//FLogger.Log(LogType.Debug, "Refresh ");
			
			CamList = CameraHandler.GetCameraList();
			FOutError[0] = "";
		}
		
		private void SDK_ProgressChanged(int Progress)
		{
			FLogger.Log(LogType.Debug, "Progress " + Progress);
			FOutProgress[0] = Progress;
			
			if (Progress == 100)
			{
				saved = true;
			}
		}
		
		private void SDK_LiveViewUpdated(Stream img)
		{
			lock (locker)
			{
				if (img.Length > 0)
				{
					if(bmp != null) bmp.Dispose(); // fix for not connected (S node without R)
					
					bmp = new Bitmap(img);
					update = true;
				}
			}
		}
		
		private void SDK_CameraAdded()
		{
			Refresh();
			// auto open session if enable
			
			if (FInEnable[0] && !CameraHandler.CameraSessionOpen) { FLogger.Log(LogType.Debug, "OpenSession"); OpenSession(); isChanged = true; };
			if (FInLiveView[0] && !CameraHandler.IsLiveViewOn) { FLogger.Log(LogType.Debug, "StartLiveView"); CameraHandler.StartLiveView(); }
		}
		
		private void SDK_CameraHasShutdown(object sender, EventArgs e)
		{
			FOutName.SliceCount = 0;
			FOutPort.SliceCount = 0;
			
			if (FInLiveView[0] && CameraHandler.IsLiveViewOn) { FLogger.Log(LogType.Debug, "StopLiveView"); CameraHandler.StopLiveView(); }
		}
		
		
		
	}
}
