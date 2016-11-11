﻿using System;
using System.Diagnostics;
using ServiceRunner.Logs;

namespace ServiceRunner.Service
{
    internal class ServiceRunner : IDisposable
    {
        private readonly ServiceInfo _serviceInfo;
        private readonly LogManager _logManager;

        private Process _osrmProcess;

        private int _failsCount;

        public ServiceRunner(ServiceInfo serviceInfo, LogManager logManager)
        {
            if (serviceInfo == null) throw new ArgumentNullException(nameof(serviceInfo));
            if (logManager == null) throw new ArgumentNullException(nameof(logManager));
            _serviceInfo = serviceInfo;
            _logManager = logManager;
            
        }

        public void Start()
        {
            _osrmProcess = new Process
            {
                StartInfo =
                {
                    FileName = _serviceInfo.ServicePath,
                    Arguments = _serviceInfo.ServiceArguments,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };



            _osrmProcess.OutputDataReceived += ProcessOnOutputDataReceived;
            _osrmProcess.ErrorDataReceived += ProcessOnErrorDataReceived;
            _osrmProcess.Exited += ProcessOnExited;

            _osrmProcess.Start();
            _osrmProcess.BeginErrorReadLine();
            _osrmProcess.BeginOutputReadLine();
        }

        private void ProcessOnExited(object sender, EventArgs eventArgs)
        {
            // штатное завершение
            if (_osrmProcess.ExitCode == 0)
            {
                _logManager.MainLog.Info("Service normally terminated");
                return;
            }

            _logManager.MainLog.Error("Service crashed");
            if (_serviceInfo.RestartAfterCrash)
            {
                if (_failsCount < _serviceInfo.RestartCountOnFail)
                {
                    _failsCount++;
                    _logManager.MainLog.Info($"Trying to restart service ({_failsCount}/{_serviceInfo.RestartCountOnFail})... ");
                    Stop();
                    Start();
                    _logManager.MainLog.Info("Service successfully restarted");
                    return;
                }
            }
            _logManager.MainLog.Fatal("Can not restart service");
            throw new Exception("Can not restart service");
        }

        private void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            if (String.IsNullOrEmpty(dataReceivedEventArgs?.Data)) return;

            _logManager.ServiceExceptionLog.Error(dataReceivedEventArgs.Data);
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            if (String.IsNullOrEmpty(dataReceivedEventArgs?.Data)) return;
            
            _logManager.ServiceMainMainLog.Info(dataReceivedEventArgs.Data);
        }


        public void Stop()
        {
            _osrmProcess?.Close();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}