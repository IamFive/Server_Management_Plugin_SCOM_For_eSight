﻿// ***********************************************************************
// Assembly         : Huawei.SCOM.ESightPlugin.Service
// Author           : suxiaobo
// Created          : 12-12-2017
//
// Last Modified By : suxiaobo
// Last Modified On : 12-12-2017
// ***********************************************************************
// <copyright file="ESightPluginService.cs" company="广州摩赛网络技术有限公司">
//     Copyright ©  2017
// </copyright>
// <summary>The e sight plugin service.</summary>
// ***********************************************************************

namespace Huawei.SCOM.ESightPlugin.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.ServiceProcess;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using CommonUtil;

    using Huawei.SCOM.ESightPlugin.Core;
    using Huawei.SCOM.ESightPlugin.Models;
    using Huawei.SCOM.ESightPlugin.Models.Server;
    using Huawei.SCOM.ESightPlugin.RESTeSightLib;
    using Huawei.SCOM.ESightPlugin.RESTeSightLib.Helper;
    using Huawei.SCOM.ESightPlugin.LogUtil;

    using Timer = System.Timers.Timer;

    /// <summary>
    /// The e sight plugin service.
    /// </summary>
    public partial class ESightPluginService : ServiceBase
    {
        /// <summary>
        /// The polling timer
        /// </summary>
        private Timer pollingTimer;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ESightPluginService"/> class.
        /// </summary>
        public ESightPluginService()
        {
            this.InitializeComponent();
            this.SyncInstances = new List<ESightSyncInstance>();
            this.CanHandlePowerEvent = true;
        }
        #endregion

        #region Property

        /// <summary>
        /// Gets or sets the synchronize instances.
        /// </summary>
        /// <value>The synchronize instances.</value>
        public List<ESightSyncInstance> SyncInstances { get; set; }

        /// <summary>
        /// The env variable.
        /// </summary>
        public string EnvVariable => "ESIGHTSCOMPLUGIN";

        /// <summary>
        /// Gets or sets the iis process.
        /// </summary>
        public Process IISProcess { get; set; }

        /// <summary>
        /// The install path.
        /// </summary>
        public string InstallPath => Environment.GetEnvironmentVariable(this.EnvVariable);

        /// <summary>
        /// 接收web转发的订阅消息
        /// </summary>
        /// <value>The TCP server taxk.</value>
        public Task TcpServerTask { get; private set; }
        #endregion

        #region Public Methods

        /// <summary>
        /// The debug.
        /// </summary>
        public void Debug()
        {
            this.OnStart(new[] { string.Empty });
            //this.CreateTcpServer();
        }

        #endregion

        #region Task
        /// <summary>
        /// The on start.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        /// <exception cref="Exception"> ex
        /// </exception>
        protected override void OnStart(string[] args)
        {
            try
            {
                var maxTryTimes = 0;
                while (true)
                {
                    maxTryTimes++;
                    if (this.CheckScomConnection())
                    {
                        this.Start();
                        break;
                    }
                    else
                    {
                        if (maxTryTimes >= 200)
                        {
                            this.OnError($"Stop retry: maxTryTimes:{maxTryTimes}");
                            break;
                        }
                        this.OnError("The Data Access service is either not running or not yet initialized,try again 3 seconds later.");
                        Thread.Sleep(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                this.OnLog("Start Service Error：" + ex);
                this.Stop();
            }
        }

        /// <summary>
        /// The on stop.
        /// </summary>
        /// <exception cref="Exception">ex
        /// </exception>
        protected override void OnStop()
        {
            try
            {
                if (this.IISProcess != null)
                {
                    if (!this.IISProcess.HasExited)
                    {
                        this.OnLog("Kill IISProcess");
                        this.IISProcess.Kill();
                    }
                    else
                    {
                        this.OnLog("IISProcess Has Exited");
                    }
                }
                this.OnLog("Stop Service Success");
            }
            catch (Exception ex)
            {
                this.OnLog("Stop Service Error：" + ex);
            }
        }

        /// <summary>
        /// Called when [power event].
        /// </summary>
        /// <param name="powerStatus">The power status.</param>
        /// <returns>System.Boolean.</returns>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            string message = $" powerStatus:{powerStatus} tcpServerTaskStatus:{this.TcpServerTask.Status} this.pollingTimer{this.pollingTimer.Enabled} ";
            this.OnLog(message);
            return true;
        }

        /// <summary>
        /// Called when [continue].
        /// </summary>
        protected override void OnContinue()
        {
            string message = $"OnContinue: tcpServerTaskStatus:{this.TcpServerTask.Status} this.pollingTimer{this.pollingTimer.Enabled} ";
            this.OnLog(message);
        }

        /// <summary>
        /// Called when [pause].
        /// </summary>
        protected override void OnPause()
        {
            string message = $"OnPause: tcpServerTaskStatus:{this.TcpServerTask.Status} this.pollingTimer{this.pollingTimer.Enabled} ";
            this.OnLog(message);
        }

        /// <summary>
        /// Checks the connection.
        /// </summary>
        /// <returns>System.Boolean.</returns>
        private bool CheckScomConnection()
        {
            try
            {
                MGroup.Instance.Init();
            }
            catch (Exception ex)
            {
                if (ex.GetType().ToString() != "Microsoft.EnterpriseManagement.Common.ServiceNotRunningException")
                {
                    this.OnError("CheckScomConnection", ex);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// The start.
        /// </summary>
        /// <exception cref="Exception"> ex
        /// </exception>
        private void Start()
        {
            this.OnLog("env:ESIGHTSCOMPLUGIN：" + this.InstallPath);
            if (string.IsNullOrEmpty(this.InstallPath))
            {
                throw new Exception("Can not find the environment variable \"ESIGHTSCOMPLUGIN\"");
            }

            this.CheckAndInstallMp();

            this.TcpServerTask = Task.Run(() => this.CreateTcpServer());
#if !DEBUG
            this.RunPolling();
#endif
            this.RunSync(); // 先执行一次 

            this.RunWebServer();

            this.ImportToRoot();

            this.OnLog("Service Start Success");
        }

        /// <summary>
        ///     Checks the and install mp.
        /// </summary>
        private void CheckAndInstallMp()
        {
#if DEBUG
            if (!MGroup.Instance.CheckIsInstallMp("eSight.View.Library"))
            {
                var path = $"{this.InstallPath}\\MPFiles\\eSight.View.Library.mpb";
                MGroup.Instance.InstallMpb(path);
            }

            // if (!MGroup.Instance.CheckIsInstallMp("eSight.BladeServer.Library"))
            // {
            // string path = $"{InstallPath}\\MPFiles\\eSight.BladeServer.Library.mpb";
            // MGroup.Instance.InstallMp(path);
            // }
            // if (!MGroup.Instance.CheckIsInstallMp("eSight.HighDensityServer.Library"))
            // {
            // string path = $"{InstallPath}\\MPFiles\\eSight.HighDensityServer.Library.mpb";
            // MGroup.Instance.InstallMp(path);
            // }
            // if (!MGroup.Instance.CheckIsInstallMp("eSight.RackServer.Library"))
            // {
            // string path = $"{InstallPath}\\MPFiles\\eSight.RackServer.Library.mpb";
            // MGroup.Instance.InstallMp(path);
            // }
            // if (!MGroup.Instance.CheckIsInstallMp("eSight.KunLunServer.Library"))
            // {
            // string path = $"{InstallPath}\\MPFiles\\eSight.KunLunServer.Library.mpb";
            // MGroup.Instance.InstallMp(path);
            // }
#else
            if (!MGroup.Instance.CheckIsInstallMp("eSight.View.Library"))
            {
                string path = $"{this.InstallPath}\\MPFiles\\eSight.View.Library.mpb";
                MGroup.Instance.InstallMpb(path);
            }
            if (!MGroup.Instance.CheckIsInstallMp("eSight.BladeServer.Library"))
            {
                string path = $"{this.InstallPath}\\MPFiles\\eSight.BladeServer.Library.mpb";
                MGroup.Instance.InstallMpb(path);
            }
            if (!MGroup.Instance.CheckIsInstallMp("eSight.HighDensityServer.Library"))
            {
                string path = $"{this.InstallPath}\\MPFiles\\eSight.HighDensityServer.Library.mpb";
                MGroup.Instance.InstallMpb(path);
            }
            if (!MGroup.Instance.CheckIsInstallMp("eSight.RackServer.Library"))
            {
                string path = $"{this.InstallPath}\\MPFiles\\eSight.RackServer.Library.mpb";
                MGroup.Instance.InstallMpb(path);
            }
            if (!MGroup.Instance.CheckIsInstallMp("eSight.KunLunServer.Library"))
            {
                string path = $"{this.InstallPath}\\MPFiles\\eSight.KunLunServer.Library.mpb";
                MGroup.Instance.InstallMpb(path);
            }
#endif
        }

        /// <summary>
        ///     开始轮询
        /// </summary>
        private void RunPolling()
        {
#if DEBUG
            this.pollingTimer = new Timer(5 * 60 * 1000) { Enabled = true, AutoReset = true };
            this.pollingTimer.Elapsed += (s, e) => { this.RunSync(); };
            this.pollingTimer.Start();
#else
            var config = ConfigHelper.GetPluginConfig();
            this.pollingTimer = new Timer(config.PollingInterval)
            {
                Enabled = true,
                AutoReset = true,
            };
            this.pollingTimer.Elapsed += (s, e) =>
                {
                    this.RunSync();
                };
            this.pollingTimer.Start();
#endif
        }

        /// <summary>
        ///     执行轮询任务
        /// </summary>
        private void RunSync()
        {
            try
            {
                this.OnLog("Run Sync Task");

                this.OnLog("Check is need update pd..");
                ESightEngine.Instance.Init();
                var hwESightHostList = ESightDal.Instance.GetList();
                if (hwESightHostList != null)
                {
                    this.OnLog($"hwESightHostList Count :{hwESightHostList.Count}");
                    foreach (var x in hwESightHostList)
                    {
                        var instance = this.FindInstance(x);
                        instance.Sync();
                    }
                }
                else
                {
                    this.OnLog("hwESightHostList is null");
                }
            }
            catch (Exception ex)
            {
                this.OnError("RunSync Error: ", ex);
            }
        }

        /// <summary>
        /// 启动IIS Express
        /// </summary>
        private void RunWebServer()
        {
            // var cmd = $"cd %{EnvVariable}%\\IISExpress&&iisexpress /config:\"%{EnvVariable}%\\Configuration\\applicationhost.config\" /site:SCOMPluginWebServer /systray:true ";
            this.OnLog("Start IISExpress");
            var iisPath = Path.GetFullPath(
                $"{Environment.GetEnvironmentVariable("ProgramFiles(x86)")}\\IIS Express\\iisexpress.exe");
            var configFilePath = Path.GetFullPath($"{this.InstallPath}\\Configuration\\applicationhost.config");
            this.IISProcess = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = iisPath;
            startInfo.Arguments = $" /config:\"{configFilePath}\" /systray:false ";
            startInfo.UseShellExecute = false; // 是否使用操作系统shell启动
            startInfo.RedirectStandardInput = true; // 接受来自调用程序的输入信息
            startInfo.RedirectStandardOutput = true; // 由调用程序获取输出信息
            startInfo.RedirectStandardError = true; // 重定向标准错误输出

            this.IISProcess.StartInfo = startInfo;
            this.IISProcess.OutputDataReceived += (s, e) => { this.OnIISLog(e.Data); };
            this.IISProcess.ErrorDataReceived += (s, e) => { this.OnIISLog(e.Data); };
            this.IISProcess.Start();
            this.IISProcess.BeginErrorReadLine();
            this.IISProcess.BeginOutputReadLine();
        }

        /// <summary>
        /// Finds the instance.
        /// </summary>
        /// <param name="eSight">The e sight.</param>
        /// <returns>Huawei.SCOM.ESightPlugin.Service.ESightSyncInstance.</returns>
        private ESightSyncInstance FindInstance(HWESightHost eSight)
        {
            var syncInstance = this.SyncInstances.FirstOrDefault(y => y.ESightIp == eSight.HostIP);
            if (syncInstance == null)
            {
                syncInstance = new ESightSyncInstance(eSight);
                this.SyncInstances.Add(syncInstance);
            }
            else
            {
                syncInstance.UpdateESight(eSight);
            }
            return syncInstance;
        }

        #endregion

        #region Notify

        /// <summary>
        /// Creates the TCP server.
        /// </summary>
        private void CreateTcpServer()
        {
            try
            {
                int localTcpPort = this.GetPort();
                IPAddress localAddr = IPAddress.Parse("127.0" + ".0.1");
                TcpListener tcpListener = new TcpListener(localAddr, localTcpPort);
                tcpListener.Start();

                var pluginConfig = ConfigHelper.GetPluginConfig();
                pluginConfig.TempTcpPort = localTcpPort;
                ConfigHelper.SavePluginConfig(pluginConfig);

                var bytes = new byte[256 * 256 * 16];
                while (true)
                {
                    try
                    {
                        TcpClient tcp = tcpListener.AcceptTcpClient();
                        NetworkStream stream = tcp.GetStream();
                        int i;
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            var json = Encoding.UTF8.GetString(bytes, 0, i);
                            Task.Run(() => { AnalysisTcpMsg(json); });
                            // Send back a response.
                            byte[] responseMsg = Encoding.UTF8.GetBytes("Received");
                            stream.Write(responseMsg, 0, responseMsg.Length);
                        }
                        stream.Close();
                        tcp.Close();
                    }
                    catch (Exception ex)
                    {
                        this.OnError(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                this.OnError(ex.ToString());
            }
        }

        /// <summary>
        /// Analysises the TCP MSG.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <exception cref="System.Exception">can not find eSight:" + tcpMessage.ESightIp</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public void AnalysisTcpMsg(string json)
        {
            var tcpMessage = JsonUtil.DeserializeObject<TcpMessage<object>>(json);
            try
            {
                if (tcpMessage.MsgType != TcpMessageType.KeepAlive)
                {
                    HWLogger.GetESightNotifyLogger(tcpMessage.ESightIp).Info($"RecieveTcpMsg. data:{json}");
                }
                else
                {
                    HWLogger.GetESightSubscribeLogger(tcpMessage.ESightIp).Info($"RecieveTcpMsg. data:{json}");
                }
                var eSight = ESightDal.Instance.GetEntityByHostIp(tcpMessage.ESightIp);
                if (eSight == null && tcpMessage.MsgType != TcpMessageType.DeleteESight)
                {
                    throw new Exception("can not find eSight:" + tcpMessage.ESightIp);
                }
                switch (tcpMessage.MsgType)
                {
                    case TcpMessageType.SyncESight:
                        this.RunSyncESight(eSight);
                        break;
                    case TcpMessageType.DeleteESight:
                        this.RunDeleteESight(tcpMessage.ESightIp);
                        break;
                    case TcpMessageType.Alarm:
                        this.AnalysisAlarm(eSight, json);
                        break;
                    case TcpMessageType.NeDevice:
                        this.AnalysisNeDevice(eSight, json);
                        break;
                    case TcpMessageType.KeepAlive:
                        this.AnalysisAlive(eSight, json);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                HWLogger.GetESightSubscribeLogger(tcpMessage.ESightIp).Error(e);
            }
        }

        /// <summary>
        /// Runs to delete eSight.
        /// </summary>
        /// <param name="eSightIp">The e sight ip.</param>
        private void RunDeleteESight(string eSightIp)
        {
            HWLogger.GetESightNotifyLogger(eSightIp).Info($"Recieve: delete this eSight");
            Task.Run(() =>
            {
                BladeConnector.Instance.RemoveServerFromMGroup(eSightIp);
                HighdensityConnector.Instance.RemoveServerFromMGroup(eSightIp);
                RackConnector.Instance.RemoveServerFromMGroup(eSightIp);
                KunLunConnector.Instance.RemoveServerFromMGroup(eSightIp);
            });
            var syncInstance = this.SyncInstances.FirstOrDefault(y => y.ESightIp == eSightIp);
            if (syncInstance != null)
            {
                syncInstance.Close();
                this.SyncInstances.Remove(syncInstance);
            }
        }

        /// <summary>
        /// Runs to synchronize eSight.
        /// </summary>
        /// <param name="eSight">The e sight.</param>
        private void RunSyncESight(HWESightHost eSight)
        {
            HWLogger.GetESightNotifyLogger(eSight.HostIP).Info($"Recieve: Sync this eSight");
            var instance = this.FindInstance(eSight);
            instance.Sync();
        }

        /// <summary>
        /// 解析告警变更消息
        /// </summary>
        /// <param name="eSight">The eSight.</param>
        /// <param name="json">The json.</param>
        /// <exception cref="System.Exception"></exception>
        private void AnalysisAlarm(HWESightHost eSight, string json)
        {
            try
            {
                var messge = JsonUtil.DeserializeObject<TcpMessage<NotifyModel<AlarmData>>>(json);
                var alarmData = messge.Data;
                if (alarmData != null)
                {
                    var dn = alarmData.Data.NeDN;
                    var alarmSn = alarmData.Data.AlarmSN;
                    HWLogger.GetESightNotifyLogger(eSight.HostIP).Info($"[alarmSn:{alarmSn}] AnalysisAlarm.[DN:{dn}][optType:{alarmData.Data.OptType}][alarmName:{alarmData.Data.AlarmName}] ");

                    var instance = this.FindInstance(eSight);
                    var serverType = instance.GetServerTypeByDn(dn);
                    if (serverType == ServerTypeEnum.ChildBlade)
                    {
                        //更新子刀片的管理板
                        var childDeviceId = $"{eSight.HostIP}-{dn}";
                        var parentDn = BladeConnector.Instance.GetParentDn(childDeviceId);
                        instance.StartUpdateTask(parentDn, ServerTypeEnum.Blade, alarmSn);
                    }
                    if (serverType == ServerTypeEnum.ChildHighdensity)
                    {
                        //更新子刀片的管理板
                        var childDeviceId = $"{eSight.HostIP}-{dn}";
                        var parentDn = HighdensityConnector.Instance.GetParentDn(childDeviceId);
                        instance.StartUpdateTask(parentDn, ServerTypeEnum.Highdensity, alarmSn);
                    }
                    if (serverType == ServerTypeEnum.Switch)
                    {
                        //交换板告警 只用更新管理板
                        var childDeviceId = $"{eSight.HostIP}-{dn}";
                        var parentDn = BladeConnector.Instance.GetSwitchParentDn(childDeviceId);
                        instance.StartUpdateTask(parentDn, ServerTypeEnum.Blade, alarmSn);
                        return;
                    }
                    instance.StartUpdateTask(alarmData.Data.NeDN, serverType, alarmSn);
                    instance.InsertEvent(alarmData.Data, serverType); // 插入事件
                }
            }
            catch (Exception e)
            {
                HWLogger.GetESightNotifyLogger(eSight.HostIP).Error($"AnalysisAlarm Error.", e);
            }
        }

        /// <summary>
        /// 解析设备变更消息
        /// </summary>
        /// <param name="eSight">The e sight.</param>
        /// <param name="json">The json.</param>
        /// <exception cref="System.Exception">
        /// </exception>
        private void AnalysisNeDevice(HWESightHost eSight, string json)
        {
            try
            {
                var messge = JsonUtil.DeserializeObject<TcpMessage<NotifyModel<NedeviceData>>>(json);
                var nedeviceData = messge.Data;
                if (nedeviceData == null)
                {
                    return;
                }
                var dn = nedeviceData.Data.DeviceId;
                HWLogger.GetESightNotifyLogger(eSight.HostIP).Info($"AnalysisNeDevice [MsgType:{nedeviceData.MsgType}] [DN:{dn}][deviceName:{nedeviceData.Data.DeviceName}][desc:{nedeviceData.Data.Description}] ");
                var instance = this.FindInstance(eSight);
                switch (nedeviceData.MsgType)
                {
                    case 1: // 新增
                        //instance.Sync();
                        break;
                    case 2: // 删除
                            // 高密的设备变更消息需要单独处理
                        var deleteServerType = instance.GetServerTypeByDn(dn);
                        if (deleteServerType == ServerTypeEnum.ChildHighdensity || deleteServerType == ServerTypeEnum.Highdensity)
                        {
                            instance.SyncHighdensityList();
                        }
                        else
                        {
                            instance.DeleteServer(dn, deleteServerType);
                        }
                        break;
                    case 3: // 修改
                        var modifyServerType = instance.GetServerTypeByDn(dn);
                        instance.StartUpdateTask(dn, modifyServerType, 0);
                        instance.InsertDeviceChangeEvent(nedeviceData.Data, modifyServerType);
                        break;
                    default:
                        //HWLogger.NOTIFICATION_PROCESS.Error($"UnKnown MsgType :{dn}");
                        break;
                }
            }
            catch (Exception e)
            {
                HWLogger.GetESightNotifyLogger(eSight.HostIP).Error($"AnalysisNeDevice Error.", e);
            }

        }

        /// <summary>
        /// 解析保活消息
        /// </summary>
        /// <param name="eSight">The e sight.</param>
        /// <param name="json">The json.</param>
        private void AnalysisAlive(HWESightHost eSight, string json)
        {
            HWLogger.GetESightSubscribeLogger(eSight.HostIP).Info($"Analysis Alive Message. ");
            //var messge = JsonUtil.DeserializeObject<TcpMessage<NotifyModel<KeepAliveData>>>(json);
            var instance = this.FindInstance(eSight);
            instance.UpdateAliveTime(DateTime.Now);
        }

        /// <summary>
        /// Gets the port.
        /// </summary>
        /// <returns>System.Int32.</returns>
        private int GetPort()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var udpPorts = properties.GetActiveUdpListeners().Select(x => x.Port).ToList();
            var tcpPorts = properties.GetActiveTcpListeners().Select(x => x.Port).ToList();
            for (int i = 40001; i < 40500; i++)
            {
                if (!udpPorts.Contains(i) && !tcpPorts.Contains(i))
                {
                    return i;
                }
            }
            return 0;
        }

        #endregion

        #region Import Self Cert
        /// <summary>
        /// Gets the self cert.
        /// </summary>
        /// <returns>System.Security.Cryptography.X509Certificates.X509Certificate2.</returns>
        private X509Certificate2 GetSelfCert()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var cert in store.Certificates)
            {
                if (cert.Subject == "CN=localhost")
                {
                    return cert;
                }
            }
            store.Close();
            return null;
        }

        /// <summary>
        /// Determines whether [is exsit cert].
        /// </summary>
        /// <returns>System.Boolean.</returns>
        private bool IsNeedImport()
        {
            var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var cert in store.Certificates)
            {
                if (cert.Subject == "CN=localhost")
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Imports to root.
        /// </summary>
        private void ImportToRoot()
        {
            if (this.IsNeedImport())
            {
                this.OnLog("Start import the self signed certificate");
                var self = this.GetSelfCert();
                if (self == null)
                {
                    throw new Exception("can not fin Self Signed Certificate");
                }
                var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(self);
                store.Close();
            }
            else
            {
                this.OnLog("Do not need import the self signed certificate");
            }
        }
        #endregion

        #region Utils

        /// <summary>
        /// The on error.
        /// </summary>
        /// <param name="msg">
        /// The msg.
        /// </param>
        /// <param name="ex">
        /// The ex.
        /// </param>
        private void OnError(string msg, Exception ex = null)
        {
            HWLogger.Service.Error(msg, ex);
        }

        /// <summary>
        /// The on log.
        /// </summary>
        /// <param name="msg">
        /// The msg.
        /// </param>
        private void OnLog(string msg)
        {
            HWLogger.Service.Info(msg);
        }

        /// <summary>
        /// Called when [IIS log].
        /// </summary>
        /// <param name="msg">The MSG.</param>
        private void OnIISLog(string msg)
        {
            // IIS请求的日志
            if (msg != null)
            {
                if (!msg.StartsWith("Request"))
                {
                    HWLogger.Service.Info("IIS-" + msg);
                }
            }
        }

        #endregion

    }
}