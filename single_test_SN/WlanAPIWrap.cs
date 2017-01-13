using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;

public class Wlan
{
	//Native API Wrapper
	[DllImport("wlanapi.dll")]
	public static extern int WlanSetProfile(
		[In] IntPtr clientHandle,
		[In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceGuid,
		[In] WlanProfileFlags flags,
		[In, MarshalAs(UnmanagedType.LPWStr)] string profileXml,
		[In, Optional, MarshalAs(UnmanagedType.LPWStr)] string allUserProfileSecurity,
		[In] bool overwrite,
		[In] IntPtr pReserved,
		[Out] out WlanReasonCode reasonCode);

	/// <summary>
	/// This structure contains an array of NIC information
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WLAN_INTERFACE_INFO_LIST
	{
		public Int32 dwNumberofItems;
		public Int32 dwIndex;
		public WLAN_INTERFACE_INFO[] InterfaceInfo;

		public WLAN_INTERFACE_INFO_LIST(IntPtr pList)
		{
			// The first 4 bytes are the number of WLAN_INTERFACE_INFO structures.
			dwNumberofItems = Marshal.ReadInt32(pList, 0);

			// The next 4 bytes are the index of the current item in the unmanaged API.
			dwIndex = Marshal.ReadInt32(pList, 4);

			// Construct the array of WLAN_INTERFACE_INFO structures.
			InterfaceInfo = new WLAN_INTERFACE_INFO[dwNumberofItems];

			for (int i = 0; i < dwNumberofItems; i++)
			{
				// The offset of the array of structures is 8 bytes past the beginning. Then, take the index and multiply it by the number of bytes in the structure.
				// the length of the WLAN_INTERFACE_INFO structure is 532 bytes - this was determined by doing a sizeof(WLAN_INTERFACE_INFO) in an unmanaged C++ app.
				IntPtr pItemList = new IntPtr(pList.ToInt32() + (i * 532) + 8);

				// Construct the WLAN_INTERFACE_INFO structure, marshal the unmanaged structure into it, then copy it to the array of structures.
				WLAN_INTERFACE_INFO wii = new WLAN_INTERFACE_INFO();
				wii = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(pItemList, typeof(WLAN_INTERFACE_INFO));
				InterfaceInfo[i] = wii;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WLAN_INTERFACE_INFO
	{
		/// GUID->_GUID
		public Guid InterfaceGuid;

		/// WCHAR[256]
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string strInterfaceDescription;

		/// WLAN_INTERFACE_STATE->_WLAN_INTERFACE_STATE
		public WLAN_INTERFACE_STATE isState;
	}

	/// <summary>
	/// Interface state enums
	/// </summary>
	public enum WLAN_INTERFACE_STATE : int
	{
		wlan_interface_state_not_ready = 0,
		wlan_interface_state_connected,
		wlan_interface_state_ad_hoc_network_formed,
		wlan_interface_state_disconnecting,
		wlan_interface_state_disconnected,
		wlan_interface_state_associating,
		wlan_interface_state_discovering,
		wlan_interface_state_authenticating
	};

	[DllImport("wlanapi.dll")]
	public static extern uint WlanConnect(
		[In] IntPtr clientHandle,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceGuid,
        [In] ref WlanConnectionParameters connectionParameters, 
		IntPtr pReserved);


	/// <summary>
	/// Specifies the parameters used when using the <see cref="WlanConnect"/> function.
	/// </summary>
	/// <remarks>
	/// Corresponds to the native <c>WLAN_CONNECTION_PARAMETERS</c> type.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	public struct WlanConnectionParameters
	{
		/// <summary>
		/// Specifies the mode of connection.
		/// </summary>
		public WlanConnectionMode wlanConnectionMode;
		/// <summary>
		/// Specifies the profile being used for the connection.
		/// The contents of the field depend on the <see cref="wlanConnectionMode"/>:
		/// <list type="table">
		/// <listheader>
		/// <term>Value of <see cref="wlanConnectionMode"/></term>
		/// <description>Contents of the profile string</description>
		/// </listheader>
		/// <item>
		/// <term><see cref="WlanConnectionMode.Profile"/></term>
		/// <description>The name of the profile used for the connection.</description>
		/// </item>
		/// <item>
		/// <term><see cref="WlanConnectionMode.TemporaryProfile"/></term>
		/// <description>The XML representation of the profile used for the connection.</description>
		/// </item>
		/// <item>
		/// <term><see cref="WlanConnectionMode.DiscoverySecure"/>, <see cref="WlanConnectionMode.DiscoveryUnsecure"/> or <see cref="WlanConnectionMode.Auto"/></term>
		/// <description><c>null</c></description>
		/// </item>
		/// </list>
		/// </summary>
		[MarshalAs(UnmanagedType.LPWStr)]
		public string profile;
		/// <summary>
		/// Pointer to a <see cref="Dot11Ssid"/> structure that specifies the SSID of the network to connect to.
		/// This field is optional. When set to <c>null</c>, all SSIDs in the profile will be tried.
		/// This field must not be <c>null</c> if <see cref="wlanConnectionMode"/> is set to <see cref="WlanConnectionMode.DiscoverySecure"/> or <see cref="WlanConnectionMode.DiscoveryUnsecure"/>.
		/// </summary>
		public IntPtr dot11SsidPtr;
		/// <summary>
		/// Pointer to a <c>Dot11BssidList</c> structure that contains the list of basic service set (BSS) identifiers desired for the connection.
		/// </summary>
		/// <remarks>
		/// On Windows XP SP2, must be set to <c>null</c>.
		/// </remarks>
		public IntPtr desiredBssidListPtr;
		/// <summary>
		/// A <see cref="Dot11BssType"/> value that indicates the BSS type of the network. If a profile is provided, this BSS type must be the same as the one in the profile.
		/// </summary>
		public Dot11BssType dot11BssType;
		/// <summary>
		/// Specifies ocnnection parameters.
		/// </summary>
		/// <remarks>
		/// On Windows XP SP2, must be set to 0.
		/// </remarks>
		public WlanConnectionFlags flags;
	}
	/// <summary>
	///get NIC state  
	/// </summary>
	private static string getStateDescription(WLAN_INTERFACE_STATE state)
	{
		string stateDescription=string.Empty;
		switch (state)
		{
			case WLAN_INTERFACE_STATE.wlan_interface_state_not_ready:
				stateDescription = "not ready to operate";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_connected:
				stateDescription = "connected";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_ad_hoc_network_formed:
				stateDescription = "first node in an adhoc network";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_disconnecting:
				stateDescription = "disconnecting";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_disconnected:
				stateDescription = "disconnected";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_associating:
				stateDescription = "associating";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_discovering:
				stateDescription = "discovering";
				break;
			case WLAN_INTERFACE_STATE.wlan_interface_state_authenticating:
				stateDescription = "authenticating";
				break;
		}

		return stateDescription;
	}

	/// <summary>
	/// Get list of all available wifi network.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WLAN_AVAILABLE_NETWORK_LIST
	{

		public UInt32 dwNumberOfItems;
		public UInt32 dwIndex;
		public WLAN_AVAILABLE_NETWORK[] Networks;

		public WLAN_AVAILABLE_NETWORK_LIST(IntPtr list)
		{
			dwNumberOfItems = (UInt32)Marshal.ReadInt32(list, 0);
			dwIndex = (UInt32)Marshal.ReadInt32(list, 4);
			Networks = new WLAN_AVAILABLE_NETWORK[dwNumberOfItems];
			try
			{
				for (int index = 0; index < dwNumberOfItems; index++)
				{
					Networks[index].strProfileName = Marshal.PtrToStringUni(new IntPtr(list.ToInt32() + 8));
					Networks[index].dot11Ssid = new DOT11_SSID(new IntPtr(list.ToInt32() + 520));
					Networks[index].dot11BssType = (DOT11_BSS_TYPE)Marshal.ReadInt32(list, 556);
					Networks[index].uNumberOfBssids = (UInt32)Marshal.ReadInt32(list, 560);
					Networks[index].bNetworkConnectable = (Marshal.ReadByte(list, 564) == 1 ? true : false);
					Networks[index].wlanNotConnectableReason = (UInt32)Marshal.ReadInt32(list, 568);
					Networks[index].uNumberOfPhyTypes = (UInt32)Marshal.ReadInt32(list, 572);
					Networks[index].bMorePhyTypes = (Marshal.ReadByte(list, 608) == 1 ? true : false);
					Networks[index].wlanSignalQuality =  (UInt32)Marshal.ReadInt32(list, 612);
					Networks[index].bSecurityEnabled = (Marshal.ReadByte(list, 616) == 1 ? true : false);
					Networks[index].dot11DefaultAuthAlgorithm = (DOT11_AUTH_ALGORITHM)Marshal.ReadInt32(list, 620);
					Networks[index].dot11DefaultCipherAlgorithm = (DOT11_CIPHER_ALGORITHM)Marshal.ReadInt32(list, 624);
					Networks[index].dwFlags = (UInt32)Marshal.ReadInt32(list, 628);
					Networks[index].dwReserved = (UInt32)Marshal.ReadInt32(list, 632);
					UInt32 kk = (UInt32)Marshal.ReadInt32(list, 636);

					list = new IntPtr(list.ToInt32() + 632 - 4);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}
	}


	/// <summary>
	/// Struct Availabel Wifi Network.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WLAN_AVAILABLE_NETWORK // size 628 in byte
	{
		[MarshalAs(UnmanagedType.LPWStr, SizeConst = 256)]
		public string strProfileName; // size 512
		public DOT11_SSID dot11Ssid; // size 36
		public DOT11_BSS_TYPE dot11BssType; // size 4
		public UInt32 uNumberOfBssids; // 4
		public bool bNetworkConnectable; // 4
		public UInt32 wlanNotConnectableReason; // 4
		public UInt32 uNumberOfPhyTypes; // 4
		public DOT11_PHY_TYPE[] dot11PhyTypes; // max is 8, size is 8*4 = 32
		public bool bMorePhyTypes;// 4
		public UInt32 wlanSignalQuality; // see doc, size 4
		public bool bSecurityEnabled;// 4
		public DOT11_AUTH_ALGORITHM dot11DefaultAuthAlgorithm;// 4
		public DOT11_CIPHER_ALGORITHM dot11DefaultCipherAlgorithm;// 4
		public UInt32 dwFlags;// 4
		public UInt32 dwReserved;// 4
	}


	[StructLayout(LayoutKind.Sequential)]
	public struct DOT11_SSID // size 36
	{
		public UInt32 uSSIDLength; // size 4
		public char[] ucSSID;  // size is 32*1

		public DOT11_SSID(IntPtr p)
		{
			uSSIDLength = System.Convert.ToUInt32(Marshal.ReadInt32(p, 0));
			ucSSID = new char[32];
			//if (uSSIDLength <= 32)
			//{
			for (int i = 0; i < 32; i++)
			{
				char c = (char)Marshal.ReadByte(p, 4 + i);
				ucSSID[i] =  (char)c;
			}
			//}
		}
	}



	/// <summary>
	/// Represents an 802.11 Basic Service Set type
	/// </summary>
	public enum DOT11_BSS_TYPE
	{
		///<summary>
		/// dot11_BSS_type_infrastructure -> 1
		///</summary>
		dot11_BSS_type_infrastructure = 1,

		///<summary>
		/// dot11_BSS_type_independent -> 2
		///</summary>
		dot11_BSS_type_independent = 2,

		///<summary>
		/// dot11_BSS_type_any -> 3
		///</summary>
		dot11_BSS_type_any = 3,
	}

	public enum DOT11_PHY_TYPE
	{
		dot11_phy_type_unknown,
		dot11_phy_type_any,
		dot11_phy_type_fhss,
		dot11_phy_type_dsss,
		dot11_phy_type_irbaseband,
		dot11_phy_type_ofdm,
		dot11_phy_type_hrdsss,
		dot11_phy_type_erp,
		dot11_phy_type_ht,
		dot11_phy_type_IHV_start,
		dot11_phy_type_IHV_end,
	}
		
	public enum DOT11_AUTH_ALGORITHM
	{

		/// DOT11_AUTH_ALGO_80211_OPEN -> 1
		DOT11_AUTH_ALGO_80211_OPEN = 1,
		/// DOT11_AUTH_ALGO_80211_SHARED_KEY -> 2
		DOT11_AUTH_ALGO_80211_SHARED_KEY = 2,
		/// DOT11_AUTH_ALGO_WPA -> 3
		DOT11_AUTH_ALGO_WPA = 3,
		/// DOT11_AUTH_ALGO_WPA_PSK -> 4
		DOT11_AUTH_ALGO_WPA_PSK = 4,
		/// DOT11_AUTH_ALGO_WPA_NONE -> 5
		DOT11_AUTH_ALGO_WPA_NONE = 5,
		/// DOT11_AUTH_ALGO_RSNA -> 6
		DOT11_AUTH_ALGO_RSNA = 6,
		/// DOT11_AUTH_ALGO_RSNA_PSK -> 7
		DOT11_AUTH_ALGO_RSNA_PSK = 7,
		/// DOT11_AUTH_ALGO_IHV_START -> 0x80000000
		DOT11_AUTH_ALGO_IHV_START = -2147483648,
		/// DOT11_AUTH_ALGO_IHV_END -> 0xffffffff
		DOT11_AUTH_ALGO_IHV_END = -1,
	}

	public enum DOT11_CIPHER_ALGORITHM
	{
		/// DOT11_CIPHER_ALGO_NONE -> 0x00
		DOT11_CIPHER_ALGO_NONE = 0,
		/// DOT11_CIPHER_ALGO_WEP40 -> 0x01
		DOT11_CIPHER_ALGO_WEP40 = 1,
		/// DOT11_CIPHER_ALGO_TKIP -> 0x02
		DOT11_CIPHER_ALGO_TKIP = 2,
		/// DOT11_CIPHER_ALGO_CCMP -> 0x04
		DOT11_CIPHER_ALGO_CCMP = 4,
		/// DOT11_CIPHER_ALGO_WEP104 -> 0x05
		DOT11_CIPHER_ALGO_WEP104 = 5,
		/// DOT11_CIPHER_ALGO_WPA_USE_GROUP -> 0x100
		DOT11_CIPHER_ALGO_WPA_USE_GROUP = 256,
		/// DOT11_CIPHER_ALGO_RSN_USE_GROUP -> 0x100
		DOT11_CIPHER_ALGO_RSN_USE_GROUP = 256,
		/// DOT11_CIPHER_ALGO_WEP -> 0x101
		DOT11_CIPHER_ALGO_WEP = 257,
		/// DOT11_CIPHER_ALGO_IHV_START -> 0x80000000
		DOT11_CIPHER_ALGO_IHV_START = -2147483648,
		/// DOT11_CIPHER_ALGO_IHV_END -> 0xffffffff
		DOT11_CIPHER_ALGO_IHV_END = -1,
	}
	
	[DllImport("wlanapi.dll")]
	public static extern int WlanCloseHandle(    
		[In(), Out()]
		IntPtr clientHandle,     
		[In(), Out()]
		IntPtr pReserved)
	;
	
    //[DllImport("wlanapi.dll")]
    //public static extern int WlanDeleteProfile(
    //    [In] IntPtr clientHandle,
    //    [In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceGuid,  
    //    [In()] string profileName,
    //    [In, Out] IntPtr reservedPtr)
    //;

    [DllImport("wlanapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int WlanDeleteProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved);

	[DllImport("wlanapi.dll")]
	public static extern int WlanEnumInterfaces(    
		[In(), Out()]
		IntPtr clientHandle,    
		[In(), Out()]
		IntPtr pReserved,     
		[In(), Out()]
		ref IntPtr ppInterfaceList)
	;

	[DllImport("wlanapi.dll")]
	public static extern void WlanFreeMemory(IntPtr pMemory)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanGetAvailableNetworkList(
		[In] IntPtr clientHandle,
		[In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceGuid,
		[In] WlanGetAvailableNetworkFlags flags,
		[In, Out] IntPtr reservedPtr,
		[Out] out IntPtr availableNetworkListPtr);

	[DllImport("wlanapi.dll")]
	public static extern int WlanGetNetworkBssList( 
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr clientHandle,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		Guid interfaceGuid,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr dot11SsidInt,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		Dot11BssType dot11BssType,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		bool securityEnabled,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr reservedPtr,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		ref IntPtr wlanBssList)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanGetProfile(    
		[In(), MarshalAs(UnmanagedType.LPStruct)]	
		[Out()]
		IntPtr clientHandle,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]	
		[Out()]
		Guid interfaceGuid,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		string profileName,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr pReserved,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		ref IntPtr profileXml,  
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		// ERROR: Unsupported modifier : Ref, Optional
		WlanProfileFlags flags,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		// ERROR: Unsupported modifier : Ref, Optional
		WlanAccess grantedAccess)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanGetProfileList(    
		[In()] IntPtr clientHandle, 
    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		Guid interfaceGuid,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr pReserved,
		
		[Out()]
		string profileList)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanOpenHandle(    
		[In(), Out()]
		
		UInt32 clientVersion,     
		[In(), Out()]
		
		IntPtr pReserved,     
		[In(), Out()]
		
		ref UInt32 negotiatedVersion,    
		[In(), Out()]
		
		ref IntPtr clientHandle)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanQueryInterface(    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr clientHandle,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		Guid interfaceGuid,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		WlanIntfOpcode opCode,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		IntPtr pReserved,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		ref int dataSize,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		ref IntPtr ppData,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		[Out()]
		ref WlanOpcodeValueType wlanOpcodeValueType)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanReasonCodeToString(    
		
		[In(), Out()]
		WlanReasonCode reasonCode,    
		
		[In(), Out()]
		int bufferSize,    
		
		[In(), Out()]
		StringBuilder stringBuffer,   
		[In(), Out()]
		IntPtr pReserved)
	;


	/// <summary>
	/// Contains information provided when registering for notifications.
	/// </summary>
	/// <remarks>
	/// Corresponds to the native <c>WLAN_NOTIFICATION_DATA</c> type.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	public struct WLAN_NOTIFICATION_DATA
	{
		/// <summary>
		/// Specifies where the notification comes from.
		/// </summary>
		/// <remarks>
		/// On Windows XP SP2, this field must be set to <see cref="WlanNotificationSource.None"/>, <see cref="WlanNotificationSource.All"/> or <see cref="WlanNotificationSource.ACM"/>.
		/// </remarks>
		public WLAN_NOTIFICATION_SOURCE notificationSource;
		/// <summary>
		/// Indicates the type of notification. The value of this field indicates what type of associated data will be present in <see cref="dataPtr"/>.
		/// </summary>
		public int notificationCode;
		/// <summary>
		/// Indicates which interface the notification is for.
		/// </summary>
		public Guid interfaceGuid;
		/// <summary>
		/// Specifies the size of <see cref="dataPtr"/>, in bytes.
		/// </summary>
		public int dataSize;
		/// <summary>
		/// Pointer to additional data needed for the notification, as indicated by <see cref="notificationCode"/>.
		/// </summary>
		public IntPtr dataPtr;

		/// <summary>
		/// Gets the notification code (in the correct enumeration type) according to the notification source.
		/// </summary>
		public object NotificationCode
		{
			get
			{
				if (notificationSource == WLAN_NOTIFICATION_SOURCE.MSM)
					return notificationCode;
				else if (notificationSource == WLAN_NOTIFICATION_SOURCE.ACM)
					return notificationCode;
				else
					return notificationCode;
			}

		}
	} 

	[Flags]
	public enum WLAN_NOTIFICATION_SOURCE : uint
	{
		None = 0,
		/// <summary>
		/// All notifications, including those generated by the 802.1X module.
		/// </summary>
		All = 0X0000FFFF,
		/// <summary>
		/// Notifications generated by the auto configuration module.
		/// </summary>
		ACM = 0X00000008,
		/// <summary>
		/// Notifications generated by MSM.
		/// </summary>
		MSM = 0X00000010,
		/// <summary>
		/// Notifications generated by the security module.
		/// </summary>
		Security = 0X00000020,
		/// <summary>
		/// Notifications generated by independent hardware vendors (IHV).
		/// </summary>
		IHV = 0X00000040
	} 


	public delegate void WLAN_NOTIFICATION_CALLBACK(ref WLAN_NOTIFICATION_DATA notificationData, IntPtr context);
	[DllImport("Wlanapi.dll", EntryPoint = "WlanRegisterNotification")]
	public static extern uint WlanRegisterNotification(
		IntPtr hClientHandle, 
		WLAN_NOTIFICATION_SOURCE dwNotifSource,
		bool bIgnoreDuplicate,
		WLAN_NOTIFICATION_CALLBACK funcCallback,
		IntPtr pCallbackContext, 
		IntPtr pReserved,
		[Out] out WLAN_NOTIFICATION_SOURCE pdwPrevNotifSource);


	[DllImport("wlanapi.dll")]
	public static extern int WlanScan(
        [In] IntPtr clientHandle,
 
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		Guid interfaceGuid,

        [In] IntPtr pDot11Ssid,

        [In] IntPtr pIeData,

        [In] IntPtr pReserved)
	;
	[DllImport("wlanapi.dll")]
	public static extern int WlanSetInterface(    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		IntPtr clientHandle,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		Guid interfaceGuid,    
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		WlanIntfOpcode opCode,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		UInt32 dataSize,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		IntPtr pData,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		
		IntPtr pReserved)
	;

	[DllImport("wlanapi.dll")]
	public static extern int WlanDisconnect( 
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		[Out()]
		IntPtr clientHandle,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		[Out()]
		Guid interfaceGuid,     
		[In(), MarshalAs(UnmanagedType.LPStruct)]
		[Out()]
		IntPtr pReserved)
	;


	// Fields
	public const UInt32 WLAN_CLIENT_VERSION_LONGHORN = 2;
	public const UInt32 WLAN_CLIENT_VERSION_XP_SP2 = 1;

	// Nested Types
	public enum Dot11AuthAlgorithm : int
	{
		IEEE80211_Open = 1,
		IEEE80211_SharedKey = 2,
		IHV_End = -1,
		IHV_Start = -2147483648,
		RSNA = 6,
		RSNA_PSK = 7,
		WPA = 3,
		WPA_None = 5,
		WPA_PSK = 4
	}

	public enum Dot11BssType
	{
		Any = 3,
		Independent = 2,
		Infrastructure = 1
	}

	public enum Dot11CipherAlgorithm : int
	{
		CCMP = 4,
		IHV_End = -1,
		IHV_Start = -2147483648,
		None = 0,
		RSN_UseGroup = 256,
		TKIP = 2,
		WEP = 257,
		WEP104 = 5,
		WEP40 = 1,
		WPA_UseGroup = 256
	}

	[Flags()]
	public enum Dot11OperationMode : int
	{
		AP = 2,
		ExtensibleStation = 4,
		NetworkMonitor = -2147483648,
		Station = 1,
		Unknown = 0
	}

	public enum Dot11PhyType : int
	{
		Any = 0,
		DSSS = 2,
		ERP = 6,
		FHSS = 1,
		HRDSSS = 5,
		IHV_End = -1,
		IHV_Start = -2147483648,
		IrBaseband = 3,
		OFDM = 4,
		Unknown = 0
	}

	[StructLayout(LayoutKind.Sequential)]
		public struct Dot11Ssid
	{
		public UInt32 SSIDLength;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] SSID;
	}

	[Flags()]
		public enum WlanAccess
	{
		ExecuteAccess = 131105,
		ReadAccess = 131073,
		WriteAccess = 458787
	}

	public enum WlanAdhocNetworkState
	{
		Connected = 1,
		Formed = 0
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WlanAssociationAttributes
	{
		public Dot11Ssid dot11Ssid;
		public Dot11BssType dot11BssType;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] dot11Bssid;
		public Dot11PhyType dot11PhyType;
		public UInt32 dot11PhyIndex;
		public UInt32 wlanSignalQuality;
		public UInt32 rxRate;
		public UInt32 txRate;
	}

	/// <summary>
	/// Contains information about an available wireless network.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
	public struct WlanAvailableNetwork
	{
		/// <summary>
		/// Contains the profile name associated with the network.
		/// If the network doesn't have a profile, this member will be empty.
		/// If multiple profiles are associated with the network, there will be multiple entries with the same SSID in the visible network list. Profile names are case-sensitive.
		/// </summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string profileName;
		/// <summary>
		/// Contains the SSID of the visible wireless network.
		/// </summary>
		public Dot11Ssid dot11Ssid;
		/// <summary>
		/// Specifies whether the network is an infrastructure or an ad-hoc one.
		/// </summary>
		public Dot11BssType dot11BssType;
		/// <summary>
		/// Indicates the number of BSSIDs in the network.
		/// </summary>
		public uint numberOfBssids;
		/// <summary>
		/// Indicates whether the network is connectable.
		/// </summary>
		public bool networkConnectable;
		/// <summary>
		/// Indicates why a network cannot be connected to. This member is only valid when <see cref="networkConnectable"/> is <c>false</c>.
		/// </summary>
		public WlanReasonCode wlanNotConnectableReason;
		/// <summary>
		/// The number of PHY types supported on available networks.
		/// The maximum value of this field is 8. If more than 8 PHY types are supported, <see cref="morePhyTypes"/> must be set to <c>true</c>.
		/// </summary>
		private uint numberOfPhyTypes;
		/// <summary>
		/// Contains an array of <see cref="Dot11PhyType"/> values that represent the PHY types supported by the available networks.
		/// When <see cref="numberOfPhyTypes"/> is greater than 8, this array contains only the first 8 PHY types.
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		private Dot11PhyType[] dot11PhyTypes;
		/// <summary>
		/// Gets the <see cref="Dot11PhyType"/> values that represent the PHY types supported by the available networks.
		/// </summary>
		public Dot11PhyType[] Dot11PhyTypes
		{
			get
			{
				Dot11PhyType[] ret = new Dot11PhyType[numberOfPhyTypes];
				Array.Copy(dot11PhyTypes, ret, numberOfPhyTypes);
				return ret;
			}
		}
		/// <summary>
		/// Specifies if there are more than 8 PHY types supported.
		/// When this member is set to <c>true</c>, an application must call <see cref="WlanClient.WlanInterface.GetNetworkBssList"/> to get the complete list of PHY types.
		/// <see cref="WlanBssEntry.phyId"/> contains the PHY type for an entry.
		/// </summary>
		public bool morePhyTypes;
		/// <summary>
		/// A percentage value that represents the signal quality of the network.
		/// This field contains a value between 0 and 100.
		/// A value of 0 implies an actual RSSI signal strength of -100 dbm.
		/// A value of 100 implies an actual RSSI signal strength of -50 dbm.
		/// You can calculate the RSSI signal strength value for values between 1 and 99 using linear interpolation.
		/// </summary>
		public uint wlanSignalQuality;
		/// <summary>
		/// Indicates whether security is enabled on the network.
		/// </summary>
		public bool securityEnabled;
		/// <summary>
		/// Indicates the default authentication algorithm used to join this network for the first time.
		/// </summary>
		public Dot11AuthAlgorithm dot11DefaultAuthAlgorithm;
		/// <summary>
		/// Indicates the default cipher algorithm to be used when joining this network.
		/// </summary>
		public Dot11CipherAlgorithm dot11DefaultCipherAlgorithm;
		/// <summary>
		/// Contains various flags specifying characteristics of the available network.
		/// </summary>
		public WlanAvailableNetworkFlags flags;
		/// <summary>
		/// Reserved for future use. Must be set to NULL.
		/// </summary>
		uint reserved;
	}


	[Flags()]
	public enum WlanAvailableNetworkFlags
	{
		Connected = 1,
		HasProfile = 2
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WlanAvailableNetworkListHeader
	{
		public UInt32 numberOfItems;
		public UInt32 index;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WlanBssEntry
	{
		public Dot11Ssid dot11Ssid;
		public UInt32 phyId;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] dot11Bssid;
		public Dot11BssType dot11BssType;
		public Dot11PhyType dot11BssPhyType;
		public int rssi;
		public UInt32 linkQuality;
		public bool inRegDomain;
		public UInt16 beaconPeriod;
		public UInt64 timestamp;
		public UInt64 hostTimestamp;
		public UInt16 capabilityInformation;
		public UInt32 chCenterFrequency;
		public WlanRateSet wlanRateSet;
		public UInt32 ieOffset;
		public UInt32 ieSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WlanBssListHeader
	{
		internal UInt32 totalSize;
		internal UInt32 numberOfItems;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WlanConnectionAttributes
	{
		public WlanInterfaceState isState;
		public WlanConnectionMode wlanConnectionMode;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string profileName;
		public WlanAssociationAttributes wlanAssociationAttributes;
		public WlanSecurityAttributes wlanSecurityAttributes;
	}

	[Flags()]
	public enum WlanConnectionFlags
	{
		AdhocJoinOnly = 2,
		EapolPassthrough = 8,
		HiddenNetwork = 1,
		IgnorePrivacyBit = 4
	}

	public enum WlanConnectionMode
	{
		Auto = 4,
		DiscoverySecure = 2,
		DiscoveryUnsecure = 3,
		Invalid = 5,
		Profile = 0,
		TemporaryProfile = 1
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WlanConnectionNotificationData
	{
		public WlanConnectionMode wlanConnectionMode;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string profileName;
		public Dot11Ssid dot11Ssid;
		public Dot11BssType dot11BssType;
		public bool securityEnabled;
		public WlanReasonCode wlanReasonCode;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
		public string profileXml;
	}
	
	[Flags()]
	public enum WlanGetAvailableNetworkFlags
	{
		IncludeAllAdhocProfiles = 1,
		IncludeAllManualHiddenProfiles = 2
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WlanInterfaceInfo
	{
		public Guid interfaceGuid;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string interfaceDescription;
		public WlanInterfaceState isState;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WlanInterfaceInfoListHeader
	{
		public UInt32 numberOfItems;
		public UInt32 index;
	}

	public enum WlanInterfaceState
	{
		AdHocNetworkFormed = 2,
		Associating = 5,
		Authenticating = 7,
		Connected = 1,
		Disconnected = 4,
		Disconnecting = 3,
		Discovering = 6,
		NotReady = 0
	}

	public enum WlanIntfOpcode
	{
		AutoconfEnabled = 1,
		BackgroundScanEnabled = 2,
		BssType = 5,
		ChannelNumber = 8,
		CurrentConnection = 7,
		CurrentOperationMode = 12,
		IhvEnd = 1073741823,
		IhvStart = 805306368,
		InterfaceState = 6,
		MediaStreamingMode = 3,
		RadioState = 4,
		RSSI = 268435714,
		SecurityEnd = 805306367,
		SecurityStart = 536936448,
		Statistics = 268435713,
		SupportedAdhocAuthCipherPairs = 10,
		SupportedCountryOrRegionStringList = 11,
		SupportedInfrastructureAuthCipherPairs = 9
	}

	public delegate void WlanNotificationCallbackDelegate(
		ref WlanNotificationData notificationData, 
		IntPtr context
		);

	public enum WlanNotificationCodeAcm
	{
		AdhocNetworkStateChange = 22,
		AutoconfDisabled = 2,
		AutoconfEnabled = 1,
		BackgroundScanDisabled = 4,
		BackgroundScanEnabled = 3,
		BssTypeChange = 5,
		ConnectionAttemptFail = 11,
		ConnectionComplete = 10,
		ConnectionStart = 9,
		Disconnected = 21,
		Disconnecting = 20,
		FilterListChange = 12,
		InterfaceArrival = 13,
		InterfaceRemoval = 14,
		NetworkAvailable = 19,
		NetworkNotAvailable = 18,
		PowerSettingChange = 6,
		ProfileChange = 15,
		ProfileNameChange = 16,
		ProfilesExhausted = 17,
		ScanComplete = 7,
		ScanFail = 8
	}

	public enum WlanNotificationCodeMsm
	{
		AdapterOperationModeChange = 14,
		AdapterRemoval = 13,
		Associated = 2,
		Associating = 1,
		Authenticating = 3,
		Connected = 4,
		Disassociating = 9,
		Disconnected = 10,
		PeerJoin = 11,
		PeerLeave = 12,
		RadioStateChange = 7,
		RoamingEnd = 6,
		RoamingStart = 5,
		SignalQualityChange = 8
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WlanNotificationData
	{
		public WlanNotificationSource notificationSource;
		public int notificationCode;
		public Guid interfaceGuid;
		public int dataSize;
		public IntPtr dataPtr;
		public object NotificationCode2 
		{
			get 
			{
				if ((this.notificationSource == WlanNotificationSource.MSM))
				{
					return (WlanNotificationCodeMsm)this.notificationCode;
				}
				if ((this.notificationSource == WlanNotificationSource.ACM))
				{
					return (WlanNotificationCodeAcm)this.notificationCode;
				}
				return this.notificationCode;
			}
		}

	}

	[Flags()]
	public enum WlanNotificationSource
	{
		ACM = 8,
		All = 65535,
		IHV = 64,
		MSM = 16,
		None = 0,
		Security = 32
	}

	public enum WlanOpcodeValueType
	{
		Invalid = 3,
		QueryOnly = 0,
		SetByGroupPolicy = 1,
		SetByUser = 2
	}

	[Flags()]
	public enum WlanProfileFlags
	{
		AllUser = 0,
		GroupPolicy = 1,
		User = 2
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WlanProfileInfo
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string profileName;
		public WlanProfileFlags profileFlags;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct WlanProfileInfoListHeader
	{
		public UInt32 numberOfItems;
		public UInt32 index;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WlanRateSet
	{
		/// <summary>
		/// The length, in bytes, of <see cref="rateSet"/>.
		/// </summary>
		private uint rateSetLength;
		/// <summary>
		/// An array of supported data transfer rates.
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)]
		private ushort[] rateSet;

		/// <summary>
		/// Gets an array of supported data transfer rates.
		/// If the rate is a basic rate, the first bit of the rate value is set to 1.
		/// A basic rate is the data transfer rate that all stations in a basic service set (BSS) can use to receive frames from the wireless medium.
		/// </summary>
		public ushort[] Rates
		{
			get
			{
				ushort[] rates = new ushort[rateSetLength / Marshal.SizeOf(System.Type.GetType("ushort"))];
				Array.Copy(rateSet, rates, rates.Length);
				return rates;
			}
		}

		/// <summary>
		/// Calculates the data transfer rate in mbit/s for a supported rate.
		/// </summary>
		/// <param name="rateIndex">The WLAN rate index (0-based).</param>
		/// <returns>The data transfer rate in mbit/s.</returns>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <param name="rateIndex"/> does not specify an existing rate.</exception>
		public double GetRateInMbps(int rateIndex)
		{
			if ((rateIndex < 0) || (rateIndex > rateSet.Length))
				throw new ArgumentOutOfRangeException("rateIndex");

			return (rateSet[rateIndex] & 0x7FFF) * 0.5;
		}
	}


	public enum WlanReasonCode
	{
		AC_BASE = 131072,
		AC_CONNECT_BASE = 163840,
		AC_END = 196607,
		ADHOC_SECURITY_FAILURE = 229386,
		ASSOCIATION_FAILURE = 229378,
		ASSOCIATION_TIMEOUT = 229379,
		AUTO_SWITCH_SET_FOR_ADHOC = 524304,
		AUTO_SWITCH_SET_FOR_MANUAL_CONNECTION = 524305,
		BASE = 131072,
		BSS_TYPE_NOT_ALLOWED = 163845,
		BSS_TYPE_UNMATCH = 196611,
		CONFLICT_SECURITY = 524299,
		CONNECT_CALL_FAIL = 163849,
		DATARATE_UNMATCH = 196613,
		DISCONNECT_TIMEOUT = 229391,
		DRIVER_DISCONNECTED = 229387,
		DRIVER_OPERATION_FAILURE = 229388,
		GP_DENIED = 163843,
		IHV_NOT_AVAILABLE = 229389,
		IHV_NOT_RESPONDING = 229390,
		IHV_OUI_MISMATCH = 524296,
		IHV_OUI_MISSING = 524297,
		IHV_SECURITY_NOT_SUPPORTED = 524295,
		IHV_SECURITY_ONEX_MISSING = 524306,
		IHV_SETTINGS_MISSING = 524298,
		IN_BLOCKED_LIST = 163847,
		IN_FAILED_LIST = 163846,
		INTERNAL_FAILURE = 229392,
		INVALID_ADHOC_CONNECTION_MODE = 524302,
		INVALID_BSS_TYPE = 524301,
		INVALID_PHY_TYPE = 524293,
		INVALID_PROFILE_NAME = 524291,
		INVALID_PROFILE_SCHEMA = 524289,
		INVALID_PROFILE_TYPE = 524292,
		KEY_MISMATCH = 163853,
		MSM_BASE = 196608,
		MSM_CONNECT_BASE = 229376,
		MSM_END = 262143,
		MSM_SECURITY_MISSING = 524294,
		MSMSEC_AUTH_START_TIMEOUT = 294914,
		MSMSEC_AUTH_SUCCESS_TIMEOUT = 294915,
		MSMSEC_BASE = 262144,
		MSMSEC_CANCELLED = 294929,
		MSMSEC_CAPABILITY_DISCOVERY = 262165,
		MSMSEC_CAPABILITY_NETWORK = 262162,
		MSMSEC_CAPABILITY_NIC = 262163,
		MSMSEC_CAPABILITY_PROFILE = 262164,
		MSMSEC_CAPABILITY_PROFILE_AUTH = 262174,
		MSMSEC_CAPABILITY_PROFILE_CIPHER = 262175,
		MSMSEC_CONNECT_BASE = 294912,
		MSMSEC_DOWNGRADE_DETECTED = 294931,
		MSMSEC_END = 327679,
		MSMSEC_FORCED_FAILURE = 294933,
		MSMSEC_G1_MISSING_GRP_KEY = 294925,
		MSMSEC_G1_MISSING_KEY_DATA = 294924,
		MSMSEC_KEY_FORMAT = 294930,
		MSMSEC_KEY_START_TIMEOUT = 294916,
		MSMSEC_KEY_SUCCESS_TIMEOUT = 294917,
		MSMSEC_M3_MISSING_GRP_KEY = 294920,
		MSMSEC_M3_MISSING_IE = 294919,
		MSMSEC_M3_MISSING_KEY_DATA = 294918,
		MSMSEC_MAX = 327679,
		MSMSEC_MIN = 262144,
		MSMSEC_MIXED_CELL = 262169,
		MSMSEC_NIC_FAILURE = 294928,
		MSMSEC_NO_AUTHENTICATOR = 294927,
		MSMSEC_NO_PAIRWISE_KEY = 294923,
		MSMSEC_PEER_INDICATED_INSECURE = 294926,
		MSMSEC_PR_IE_MATCHING = 294921,
		MSMSEC_PROFILE_AUTH_TIMERS_INVALID = 262170,
		MSMSEC_PROFILE_DUPLICATE_AUTH_CIPHER = 262151,
		MSMSEC_PROFILE_INVALID_AUTH_CIPHER = 262153,
		MSMSEC_PROFILE_INVALID_GKEY_INTV = 262171,
		MSMSEC_PROFILE_INVALID_KEY_INDEX = 262145,
		MSMSEC_PROFILE_INVALID_PMKCACHE_MODE = 262156,
		MSMSEC_PROFILE_INVALID_PMKCACHE_SIZE = 262157,
		MSMSEC_PROFILE_INVALID_PMKCACHE_TTL = 262158,
		MSMSEC_PROFILE_INVALID_PREAUTH_MODE = 262159,
		MSMSEC_PROFILE_INVALID_PREAUTH_THROTTLE = 262160,
		MSMSEC_PROFILE_KEY_LENGTH = 262147,
		MSMSEC_PROFILE_KEY_UNMAPPED_CHAR = 262173,
		MSMSEC_PROFILE_KEYMATERIAL_CHAR = 262167,
		MSMSEC_PROFILE_NO_AUTH_CIPHER_SPECIFIED = 262149,
		MSMSEC_PROFILE_ONEX_DISABLED = 262154,
		MSMSEC_PROFILE_ONEX_ENABLED = 262155,
		MSMSEC_PROFILE_PASSPHRASE_CHAR = 262166,
		MSMSEC_PROFILE_PREAUTH_ONLY_ENABLED = 262161,
		MSMSEC_PROFILE_PSK_LENGTH = 262148,
		MSMSEC_PROFILE_PSK_PRESENT = 262146,
		MSMSEC_PROFILE_RAWDATA_INVALID = 262152,
		MSMSEC_PROFILE_TOO_MANY_AUTH_CIPHER_SPECIFIED = 262150,
		MSMSEC_PROFILE_WRONG_KEYTYPE = 262168,
		MSMSEC_PSK_MISMATCH_SUSPECTED = 294932,
		MSMSEC_SEC_IE_MATCHING = 294922,
		MSMSEC_SECURITY_UI_FAILURE = 294934,
		MSMSEC_TRANSITION_NETWORK = 262172,
		MSMSEC_UI_REQUEST_FAILURE = 294913,
		NETWORK_NOT_AVAILABLE = 163851,
		NETWORK_NOT_COMPATIBLE = 131073,
		NO_AUTO_CONNECTION = 163841,
		NON_BROADCAST_SET_FOR_ADHOC = 524303,
		NOT_VISIBLE = 163842,
		PHY_TYPE_UNMATCH = 196612,
		PRE_SECURITY_FAILURE = 229380,
		PROFILE_BASE = 524288,
		PROFILE_CHANGED_OR_DELETED = 163852,
		PROFILE_CONNECT_BASE = 557056,
		PROFILE_END = 589823,
		PROFILE_MISSING = 524290,
		PROFILE_NOT_COMPATIBLE = 131074,
		PROFILE_SSID_INVALID = 524307,
		RANGE_SIZE = 65536,
		ROAMING_FAILURE = 229384,
		ROAMING_SECURITY_FAILURE = 229385,
		SCAN_CALL_FAIL = 163850,
		SECURITY_FAILURE = 229382,
		SECURITY_MISSING = 524300,
		SECURITY_TIMEOUT = 229383,
		SSID_LIST_TOO_LONG = 163848,
		START_SECURITY_FAILURE = 229381,
		Success = 0,
		TOO_MANY_SECURITY_ATTEMPTS = 229394,
		TOO_MANY_SSID = 524308,
		UI_REQUEST_TIMEOUT = 229393,
		UNKNOWN = 65537,
		UNSUPPORTED_SECURITY_SET = 196610,
		UNSUPPORTED_SECURITY_SET_BY_OS = 196609,
		USER_CANCELLED = 229377,
		USER_DENIED = 163844,
		USER_NOT_RESPOND = 163854
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WlanSecurityAttributes
	{
		[MarshalAs(UnmanagedType.Bool)]
		public bool securityEnabled;
		[MarshalAs(UnmanagedType.Bool)]
		public bool oneXEnabled;
		public Dot11AuthAlgorithm dot11AuthAlgorithm;
		public Dot11CipherAlgorithm dot11CipherAlgorithm;
	}
}

