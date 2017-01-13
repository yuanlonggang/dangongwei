

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.IO.Ports;
using System.IO;

namespace single_test
{
    public partial class Form1 : Form
    {
        string Configfile = Application.StartupPath + "\\config.ini";
        string path = Application.StartupPath + "\\测试记录.txt";//测试记录保存的文件路径
        public int success_number = 0;//测试产品计数器
        public int fail_number = 0; 
        private SerialPort ComPort = new SerialPort();//新串口
        private const int ERROR_SUCCESS = 0;
        private static Guid InterfaceGuid;
        private static IntPtr clientHandle = IntPtr.Zero;        
        private string MAC_Text;   
        private string SSID;
        private string KEY;
        private string Wlan_Description;
        private uint Wlan_SignalQuality = 0;
        private uint Wlan_SignalQuality_Min = 80;//wifi热点强度最小为80 
        private string SN_Recive = "";
        private string Recive_Resut = "";
        private bool UDP_Type1_Recived = false;//模块返回AP模式的测试结果
        private bool COM_Type2_Recived = false;//测试模块串口功能
        private bool COM_Type4_Recived = false;//PC发送用于联网的SSID和Password，用于测试模块STA模式
        private bool COM_Type6_Recived = false;//获取MAC地址,便于进行MAC校验                
        private bool COM_Type12_Recived = false;//重置模块
        private bool COM_Type38_Recived = false;//恢复MP.

        private DateTime Test_StartTime;
        private string WiFi_Version = "";//获取到的版本号
        private string Compare_Version = "";//比较的版本号
        private string MAC_Get = "";//获取到的MAC号
        private string COM_NO_Last = "";
        private bool TestResult = true;
        private bool AP_Test = false;
        private bool STA_Test = false;
        uint uart_numBytesRead = 0;
        byte[] uart_buffer = new byte[10000]; 

        

        public Form1()
        {
            InitializeComponent();//初始化组件
        }

        private Thread UDP_RevThread;
        private Thread UART_RevThread;

        private void Form1_Load(object sender, EventArgs e)
        {
            Configfile_Read();
            GetWlanlist();
            WlanList.SelectedIndex = 0;        
            Wlan_Description = WlanList.Text;
            Updata_SSID_List();
            InitializeCOMCombox();
            UDP_RevThread = new Thread(new ThreadStart(UDP_Listener));//UDP接收线程
            UDP_RevThread.IsBackground = true;
            UDP_RevThread.Start();//UDP开启
            UART_RevThread = new Thread(new ThreadStart(UART_DataDeal));//UART数据处理线程,都不需要调用，直接在这里创建并开启
            UART_RevThread.IsBackground = true;
            UART_RevThread.Start();//UDP开启

            //如果当前路径不存在该文件则新建一个文件*******************我之所加
            if (!File.Exists(path))
            {
                File.Create(path);
            }
        }

        private void Configfile_Read()
        {
            if (File.Exists(Configfile))//如果存在文件
            {
                SSID_List.Text = get_ini("Config", "SSID", "", Configfile);
                KEYtextBox.Text = get_ini("Config", "KEY", "", Configfile);
                COM_NO_Last = get_ini("Config", "COM", "", Configfile);
                textBox2.Text = get_ini("Config", "Wlan_SignalQuality_Min", "80", Configfile);
                textBox1.Text = get_ini("Config", "Compare_Version", "1.9.0", Configfile);
            }
        }

        private void Configfile_Write()
        {
            if (!File.Exists(Configfile))//如果文件不存在
            {
                FileStream ini_file = new FileStream(Application.StartupPath + "\\config.ini", FileMode.Create, FileAccess.Write);
            }
            WritePrivateProfileString("Config", "SSID", SSID_List.Text, Configfile);
            WritePrivateProfileString("Config", "KEY", KEYtextBox.Text, Configfile);
            WritePrivateProfileString("Config", "COM", COM_NO.Text, Configfile);
            WritePrivateProfileString("Config", "Wlan_SignalQuality_Min", textBox2.Text, Configfile);
            WritePrivateProfileString("Config", "Compare_Version", textBox1.Text, Configfile);           
        }

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        /*
         *   调用windows动态库，删除WIFI连接历史接口，
         */
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
        private static extern UInt32 WlanDeleteProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, String strProfileName, IntPtr pReserved);
      
        
        
        private string get_ini(string section, string key, string def, string filePath)
        {
            StringBuilder temp = new StringBuilder(1024);
            GetPrivateProfileString(section, key, def, temp, 1024, filePath);
            return temp.ToString();
        }

        //获得wifi列表
        private void GetWlanlist()
        {
            uint negotiatedVersion = 0;
            try
            {
                if (Wlan.WlanOpenHandle(Wlan.WLAN_CLIENT_VERSION_LONGHORN, IntPtr.Zero, ref negotiatedVersion, ref clientHandle) == 0)
                {
                    IntPtr pInterfaceList = IntPtr.Zero;
                    Wlan.WLAN_INTERFACE_INFO_LIST interfaceList;
                    if (Wlan.WlanEnumInterfaces(clientHandle, IntPtr.Zero, ref pInterfaceList) == 0)
                    {
                        interfaceList = new Wlan.WLAN_INTERFACE_INFO_LIST(pInterfaceList);
                        InterfaceGuid = ((Wlan.WLAN_INTERFACE_INFO)interfaceList.InterfaceInfo[0]).InterfaceGuid;
                        for (int i = 0; i < interfaceList.dwNumberofItems; i++)
                        {
                            WlanList.Items.Add(((Wlan.WLAN_INTERFACE_INFO)interfaceList.InterfaceInfo[i]).strInterfaceDescription);
                        }
                    }                   
                    if (pInterfaceList != IntPtr.Zero)
                        Wlan.WlanFreeMemory(pInterfaceList);
                }
            }
            catch (Exception ex)
            {
            }
        }

        //关于网卡
        private void WlanList_SelectedIndexChanged(object sender, EventArgs e)
        {
            IntPtr pInterfaceList = IntPtr.Zero;
            Wlan.WLAN_INTERFACE_INFO_LIST interfaceList;
            try
            {
                if (Wlan.WlanEnumInterfaces(clientHandle, IntPtr.Zero, ref pInterfaceList) == ERROR_SUCCESS)
                {
                    interfaceList = new Wlan.WLAN_INTERFACE_INFO_LIST(pInterfaceList);
                    for (int i = 0; i < interfaceList.dwNumberofItems; i++)
                    {
                        if (((Wlan.WLAN_INTERFACE_INFO)interfaceList.InterfaceInfo[i]).strInterfaceDescription == WlanList.SelectedItem.ToString())
                        {
                            InterfaceGuid = ((Wlan.WLAN_INTERFACE_INFO)interfaceList.InterfaceInfo[i]).InterfaceGuid;
                            Wlan_Description = WlanList.Text;
                        }
                    }
                }
                if (pInterfaceList != IntPtr.Zero)
                    Wlan.WlanFreeMemory(pInterfaceList);
            }
            catch (Exception ex)
            {
            }
        }

        //显示字符串消息
        public void ShowMsgString(string s)
        {
            if (!InvokeRequired)
            {
                _ShowMsgString(s);
                return;
            }

            this.Invoke(new MethodInvoker(delegate()
            {
                _ShowMsgString(s);
            }));
        }

        public void _ShowMsgString(string s)
        {
            RSTextBox.AppendText(s);
        }

        //显示当前时间
        public void ShowTimeString()
        {
            string tempstr = "[" + DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00") + ":" + DateTime.Now.Second.ToString("00") + " " + DateTime.Now.Millisecond.ToString("000") + "] ";
            ShowMsgString(tempstr);
        }


        private Thread TestThread = null;


        private void MACTextBox_TextChanged(object sender, EventArgs e)
        {
            if (MACTextBox.Text.Length == 12)//如果MAC的长度为12位
            {
                if (!ComPort.IsOpen)
                { 
                     Uart_Open();//串口打开
                     if (!ComPort.IsOpen)
                     {
                         ShowTimeString();
                         ShowMsgString("打开串口失败\r\n");
                         TestResult = false;
                         TestEnd();
                         return;
                     }
                }

                if (checkBox2.Checked)
                {
                    if (Readfile(path) == 0)
                    {
                        ShowTimeString();
                        ShowMsgString("该MAC已被操作！\r\n");
                        System.Media.SoundPlayer sp = new System.Media.SoundPlayer();
                        sp.SoundLocation = @"PASSED.wav";
                        sp.Play();
                        MACTextBox.Clear();
                        MACTextBox.Focus();//光标停留在MAC号的输入位置
                        return;
                    }
                }

                MAC_Text = MACTextBox.Text;//获取到MAC号                             
                WlanList.Enabled = false;//上面这些栏需要他们黯淡无光
                SSID_List.Enabled = false;
                KEYtextBox.Enabled = false;
                COM_NO.Enabled = false;
                textBox2.Enabled = false;
                textBox1.Enabled = false;
                MACTextBox.Enabled = false;//MAC号栏
                SSID = SSID_List.Text;
                KEY = KEYtextBox.Text;
                
                Compare_Version = textBox1.Text;//版本号已经输进去了
                RSTextBox.Clear();//显示栏清空                
                pictureBox1.Hide();//测试结果的图片隐藏
                pictureBox2.Hide();

                if (TestThread == null || TestThread.IsAlive == false)//如果测试线程为空或者测试线程死掉
                {
                    TestThread = new Thread(new ThreadStart(TestThreadRun));//NEW一个新线程并开启
                    TestThread.IsBackground = true;
                    TestThread.Start();//测试线程开启
                } 

            }
            if (MACTextBox.Text.Length > 12)//如果MAC的长度大于12，直接清空
            {
                MACTextBox.Clear();//清除
            }
        }
       
        //############################################################################
        private void TestThreadRun()//测试线程开始跑起来。。。
        {
            try
            {
                int retrynum = 0;
                bool retrynext = true;
                TestResult = true;              
                ShowTimeString();
                Test_StartTime = DateTime.Now;
                ShowMsgString("---------开始测试---------\r\n");
                Thread.Sleep(100);//*****最后所加

                    
                if (!ComPort.IsOpen)
                {
                    ShowTimeString();
                    ShowMsgString("打开串口失败\r\n");
                    TestResult = false;
                    TestEnd();
                    return;
                }
                ComPort.DiscardInBuffer();
                Uart_Send(Pack_data(2, ""));//测试串口通信
                ShowMsgString("\r\n");
                ShowTimeString();
                ShowMsgString("串口通信测试");                
                retrynum = 1;
                retrynext = true;
                COM_Type2_Recived = false;
                Thread.Sleep(100);
    
                    while (retrynext)
                    {
                        if (COM_Type2_Recived)
                        {
                            if (Recive_Resut == "DONE")
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                ShowMsgString("串口通信测试成功\r\n");
                                break;
                            }
                            ShowMsgString("\r\n");
                            ShowTimeString();
                            ShowMsgString("串口通信测试失败:" + Recive_Resut + "\r\n");                   
                            TestResult = false;                           
                            TestEnd();
                            return;
                        }
                        else
                        {
                            if (retrynum >= 60)
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                ShowMsgString("串口通信测试失败:超时未回复\r\n");
                                ShowTimeString();
                                ShowMsgString("请检查模块是否供电？串口线连接是否无误？\r\n");
                                TestResult = false;                          
                                TestEnd();
                                return;
                            }
                            if ((retrynum % 20) == 0)
                            {
                                ShowMsgString("@");
                                Uart_Send(Pack_data(2, ""));//不止发一次
                            }

                            Thread.Sleep(100);
                            retrynum++;
                        }
                    }

                    Thread.Sleep(100);
                    ComPort.DiscardInBuffer();
                    Uart_Send(Pack_data(6, ""));//获取MAC地址                 
                    ShowTimeString();
                    ShowMsgString("MAC号的读取...");
                    Thread.Sleep(100);
                    retrynum = 1;
                    retrynext = true;
                    COM_Type6_Recived = false;
                    while (retrynext)
                    {
                        if (COM_Type6_Recived)
                        {
                            ShowMsgString("\r\n");
                            ShowTimeString();
                            ShowMsgString("获取到的MAC号:" + MAC_Get + "\r\n");

                            this.Invoke((EventHandler)(delegate
                            {
                                textBox3.Text = MAC_Get;
                            }));

                            if (MAC_Text != MAC_Get)//比较MAC号，如果发现不一致
                            {                                
                                ShowTimeString();
                                ShowMsgString("检测到MAC号不一致！！！");
                                ShowMsgString("\r\n");                               
                                TestResult = false;
                                TestEnd();
                                return;
                            }
                            else
                            {
                                ShowTimeString();
                                ShowMsgString("MAC地址校验成功！\r\n");
                                STA_Test = true;
                                AP_Test = true;
                                break;
                            }
                        }

                        else
                        {
                            if (retrynum >= 60)
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                ShowMsgString("MAC号读取失败:超时未回复\r\n");
                                TestResult = false;                          
                                TestEnd();
                                return;
                            }
                            if ((retrynum % 20) == 0)
                            {
                                ShowMsgString("@");
                                Uart_Send(Pack_data(6, ""));
                            }

                            Thread.Sleep(100);
                            retrynum++;
                        }
                    }
                                                                                   

                if (AP_Test)
                {                    
                    ShowTimeString();
                    ShowMsgString("正在搜索" + MAC_Text + "热点");
                    Refresh_SSID();//刷新SSID
                    Thread.Sleep(200);//把500ms改成了200ms

                    //搜索WIFI热点并连接
                    retrynum = 0;
                    retrynext = true;
                    Wlan_SignalQuality = 0;

                    while (retrynext)
                    {
                        ShowMsgString(".");
                        Refresh_SSID();
                        retrynum++;
                        Thread.Sleep(500);
                        int ret = (Wlan_Search(MAC_Text, ""));                      

                        if (ret != 0)//此时搜索的热点为MAC号，密码为空
                        {
                            if (retrynum >= 30)
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                if (Wlan_SignalQuality == 0)
                                {
                                    ShowMsgString("搜索不到WIFI热点\r\n");
                                }
                                else
                                {                                 
                                    ShowMsgString("WIFI热点信号强度：" + Wlan_SignalQuality.ToString());
                                    ShowMsgString("\r\n");
                                    ShowTimeString();
                                    ShowMsgString("信号强度偏低！");                                   
                                    ShowMsgString("\r\n");
                                }
                                TestResult = false;
                                TestEnd();
                                return;
                            }
                        }

                        else
                        {
                            ShowMsgString("\r\n");
                            ShowTimeString();
                            ShowMsgString("WIFI热点信号强度：" + Wlan_SignalQuality.ToString());
                             this.Invoke((EventHandler)(delegate
                             {
                                 textBox4.Text = Wlan_SignalQuality.ToString();//将获取到的热点强度放在指定的控件栏中                                                   
                             })); 
                            break;
                        }
                    }
                                       
                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("正在连接WIFI热点");
                    retrynum = 0;
                    retrynext = true;

                    while (retrynext)
                    {
                        if (GetWlanconnectstatus() == Wlan.WLAN_INTERFACE_STATE.wlan_interface_state_connected)
                        {
                            break;
                        }

                        else
                        {
                            if (retrynum >= 150)
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                ShowMsgString("-----连接WIFI热点失败-----\r\n");
                                TestResult = false;
                                TestEnd();
                                return;
                            }
                            if ((retrynum % 5) == 0)
                            {
                                ShowMsgString(".");
                            }
                            if (GetWlanconnectstatus() == Wlan.WLAN_INTERFACE_STATE.wlan_interface_state_disconnected)
                            {
                                Wlan_Search(MAC_Text, "");
                            }
                            Thread.Sleep(100);
                            retrynum++;
                        }
                    }

                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("正在获取IP地址");
                    retrynum = 0;
                    retrynext = true;
                    while (retrynext)
                    {
                        if ((GetIP().ToString() == "127.0.0.1") || (GetIP().ToString() == "255.255.255.255") || (GetIP().ToString() == ""))
                        {
                            if (retrynum >= 30)
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                ShowMsgString("获取IP地址失败\r\n");
                                TestResult = false;
                                TestEnd();
                                return;
                            }
                            Thread.Sleep(500);
                            ShowMsgString(".");
                        }
                        else
                        {
                            break;
                        }
                    }
                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("已获取IP地址:" + GetIP().ToString() + "\r\n");//获取到的IP不可以是以上三种情况
                    Thread.Sleep(200);                
                    ShowTimeString();
                    ShowMsgString("AP模式网络通信测试");
                    retrynum = 1;
                    retrynext = true;
                    UDP_Type1_Recived = false;//模块返回AP测试的结果,用UDP发送                    
                    while (retrynext)
                    {
                        UDP_Send(Pack_data(0, ""));//udp发送,因为UDP不稳定，所以要发送好多次，叫他循环执行！进行模块AP模式的测试

                        if (UDP_Type1_Recived)
                        {
                            ShowMsgString("\r\n");
                            ShowTimeString();
                            ShowMsgString("WiFi模块版本号：" + WiFi_Version + "\r\n");//获取wifi版本号                                                      
                            if (Compare_Version != WiFi_Version)
                            {                                
                                ShowTimeString();
                                ShowMsgString("软件版本不一致！！！");
                                ShowMsgString("\r\n");
                                TestResult = false;
                                TestEnd();
                                return;
                            }
                            
                            if (Recive_Resut == "DONE")
                            {                                
                                ShowTimeString();
                                ShowMsgString("AP模式网络通信测试成功\r\n");
                                break;
                            }
                           
                            ShowMsgString("\r\n");
                            ShowTimeString();
                            ShowMsgString("AP模式网络通信测试失败，错误码：" + Recive_Resut);
                            ShowMsgString("\r\n");
                            TestResult = false;
                            TestEnd();
                            return;
                         }    
                        else
                        {
                            if (retrynum >= 100)
                            {
                                ShowMsgString("\r\n");
                                ShowTimeString();
                                ShowMsgString("AP模式网络通信测试失败:超时未回复！\r\n");
                                TestResult = false;
                                TestEnd();
                                return;
                            }
                            ShowMsgString(".");
                            Thread.Sleep(100);
                            retrynum++;
                        }                        
                     }
                 }

                    //true之三
                    if (STA_Test && checkBox1.Checked)
                    {                       
                        if (!ComPort.IsOpen)
                        {
                            ShowTimeString();
                            ShowMsgString("打开串口失败！\r\n");
                            TestResult = false;
                            TestEnd();
                            return;
                        }                      

                        //***************************************************通过串口发送SSID和密码
                        //要进行远程测试了
                        Thread.Sleep(100);//*****最后所加
                        
                        Uart_Send(Pack_data(4, "")); //远程测试的时候发送ssid和key过去                      
                        ShowTimeString();
                        ShowMsgString("STA模式(与云端)测试");
                        Thread.Sleep(100);
                        retrynum = 1;
                        retrynext = true;
                        COM_Type4_Recived = false;
                        while (retrynext)
                        {
                            if (COM_Type4_Recived)
                            {
                                if (Recive_Resut == "DONE")
                                {
                                    ShowMsgString("\r\n");
                                    ShowTimeString();
                                    ShowMsgString("STA模式(与云端)测试成功");
                                    ShowMsgString("\r\n");
                                    break;
                                }

                                if (Recive_Resut == "ERR5")
                                {
                                    ShowMsgString("\r\n");
                                    ShowTimeString();
                                    ShowMsgString("STA模式(与云端)测试失败:无法连接路由器");
                                    TestResult = false;
                                    TestEnd();
                                    break;
                                }

                                if (Recive_Resut == "ERR6")
                                {
                                    ShowMsgString("\r\n");
                                    ShowTimeString();
                                    ShowMsgString("STA模式(与云端)测试失败:平台连接错误");
                                    TestResult = false;
                                    TestEnd();
                                    break;
                                }
                            }
                            else
                            {
                                if (retrynum >= 300)//100*300=30000（30秒）
                                {
                                    ShowMsgString("\r\n");
                                    ShowTimeString();
                                    ShowMsgString("STA模式(与云端)测试失败:超时未回复\r\n");
                                    TestResult = false;
                                    //Uart_Close();
                                    TestEnd();
                                    return;
                                }
                                if ((retrynum % 10) == 0)
                                {
                                    ShowMsgString(".");//这样就够了，不能一秒发一次，那边收到要重启，没办法很快回复过来。
                                }
                                Thread.Sleep(100);
                                retrynum++;
                            }
                        }                                              
                    }
                    
                    TestEnd();
                    
                }  //try完成          

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                TestResult = false;
                TestEnd();
            }
        }
        
        //测试完成
        private void TestEnd()
        {
            ShowTimeString();
            TimeSpan test_time = DateTime.Now - Test_StartTime;
            ShowMsgString("测试完成，本次测试共耗时:" + test_time.Minutes + "分" + test_time.Seconds + "秒\r\n");

            int i = Wifi_DeleteProfile(MAC_Text);//删除wifi记录
          
            this.Invoke((EventHandler)(delegate
            {                
                WlanList.Enabled = true;//框栏又变得豁然开朗
                SSID_List.Enabled = true;
                KEYtextBox.Enabled = true;
                COM_NO.Enabled = true;
                textBox2.Enabled = true;                
                textBox1.Enabled = true;
                MACTextBox.Enabled = true;
                textBox3.Clear();
                textBox4.Clear();
                MACTextBox.Clear();
                MACTextBox.Focus();//光标停留在MAC号的输入位置

                if (TestResult)
                {
                    System.Media.SoundPlayer sp = new System.Media.SoundPlayer();
                    sp.SoundLocation = @"PASS.wav";
                    sp.Play();
                    pictureBox1.Show();
                    success_number++;
                    label18.Text = success_number.ToString();//写入到label18的文本里面                  
                    FileAdd(path, MAC_Text + "\t" + "版本号：" + WiFi_Version + "\t" + "信号强度：" + Wlan_SignalQuality.ToString() + "\t" + "耗时：" + test_time.Minutes + "分" + test_time.Seconds + "秒");                   
                }
                else
                {
                    System.Media.SoundPlayer sp = new System.Media.SoundPlayer();
                    sp.SoundLocation = @"FAIL.wav";
                    sp.Play();
                    pictureBox2.Show();
                    fail_number++;
                    label11.Text = fail_number.ToString();

                }

                float testOkRate = 0.0f;
                testOkRate = (float)success_number / (success_number + fail_number) * 100.0f;                
                label13.Text = testOkRate.ToString("0.00") + "%";

            }));

        }

        //刷新网卡SSID列表，不用改动！
        private void Refresh_SSID()
        {
            Wlan.WlanScan(clientHandle, InterfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        //搜索热点
        private int Wlan_Search(string SSID_text, string key)
        {
            IntPtr pAvailableNetworkList = IntPtr.Zero;
            //获取当前句柄下所有可扫描到的WIFI信号，放入pAvailableNetworkList中

            if (Wlan.WlanGetAvailableNetworkList(clientHandle, InterfaceGuid, Wlan.WlanGetAvailableNetworkFlags.IncludeAllAdhocProfiles, IntPtr.Zero, out pAvailableNetworkList) == ERROR_SUCCESS)
            {
                //将pAvailableNetworkList中的信息处理至wlanAvailableNetworkList中
                Wlan.WLAN_AVAILABLE_NETWORK_LIST wlanAvailableNetworkList = new Wlan.WLAN_AVAILABLE_NETWORK_LIST(pAvailableNetworkList);
                //释放pAvailableNetworkList内存
                Wlan.WlanFreeMemory(pAvailableNetworkList);
                int index = 0;
                //循环遍历wlanAvailableNetworkList，寻找SSID符合需求的信号
                for (index = 0; index < wlanAvailableNetworkList.dwNumberOfItems; index++)
                {
                    Wlan.WLAN_AVAILABLE_NETWORK network = wlanAvailableNetworkList.Networks[index];
                    //SSID满足要求
                    if (GetStringForSSID(network.dot11Ssid) == SSID_text)
                    {
                        //获取WIFI信号强度
                        Wlan_SignalQuality = network.wlanSignalQuality;

                        if (Wlan_SignalQuality > Wlan_SignalQuality_Min)
                        {
                            //开放式WIFI，即WIFI无密码的情况
                            // if (checkBox1.Checked)
                            ///    return Wifi_SetProfile(Wlan.DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN, SSID_text, key);
                            //非开放式WIFI，传入WIFI的密码
                            //  else
                            return Wifi_SetProfile(network.dot11DefaultAuthAlgorithm, SSID_text, key);
                        }
                        else
                        {
                            return -2;
                        }
                    }
                }
            }
            return -1;
        }

        //wifi设置配置文件
        static int Wifi_SetProfile(Wlan.DOT11_AUTH_ALGORITHM connType, string profileName, string key)
        {
            string profileXml = "";
            Wlan.WlanReasonCode reasonCode;
            try
            {
                //Connection parameter
                Wlan.WlanConnectionParameters wlanConnParam = new Wlan.WlanConnectionParameters();
                wlanConnParam.wlanConnectionMode = Wlan.WlanConnectionMode.Profile;
                wlanConnParam.profile = profileName;
                wlanConnParam.dot11SsidPtr = IntPtr.Zero;
                wlanConnParam.desiredBssidListPtr = IntPtr.Zero;
                wlanConnParam.dot11BssType = Wlan.Dot11BssType.Infrastructure;// Wlan.DOT11_BSS_TYPE.dot11_BSS_type_infrastructure;
                wlanConnParam.flags = 0;

                switch (connType)
                {
                    //开放式WIFI
                    case Wlan.DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN:
                        string mac = StringToHex(profileName);
                        //开放式WIFI无需Key
                        profileXml = string.Format("<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{0}</name><SSIDConfig><SSID><hex>{1}</hex><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>manual</connectionMode><MSM><security><authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption></security></MSM></WLANProfile>", profileName, mac);
                        Wlan.WlanSetProfile(clientHandle, InterfaceGuid, Wlan.WlanProfileFlags.AllUser, profileXml, null, true, IntPtr.Zero, out reasonCode);
                        if (reasonCode == Wlan.WlanReasonCode.Success)
                        {
                            int ret = (int)Wlan.WlanConnect(clientHandle, InterfaceGuid, ref wlanConnParam, IntPtr.Zero);
                            return ret;
                        }
                        else
                        {
                            return -1;
                        }
                    //WEP-PSK加密类型WIFI
                    case Wlan.DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA:
                        profileXml = string.Format("<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{0}</name><SSIDConfig><SSID><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><autoSwitch>false</autoSwitch><MSM><security><authEncryption><authentication>WPAPSK</authentication><encryption>TKIP</encryption><useOneX>false</useOneX></authEncryption><sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{1}</keyMaterial></sharedKey></security></MSM></WLANProfile>", profileName, key);
                        Wlan.WlanSetProfile(clientHandle, InterfaceGuid, Wlan.WlanProfileFlags.AllUser, profileXml, null, true, IntPtr.Zero, out reasonCode);
                        if (reasonCode == Wlan.WlanReasonCode.Success)
                        {
                            int ret = (int)Wlan.WlanConnect(clientHandle, InterfaceGuid, ref wlanConnParam, IntPtr.Zero);
                            return ret;
                        }
                        else
                        {
                            return -1;
                        }
                    //AES加密类型WIFI
                    case Wlan.DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA_PSK:
                        profileXml = string.Format("<?xml version=\"1.0\" encoding=\"US-ASCII\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">  <name>{0}</name><SSIDConfig><SSID><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><autoSwitch>false</autoSwitch>  <MSM>    <security>      <authEncryption>        <authentication>WPA2PSK</authentication>        <encryption>AES</encryption>        <useOneX>false</useOneX>      </authEncryption>      <sharedKey>        <keyType>passPhrase</keyType>        <protected>false</protected>        <keyMaterial>          {1}        </keyMaterial>      </sharedKey>    </security>  </MSM></WLANProfile>", profileName, key);
                        Wlan.WlanSetProfile(clientHandle, InterfaceGuid, Wlan.WlanProfileFlags.AllUser, profileXml, null, true, IntPtr.Zero, out reasonCode);
                        if (reasonCode == Wlan.WlanReasonCode.Success)
                        {
                            int ret = (int)Wlan.WlanConnect(clientHandle, InterfaceGuid, ref wlanConnParam, IntPtr.Zero);
                            return ret;
                        }
                        else
                        {
                            return -1;
                        }
                    default:
                        return -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        public static string StringToHex(string str)
        {
            StringBuilder sb = new StringBuilder();
            byte[] byStr = System.Text.Encoding.Default.GetBytes(str); //默认是System.Text.Encoding.Default.GetBytes(str)  
            for (int i = 0; i < byStr.Length; i++)
            {
                sb.Append(Convert.ToString(byStr[i], 16));
            }
            return (sb.ToString().ToUpper());
        }


        //获取SSID的字符串
        public static string GetStringForSSID(Wlan.DOT11_SSID ssid)
        {
            string sSSId = "";
            try
            {
                for (int k = 0; k < ssid.uSSIDLength; k++)
                    sSSId += ssid.ucSSID[k];
            }
            catch (Exception ex)
            {

            }
            return sSSId;
        }

        //获取wifi的连接状态
        private Wlan.WLAN_INTERFACE_STATE GetWlanconnectstatus()
        {
            try
            {
                IntPtr pInterfaceList = IntPtr.Zero;
                Wlan.WLAN_INTERFACE_INFO_LIST interfaceList;
                if (Wlan.WlanEnumInterfaces(clientHandle, IntPtr.Zero, ref pInterfaceList) == ERROR_SUCCESS)
                {
                    interfaceList = new Wlan.WLAN_INTERFACE_INFO_LIST(pInterfaceList);
                    for (int i = 0; i < interfaceList.dwNumberofItems; i++)
                    {
                        if (((Wlan.WLAN_INTERFACE_INFO)interfaceList.InterfaceInfo[i]).InterfaceGuid == InterfaceGuid)
                        {
                            return interfaceList.InterfaceInfo[i].isState;
                        }
                    }
                }
                //frees memory
                if (pInterfaceList != IntPtr.Zero)
                    Wlan.WlanFreeMemory(pInterfaceList);
            }
            catch (Exception ex)
            {
            }
            return 0;
        }

        //wifi删除的配置文件
        static int Wifi_DeleteProfile(string profileName)
        {
            return Wlan.WlanDeleteProfile(clientHandle, ref InterfaceGuid, profileName, IntPtr.Zero);
        }

        //获取IP
        private IPAddress GetIP()
        {
            try
            {
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();//获取本机所有网卡对象
                foreach (NetworkInterface adapter in adapters)
                {
                    if (adapter.Description.Equals(Wlan_Description))
                    {
                        IPInterfaceProperties ipProperties = adapter.GetIPProperties();//获取IP配置
                        UnicastIPAddressInformationCollection ipCollection = ipProperties.UnicastAddresses;//获取单播地址集
                        foreach (UnicastIPAddressInformation ip in ipCollection)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)//只要ipv4的
                                return ip.Address;//获取ip
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return IPAddress.None;
        }
        //UDP发送
        public void UDP_Send(byte[] data)
        {
            try
            {
                IPAddress addr = GetIP();
                IPEndPoint endpoint = new IPEndPoint(addr, 0);
                UdpClient udp = new UdpClient(endpoint);
                udp.Connect(IPAddress.Parse("255.255.255.255"), 8086);
                int i = 0;
                for (i = 0; i < 1; i++) //既然只发送一次，干嘛还这么矫情。
                {
                    udp.Send(data, data.Length);//发送函数，具体发送内容还得组包
                    Thread.Sleep(200);
                }
                udp.Close();
            }
            catch (Exception ex)
            {
            }
        }

        //UDP接收
        public void UDP_Listener()
        {
            UdpClient udpclient = new UdpClient(9986); 
            IPEndPoint ipendpoint = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                while (true)
                {
                    byte[] UDP_RevBuff = udpclient.Receive(ref ipendpoint);
                    if (UDP_RevBuff.Length > 6)
                    {
                        int type = UDP_RevBuff[2];
                        if (type > -1)
                        {
                            byte[] SN_bt = new byte[24];
                            byte[] Resut_bt = new byte[4];
                            switch (type)
                            {
                                case 1://模块返回AP模式的测试结果
                                    WiFi_Version = UDP_RevBuff[5].ToString() + "." + UDP_RevBuff[4].ToString() + "." + UDP_RevBuff[3].ToString();
                                    Array.Copy(UDP_RevBuff, 6, SN_bt, 0, 24);
                                    SN_Recive = Encoding.Default.GetString(SN_bt);//此时的SN_Recive应该全为0
                                    Array.Copy(UDP_RevBuff, 30, Resut_bt, 0, 4);
                                    Recive_Resut = Encoding.Default.GetString(Resut_bt);//收到的结果，done是最好的
                                    UDP_Type1_Recived = true;
                                    break;
                                case 15://设备回送回复【重置模块】的测试结果，意思是这里不具备这个功能了。
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString()); 
            }
            finally
            {
                udpclient.Close();
            }
        }

        //type组包——组外包 这里又不论串口和UDP
        public byte[] Pack_data(int type, string sn)
        {
            byte[] pack = null;
            switch (type)
            {
                case 0://UDP发送给模块，进行AP检测
                    pack = Build_pack(0x00, null, 1);  
                    break;

                case 2://测试模块串口功能
                    pack = Build_pack(0x02, null, 1);   
                    break;

                case 4://发送路由帐号密码，用于测试STA功能
                    byte[] package4 = new byte[SSID.Length + KEY.Length + 2];
                    package4[0] = (byte)SSID.Length;
                    System.Text.Encoding.Default.GetBytes(SSID).CopyTo(package4, 1);
                    package4[SSID.Length + 1] = (byte)KEY.Length;
                    System.Text.Encoding.Default.GetBytes(KEY).CopyTo(package4, SSID.Length + 2);
                    pack = Build_pack(0x04, package4, SSID.Length + KEY.Length + 3);
                    break;
                              
                case 12://重置模块
                    pack = Build_pack(0x0c, null, 1);
                    break;                                
                 
                case 6://通过串口获取MAC
                    pack = Build_pack(0x06, null, 1);
                    break;

                case 38://恢复MP
                    pack = Build_pack(0x26, null, 1);
                    break;

                default:
                    break;
            }
            return pack;
        }

        //组内包——都相同
        public byte[] Build_pack(byte type, byte[] DATA, int DATA_long)
        {
            byte[] packge = new byte[4 + DATA_long];
            //组包
            packge[0] = 0xA6;
            packge[1] = 0xA6;
            packge[2] = type;
            packge[3] = (byte)(DATA_long + 4);      
            if (DATA != null)
                DATA.CopyTo(packge, 4);
            byte checksum = 0x00;
            for (int x = 0; x < packge.Length - 1; x++)
                checksum += packge[x];
            packge[4 + DATA_long - 1] = checksum;
            return packge;
        }
      
        //定义信号强度的时候要合理，取值在0-100之间
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            uint Q = Convert.ToUInt32(textBox2.Text);
            if ((Q >= 0) && (Q <= 100))
            {
                Wlan_SignalQuality_Min = Q;
            }
        }
        //SSID列表数据
        private void Updata_SSID_List()
        {
            SSID_List.Items.Clear();
            IntPtr pAvailableNetworkList = IntPtr.Zero;
            if (Wlan.WlanGetAvailableNetworkList(clientHandle, InterfaceGuid, 0, IntPtr.Zero, out pAvailableNetworkList) == ERROR_SUCCESS)
            {
                Wlan.WLAN_AVAILABLE_NETWORK_LIST wlanAvailableNetworkList = new Wlan.WLAN_AVAILABLE_NETWORK_LIST(pAvailableNetworkList);
                Wlan.WlanFreeMemory(pAvailableNetworkList);
                int index = 0;
                for (index = 0; index < wlanAvailableNetworkList.dwNumberOfItems; index++)
                {
                    Wlan.WLAN_AVAILABLE_NETWORK network = wlanAvailableNetworkList.Networks[index];
                    if (GetStringForSSID(network.dot11Ssid) != "")
                        SSID_List.Items.Add(GetStringForSSID(network.dot11Ssid));
                }
                if ((index > 0) && (SSID_List.Text == ""))
                {
                    SSID_List.SelectedIndex = 0;
                }
            }
        }
       
        //SSID列表下拉
        private void SSID_List_DropDown(object sender, EventArgs e)
        {            
            Updata_SSID_List();
            Refresh_SSID();
        }

        //初始化串口
        private void InitializeCOMCombox()
        {
            string[] ArrayComPortsNames = SerialPort.GetPortNames();
            if (ArrayComPortsNames.Length == 0)
            {
                ShowTimeString();
                ShowMsgString("未找到串口\r\n");
                COM_NO.Text = "";
                return;
            }
            else
            {
                COM_NO.Items.Clear();
                Array.Sort(ArrayComPortsNames);
                for (int i = 0; i < ArrayComPortsNames.Length; i++)
                {
                    //显示找到的串口
                    COM_NO.Items.Add(ArrayComPortsNames[i]);
                    if (ArrayComPortsNames[i] == COM_NO_Last)
                    {
                        COM_NO.Text = COM_NO_Last;
                    }
                }
                if (COM_NO.Text == "")
                {
                    COM_NO.SelectedIndex = 0;
                }
            }
        }
 
        //点叉叉关闭软件
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (UDP_RevThread != null || UDP_RevThread.IsAlive != false)
                UDP_RevThread.Abort();
            Configfile_Write();
            Thread.Sleep(1000);
            Environment.Exit(0);
        }

        //串口列表下拉
        private void COM_NO_DropDown(object sender, EventArgs e)
        {
            bool alive = false;
            if (ComPort.IsOpen)
            {
                Uart_Close();
                alive = true;
            }
            InitializeCOMCombox();
            Uart_Open();
        }

        //光标所在的位置，先输入密码，在输入MAC号，最后输入SN
        private void Form1_Shown(object sender, EventArgs e)
        {
            if (KEYtextBox.Text == "")
            {
                KEYtextBox.Focus();
            }
            else if (textBox1.Text == "")
            {
                textBox1.Focus();
            }
            else 
            {
                MACTextBox.Focus();
            }
           
        }

        //串口打开
        public void Uart_Open()
        {
            if (!InvokeRequired)
            {
                _Uart_Open();
                return;
            }

            this.Invoke(new MethodInvoker(delegate()
            {
                _Uart_Open();
            }));
        }

        private void _Uart_Open()
        {
            if (ComPort.IsOpen)
            {
                ComPort.Close();
            }
            if (COM_NO.Text == "")
            {
                ShowTimeString();
                ShowMsgString("未找到串口\r\n");
                return;
            }
            ComPort.PortName = COM_NO.Text;
            ComPort.BaudRate = 9600;
            ComPort.Parity = Parity.None;
            ComPort.StopBits = StopBits.One;
            ComPort.DataBits = 8;
            ComPort.WriteTimeout = 3000;
            ComPort.ReadTimeout = 3000;
            ComPort.ReceivedBytesThreshold = 1; 
            ComPort.Open();
            if (ComPort.IsOpen)
            {
                ComPort.DataReceived += new SerialDataReceivedEventHandler(Uart_DataReceived);
            }
        }

        //串口数据接收
        void Uart_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            uint numBytesRead = 0;

            if (uart_numBytesRead > 0)
                return;           
            
            if (sender == ComPort)
            {
                Thread.Sleep(100);
                while ((numBytesRead < 10000) && ComPort.IsOpen && (ComPort.BytesToRead > 0))
                {
                    uart_buffer[numBytesRead] = Convert.ToByte(ComPort.ReadByte());

                    if ((ComPort.BytesToRead <= 0) && (uart_buffer[numBytesRead] == 0xa6))
                    {
                        Thread.Sleep(100);
                    }
                    numBytesRead++;
                }

            }

            if (numBytesRead > 0)
            {
                uart_numBytesRead = numBytesRead;
            }
        }

        //串口数据处理

        public void UART_DataDeal()
        {
            uint numBytesRead = 0;
            uint numBytesToRead = 10000;
            uint i = 0;
            uint oldnum = 0;
            bool incomplet = false;
            byte[] buffer = new byte[numBytesToRead];

            while (true)
            {
                if (!ComPort.IsOpen)
                {
                    oldnum = 0;
                    incomplet = false;
                }
                if (uart_numBytesRead > 0)
                {

                    if (uart_numBytesRead > 10000)
                    {
                        uart_numBytesRead = 0;
                        continue;
                    }
                    if (incomplet == false)
                    {
                        numBytesRead = uart_numBytesRead;
                        Array.Copy(uart_buffer, 0, buffer, 0, numBytesRead);
                    }
                    else
                    {
                        numBytesRead = uart_numBytesRead + oldnum;
                        Array.Copy(uart_buffer, 0, buffer, oldnum, numBytesRead);
                        oldnum = 0;
                        incomplet = false;
                    }
                    uart_numBytesRead = 0;

                    /*////////
                    ShowMsgString("--------------------\r\n");
                    i = 0;
                    while (i < numBytesRead)
                    {
                        ShowMsgString(buffer[i].ToString("X2") + " ");
                        i++;
                    }
                    ShowMsgString("\r\n");
                    ////////////*/

                    i = 0;
                    while (i + 5 <= numBytesRead)
                    {
                        if ((buffer[i] == 0xA6) && (buffer[i + 1] == 0xA6))
                        {
                            int type = buffer[i + 2];
                            if ((incomplet))
                            {
                                incomplet = false;
                                oldnum = 0;
                            }

                            byte[] Resut_bt = new byte[4];
                            byte[] mac_get = new byte[12];

                            switch (type)
                            {
                                case 3://模块回送串口测试结果
                                    WiFi_Version = buffer[i + 5].ToString() + "." + buffer[i + 4].ToString() + "." + buffer[i + 3].ToString();
                                    Array.Copy(buffer, i + 6, Resut_bt, 0, 4);
                                    Recive_Resut = Encoding.Default.GetString(Resut_bt);

                                    if (Recive_Resut == "DONE")
                                        COM_Type2_Recived = true;
                                    else
                                    {
                                        if (numBytesRead - i < 11)
                                        {
                                            incomplet = true;
                                            break;
                                        }
                                        else
                                        {
                                            COM_Type2_Recived = true;
                                            oldnum = 0;
                                        }
                                    }
                                    break;
                                case 5://模块回送STA测试结果
                                    WiFi_Version = buffer[i + 5].ToString() + "." + buffer[i + 4].ToString() + "." + buffer[i + 3].ToString();
                                    Array.Copy(buffer, i + 6, Resut_bt, 0, 4);
                                    Recive_Resut = Encoding.Default.GetString(Resut_bt);
                                    if (Recive_Resut == "DONE")
                                        COM_Type4_Recived = true;
                                    else
                                    {
                                        if (numBytesRead - i < 11)
                                        {
                                            incomplet = true;
                                            break;
                                        }
                                        else
                                        {
                                            COM_Type4_Recived = true;
                                            oldnum = 0;
                                        }
                                    }
                                    break;
                               

                                case 13://设备回送“重置模块”的结果
                                    WiFi_Version = buffer[i + 5].ToString() + "." + buffer[i + 4].ToString() + "." + buffer[i + 3].ToString();
                                    Array.Copy(buffer, i + 6, Resut_bt, 0, 4);
                                    Recive_Resut = Encoding.Default.GetString(Resut_bt);
                                    COM_Type12_Recived = true;                                  
                                    break;

                                case 39://设备回送“恢复MP”的结果
                                    WiFi_Version = buffer[5].ToString() + "." + buffer[4].ToString() + "." + buffer[3].ToString();
                                    Array.Copy(buffer, 6, Resut_bt, 0, 4);
                                    Recive_Resut = Encoding.Default.GetString(Resut_bt);
                                    COM_Type38_Recived = true;
                                    break;

                                case 7://模块回送MAC地址******************************************我之所加
                                    WiFi_Version = buffer[5].ToString() + "." + buffer[4].ToString() + "." + buffer[3].ToString();
                                    Array.Copy(buffer, 6, Resut_bt, 0, 4);
                                    Recive_Resut = Encoding.Default.GetString(Resut_bt);
                                    Array.Copy(buffer, 10, mac_get, 0, 12);
                                    MAC_Get = Encoding.Default.GetString(mac_get);
                                    if (Recive_Resut == "DONE")
                                        COM_Type6_Recived = true;
                                    else
                                    {
                                        if (numBytesRead - i < 35)
                                        {
                                            incomplet = true;
                                            break;
                                        }
                                        else
                                        {
                                            COM_Type6_Recived = true;
                                            oldnum = 0;
                                        }
                                    }
                                    break;

                                default:
                                    break;
                            }

                            if (incomplet == true)
                                break;
                            i = i + 5;
                        }

                        while (i <= numBytesRead)
                        {
                            if (buffer[i] == 0xa6)
                                break;
                            i++;
                        }
                    }

                    /////////////可能下一帧只接收到一半，所以需要做特殊的处理

                    if ((i + 1 < numBytesRead) && (buffer[i] == 0xa6))
                    {
                        if (i != 0)
                        {
                            Array.Copy(buffer, i, buffer, 0, numBytesRead - i);
                        }
                        oldnum = numBytesRead - i;
                        i = numBytesRead;
                        incomplet = true;
                        continue;
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }

        //串口数据发送——千篇一律
        private void Uart_Send(byte[] data)
        {
            if (!InvokeRequired)
            {
                _Uart_Send(data);
                return;
            }

            this.Invoke(new MethodInvoker(delegate()
            {
                _Uart_Send(data);
            }));
        }

        private void _Uart_Send(byte[] data)
        {
            if (ComPort.IsOpen)
            {
                ComPort.Write(data, 0, data.Length);
            }
        }

        //串口关闭
        private void Uart_Close()
        {
            if (!InvokeRequired)
            {
                _Uart_Close();
                return;
            }

            this.Invoke(new MethodInvoker(delegate()
            {
                _Uart_Close();
            }));
        }
        private void _Uart_Close()
        {
            if (ComPort.IsOpen)
            {
                ComPort.DataReceived -= new SerialDataReceivedEventHandler(Uart_DataReceived);
                ComPort.Close();
            }
        }               

        //将字符串写入文件中，文件处理包含了文件的删除，粘贴，复制等基本的操作信息。copy过来，如有问题再来商榷。
        public static void FileAdd(string Path, string strings)//两个参数，要写入的字符串和文件路径
        {
            string bak_path = "";
            int namei = Path.IndexOf(".txt");//路径是一个txt文件？

            if (namei >= 0)//表示存在
                bak_path = Path.Insert(namei, "_temp");//插入

            if (strings.Length == 0) //写入的字符串长度为0，开玩笑！就是啥也没写嘛。
                return;

            try
            {
                if (File.Exists(bak_path)) //指定的文件存在
                {
                    if (File.Exists(Path))
                    {
                        File.Delete(bak_path);//删除
                    }
                    else
                    {
                        File.Move(bak_path, Path);
                    }
                }

                if (File.Exists(Path))//路径也存在
                    File.Copy(Path, bak_path);//将现有文件复制到新文件，不允许覆盖同名文件。
                StreamWriter sw = File.AppendText(bak_path);//以一种特定的编码向流中写入字符
                sw.Write(DateTime.Now.ToLocalTime().ToString() + "\t" + strings + "\r\n");//当前的时间加上字符串写入流

                sw.Flush();//清理缓冲区，并使所有的缓冲区数据写入基础流
                sw.Close();//关闭当前的streamwriter对象和基础流

                if (File.Exists(Path))
                    File.Delete(Path);
                File.Move(bak_path, Path);
            }
            catch (Exception e)
            {
                MessageBox.Show("写记录文件错误 error" + e.Message.ToString());
            }
        }

        //遍历文件查找MAC号是否已经写过
        public int Readfile(string path)    //逐行读取文件确认是否MAC已经被写入
        {
            try
            {
                if (!File.Exists(path))
                    File.CreateText(path);                
                StreamReader sr = new StreamReader(path, Encoding.Default);
                string linedata;
                int id;
                while ((linedata = sr.ReadLine()) != null)//从当前流中读取一行字符并将数据作为字符串返回
                {
                    if ((id = (linedata.IndexOf(MACTextBox.Text))) > -1) //大于等于0表示存在
                    {
                        sr.Dispose();//释放对象占用的所有资源
                        sr.Close();//关闭对象和基础流，并释放与读取器关联的所有系统资源
                        return 0;//表示该MAC栏已经存在
                    }
                };
                sr.Dispose();
                sr.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show("读取记录文件失败 ：" + e.Message.ToString());
            }            
            return 1;//表示不曾操作过
        }

        //打开写入记录文件

        private void history_Click(object sender, EventArgs e)
        {
        System.Diagnostics.Process.Start(path);//启动进程资源
        }

        //清零功能
        private void button1_Click(object sender, EventArgs e)
        {
            label18.Text = "0";
            label11.Text = "0";
            label13.Text = "100.00%";
            success_number = 0;
            fail_number = 0;
        }

        //恢复工厂并擦除SN号        
        private void button2_Click(object sender, EventArgs e)
        {            
            if (!ComPort.IsOpen)
            {

                Uart_Open();//串口打开
                if (!ComPort.IsOpen)
                {
                    ShowTimeString();
                    ShowMsgString("打开串口失败\r\n");
                    TestResult = false;
                    TestEnd();//立马上结果
                    return;
                }
            }

            COM_Type12_Recived = false;
            Uart_Send(Pack_data(12, ""));//发送命令！  
            ShowMsgString("\r\n");
            ShowTimeString();
            ShowMsgString("【开始重置模块】");
            Thread.Sleep(800);

            if (COM_Type12_Recived)
            {
                if (Recive_Resut == "DONE")
                {
                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("重置模块成功！\r\n");
                    textBox3.Clear();
                    textBox4.Clear();
                    MACTextBox.Clear();
                    MACTextBox.Focus();//光标停留在MAC号的输入位置               
                }
                else
                {
                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("重置模块失败:" + Recive_Resut + "\r\n");
                    MACTextBox.Focus();
                    return;
                }
            }
            else
            {
                ShowMsgString("\r\n");
                ShowTimeString();
                ShowMsgString("重置模块失败！ \r\n");
                MACTextBox.Focus();
                return;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Uart_Open();//串口打开
            if (!ComPort.IsOpen)
            {
                ShowTimeString();
                ShowMsgString("打开串口失败\r\n");
                TestResult = false;
                TestEnd();//立马上结果
                return;
            }

            Uart_Send(Pack_data(38, ""));//发送命令！  
            ShowMsgString("\r\n");
            ShowTimeString();
            ShowMsgString("【恢复MP模式】");
            COM_Type38_Recived = false;
            Thread.Sleep(1000);

            if (COM_Type38_Recived)
            {
                if (Recive_Resut == "DONE")
                {
                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("【恢复MP模式】：成功\r\n");
                    MACTextBox.Focus();
                }
                else
                {
                    ShowMsgString("\r\n");
                    ShowTimeString();
                    ShowMsgString("恢复MP结果失败:" + Recive_Resut + "\r\n");
                    MACTextBox.Focus();
                    return;
                }
            }
            else
            {
                ShowMsgString("\r\n");
                ShowTimeString();
                ShowMsgString("恢复MP结果失败 \r\n");
                MACTextBox.Focus();
                return;
            }
        }             

    }
}