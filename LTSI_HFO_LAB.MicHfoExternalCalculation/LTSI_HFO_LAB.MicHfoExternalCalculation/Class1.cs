using System;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using Micromed.EventCalculation.Common;
using Micromed.ExternalCalculation.Common.Dto;

namespace LTSI_HFO_LAB.MicHfoExternalCalculation
{
    public class LTSI_HFOPlugin : IExternalCalculationPlugin, IPluginInfo
    {
        public Guid Guid { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Author { get; private set; }
        public string Version { get; private set; }

        private bool isRunning = false;

        public LTSI_HFOPlugin()
        {
            Guid = Guid.NewGuid();
            Name = "LTSI HFO_LAB";
            Description = "LTSI HFO Automatic Detection plugin";
            Author = "LTSI";
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            Version = fvi.FileVersion;
        }

        private int runCommand(string exeCommand, PluginParametersDto pluginParameters)
        {
            string tempFilePath;
            FileStream fileStream;
            UInt16 DerivationNumber;
            byte[] data = new byte[2];

            // read temporary mxt file to determine number of elements
            tempFilePath = pluginParameters.ExchangeTraceFilePathList.First();
            fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
            fileStream.Seek(129, SeekOrigin.Begin);
            fileStream.Read(data, 0, 2);
            fileStream.Dispose();
            DerivationNumber = BitConverter.ToUInt16(data, 0);
            ProcessStartInfo cmdsi = new ProcessStartInfo(exeCommand);

            cmdsi.Arguments = pluginParameters.ExchangeTraceFilePathList.First() + " " + DerivationNumber.ToString() + " " + pluginParameters.ExchangeEventFilePath;
            Process cmd = Process.Start(cmdsi);
            cmd.WaitForExit();
            return cmd.ExitCode;
        }


        public int Start(PluginParametersDto pluginParameters)
        {
            byte[] data = new byte[8];

            if (isRunning)
                return 1;

            int ret;
            try
            {
                ret = runCommand(@"C:\System98\Programs\HFOLAB_LTSI\Master_HFOLAB\WindowsFormsApp1.exe", pluginParameters);
            }
            catch (Exception ex)
            {
                ret = 2;
            }

            isRunning = true;
            OnProgress(100);
            return ret;
        }

        public bool Stop()
        {
            if (!isRunning)
                return false;


            isRunning = false;
            OnCancelled();
            return true;
        }



        public event EventHandler Completed;
        protected void OnCompleted()
        {
            EventHandler handler = Completed;
            if (handler != null) handler(this, null);
        }

        public event EventHandler Cancelled;
        protected void OnCancelled()
        {
            EventHandler handler = Cancelled;
            if (handler != null) handler(this, null);
        }

        public event EventHandler<string> Error;
        protected void OnError(string e)
        {
            EventHandler<string> handler = Error;
            if (handler != null) handler(this, e);
        }

        public event EventHandler<int> Progress;
        protected void OnProgress(int e)
        {
            EventHandler<int> handler = Progress;
            if (handler != null) handler(this, e);
        }

        public bool NeedTraceFilePathList
        {
            get { return false; }
        }

        public bool NeedExchangeTraceFilePathList
        {
            get { return true; }
        }

        public bool NeedExchangeEventFilePath
        {
            get { return true; }
        }

        public bool NeedExchangeReportFilePath
        {
            get { return false; }
        }

        public bool NeedExchangeTrendFilePathList
        {
            get { return false; }
        }

        public bool NeedFilteredData
        {
            get { return false; }
        }

        public bool DerivationOptionEnabled
        {
            get { return false; }
        }

        public bool TraceSelectionOptionEnabled
        {
            get { return true; }
        }
    }


}