using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Xml;
using System.Globalization;


namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // read arguments from plugin 
            String[] arguments = Environment.GetCommandLineArgs();
            string filename = arguments[1];
            string  nbElectrodes = arguments[2];
            string nameS, surnameS;
            // read brainQuick exchange File
            byte[] surname= new byte[sizeof(byte)*22];
            byte[] name = new byte[sizeof(byte) * 20];
            byte[] recordDateTime = new byte[sizeof(Int64)*1];
            byte[] samplingFrequency = new byte[sizeof(UInt16) * 1];
            byte[] sampleLength = new byte[sizeof(UInt16) * 1];
            DateTime dateTimeVar;
            int doubleOrFloat;
            long position = 0;
            using (FileStream fileStream = new FileStream(filename, FileMode.Open))
            {
                try { 
                fileStream.Seek(50,SeekOrigin.Begin);
                fileStream.Read(surname, 0, sizeof(byte) * 22);
                fileStream.Seek(72, SeekOrigin.Begin);
                fileStream.Read(name, 0, sizeof(byte) * 20);
                fileStream.Seek(119, SeekOrigin.Begin);
                fileStream.Read(recordDateTime, 0, sizeof(Int64));
                fileStream.Seek(127, SeekOrigin.Begin);
                fileStream.Read(sampleLength, 0, sizeof(UInt16));
                fileStream.Seek(131, SeekOrigin.Begin);
                fileStream.Read(samplingFrequency, 0, sizeof(UInt16));
                fileStream.Seek(0, SeekOrigin.End);
                position = fileStream.Position;
          
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                nameS = Encoding.GetEncoding("iso-8859-1").GetString(name).Replace("\0",string.Empty);
                surnameS = Encoding.GetEncoding("iso-8859-1").GetString(surname).Replace("\0", string.Empty);               
                textPatientName.Text = nameS + " " + surnameS;
                dateTimeVar = new DateTime(BitConverter.ToInt64(recordDateTime,0));
                textDate.Text = dateTimeVar.Date.ToString("d"); //display date
                textTime.Text = dateTimeVar.Hour.ToString("00") + ":" + dateTimeVar.Minute.ToString("00") + ":" + dateTimeVar.Second.ToString("00");//display time
                textSamplingFrequency.Text = BitConverter.ToUInt16(samplingFrequency, 0).ToString();
                textNbChannels.Text = nbElectrodes.ToString();
                doubleOrFloat = BitConverter.ToUInt16(sampleLength, 0);
                textDuration.Text = (((position - 20613) / (doubleOrFloat*int.Parse(nbElectrodes)* BitConverter.ToUInt16(samplingFrequency, 0)))).ToString("0.##");                
                textNbProcessors.Text = Environment.ProcessorCount.ToString(); // determine number of logical cores                
                numericUpDownProcessors.Maximum = Math.Round((decimal)(Environment.ProcessorCount*0.5)); // set maximum of usable logical cores                
            }

        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            // calculate elapsed time
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            // Index of analyzed contacts
            int invID, nonInvID;
            var eventsList = new List<LabEvent>(); // event list declaration
            String[] arguments = Environment.GetCommandLineArgs(); // read arguments from plugin 
            DateTime myDate, startEvent, endEvent; // dateTime variables
            int nbChannels = int.Parse(arguments[2]); // get number of selected channels           
            int nbCores = int.Parse(numericUpDownProcessors.Text); // read number of cores to be used           
            string exFilename = arguments[1]; // trace file path            
            string evFilename = arguments[3]; // event file path            
            string staticCmd;// process to be launched
            string exeCommand = @"C:\System98\Programs\HFOLAB_LTSI\detectHFO.app\detectHFO.exe"; // path of HFOLAB mono detector
            ProcessStartInfo cmdsi = new ProcessStartInfo(exeCommand);
            staticCmd = exFilename + " " + numericUpDownThreshold.Text; // Static command                      
            int[] mode = new int[nbChannels]; // array of mode
            progressBar1.Step = (int)Math.Round((decimal)(progressBar1.Maximum / (nbChannels/nbCores))); // progress bar step
            
            // wait label
            labelWait.Text = "please wait ... ";
            // delete all (.mrk) files in tmp directory
            DirectoryInfo directory = new DirectoryInfo(@"C:\System98\Programs\HFOLAB_LTSI\tmp\");
            foreach (FileInfo file in directory.GetFiles()) file.Delete();

            // Fill mode array
            for (int i = 0; i < nbChannels; i++)
            {
                mode[i] = 1;
                if ((i % nbCores) == 0)
                { mode[i] = 0; }
            }
            mode[nbChannels - 1] = 0;


            // read date and time 
            FileStream fileStream;
            byte[] data = new byte[8];
            Int64 timeticks;
            fileStream = new FileStream(arguments[1], FileMode.Open, FileAccess.Read);
            fileStream.Seek(119, SeekOrigin.Begin);
            fileStream.Read(data, 0, 8);
            timeticks = BitConverter.ToInt64(data, 0);
            myDate = new DateTime(timeticks);
            fileStream.Dispose();

            // ------------------------ detection starts -----------------------
            for (int k = 0; k < nbChannels; k++)
            {
                cmdsi.Arguments = staticCmd + " " + k.ToString();
                if (IsFileReady(exFilename))
                { Process cmd = Process.Start(cmdsi);
                    if (mode[k] == 0)
                    {
                        cmd.WaitForExit();
                        progressBar1.PerformStep();

                    }


                }

            }
            //------------------------ detection finished -----------------------

           
            //------------------------ Write evt file start ---------------------

            // general xml settings
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = " ",
                ConformanceLevel = ConformanceLevel.Document,
                OmitXmlDeclaration = true
            };
            // general xml heading
            XmlWriter xmlWriter = XmlWriter.Create(Path.ChangeExtension(evFilename, "evt"), settings);
            Guid spikesGuid = Guid.NewGuid();
            Guid ripplesGuid = Guid.NewGuid();
            Guid FastRipplesGuid = Guid.NewGuid();
            Guid GammaGuid = Guid.NewGuid();
            Guid HGammaGuid = Guid.NewGuid();
            string[] name = { "HfoSpike", "HfoRipple", "HfoFastRipple", "HfoGamma", "HfoHighGamma" };
            string[] guid = { spikesGuid.ToString(), ripplesGuid.ToString(), FastRipplesGuid.ToString(), GammaGuid.ToString(), HGammaGuid.ToString() };
            string[] description = { "Spike", "Ripple", "Fast Ripple", "Gamma", "HGamma" };
            string[] colorRect = { "4294901760", "4278228736", "4278231539", "4294902015", "4294947763" };
            string[] colorFont = { "4278190080", "4286599762", "4294947763", "4294902015", "4294901760" };
            try
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("EventFile");
                xmlWriter.WriteAttributeString("CreationDate", DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
                xmlWriter.WriteAttributeString("Version", "1.00");
                xmlWriter.WriteAttributeString("Guid", Guid.NewGuid().ToString());
                //Event types
                xmlWriter.WriteStartElement("EventTypes");
                //Category
                xmlWriter.WriteStartElement("Category");
                xmlWriter.WriteAttributeString("Name", "Hfo");
                xmlWriter.WriteElementString("Description", "HFO");
                xmlWriter.WriteElementString("IsPredefined", "true");
                xmlWriter.WriteElementString("Guid", "27e2727f-e49d-4113-aa8c-4944ef8f2588");
                //SubCategory
                xmlWriter.WriteStartElement("SubCategory");
                xmlWriter.WriteAttributeString("Name", "DefaultHfo");
                xmlWriter.WriteElementString("Description", "HFO");
                xmlWriter.WriteElementString("IsPredefined", "true");
                xmlWriter.WriteElementString("Guid", "c142e214-826e-4dfe-965a-110246492c9e");
                //Definition of all 5 subcategories
                for (int i = 0; i < 5; i++)
                {
                    xmlWriter.WriteStartElement("Definition");
                    xmlWriter.WriteAttributeString("Name", name[i]);
                    xmlWriter.WriteElementString("Guid", guid[i]);
                    xmlWriter.WriteElementString("Description", description[i]);
                    if (i == 4)
                        xmlWriter.WriteElementString("IsPredefined", "false");
                    else
                        xmlWriter.WriteElementString("IsPredefined", "false");
                    xmlWriter.WriteElementString("IsDefinitionAdjustable", "false");
                    xmlWriter.WriteElementString("CanInsert", "true");
                    xmlWriter.WriteElementString("CanDelete", "true");
                    xmlWriter.WriteElementString("CanUpdateText", "false");
                    xmlWriter.WriteElementString("CanUpdatePosition", "true");
                    xmlWriter.WriteElementString("CanReassign", "false");
                    xmlWriter.WriteElementString("InsertionType", "ClickAndDrag");
                    xmlWriter.WriteElementString("FixedInsertionDuration", "PT1S");
                    xmlWriter.WriteElementString("TextType", "FromDefinitionDescription");
                    xmlWriter.WriteElementString("ReferenceType", "SingleLine");
                    xmlWriter.WriteElementString("DurationType", "Interval");
                    xmlWriter.WriteElementString("TextArgbColor", colorRect[i]);
                    xmlWriter.WriteElementString("GraphicArgbColor", colorFont[i]);
                    xmlWriter.WriteElementString("GraphicType", "EmptyRectangle");
                    xmlWriter.WriteElementString("TextPositionType", "Top");
                    xmlWriter.WriteElementString("VisualizationType", "Graphic");
                    xmlWriter.WriteElementString("FontFamily", "Segoe UI");
                    xmlWriter.WriteElementString("FontSize", "11");
                    xmlWriter.WriteElementString("FontItalic", "false");
                    xmlWriter.WriteElementString("FontBold", "false");
                    // End Defintion Spike
                    xmlWriter.WriteEndElement();
                }

                //End SubCategory
                xmlWriter.WriteEndElement();
                //End Category
                xmlWriter.WriteEndElement();
                //End EventTypes
                xmlWriter.WriteEndElement();

            string dir = @"C:\System98\Programs\HFOLAB_LTSI\tmp\"; // directory of (.mrk) files 
            string[] files = Directory.GetFiles(dir);

            // loop over each saved event file (.mrk)
            foreach (string file in files)
            {
                 // determine which channels
                 string croppedEnd = file.Remove(file.Length - 6);
                 string mergedIndexes = croppedEnd.Substring(croppedEnd.Length - 7);
                 string[] words = mergedIndexes.Split('_');
                 invID = int.Parse(words[0]);
                 nonInvID = int.Parse(words[1]);
                 
                 // read all events from .evt File
                 eventsList = readAllEvents(dir + Path.GetFileName(file));  
                
                //loop over each of the events
                // Start Events
                xmlWriter.WriteStartElement("Events");
                for (int i = 0; i < eventsList.Count; i++)
                {
                    //start a new event
                    xmlWriter.WriteStartElement("Event");
                    xmlWriter.WriteAttributeString("Guid", Guid.NewGuid().ToString());
                    if (eventsList[i].Type.StartsWith("S"))
                        xmlWriter.WriteElementString("EventDefinitionGuid", guid[0]);
                    if (eventsList[i].Type.StartsWith("R"))
                        xmlWriter.WriteElementString("EventDefinitionGuid", guid[1]);
                    if (eventsList[i].Type.StartsWith("F"))
                        xmlWriter.WriteElementString("EventDefinitionGuid", guid[2]);
                    if (eventsList[i].Type.StartsWith("G"))
                        xmlWriter.WriteElementString("EventDefinitionGuid", guid[3]);
                    if (eventsList[i].Type.StartsWith("H"))
                        xmlWriter.WriteElementString("EventDefinitionGuid", guid[4]);

                    //add seconds to ticks   
                    try
                    {
                        startEvent = myDate.AddSeconds(float.Parse(eventsList[i].startTime, CultureInfo.InvariantCulture) - 1);
                        endEvent = myDate.AddSeconds(float.Parse(eventsList[i].endTime, CultureInfo.InvariantCulture) - 1);
                        xmlWriter.WriteElementString("Begin", startEvent.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
                        xmlWriter.WriteElementString("End", endEvent.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
                    }
                    catch (Exception ex) { label1.Text = ex.Message; }
                    xmlWriter.WriteElementString("Text", "new HFO Ripple");
                    xmlWriter.WriteElementString("Value", "0");
                    xmlWriter.WriteElementString("ExtraValue", "0");
                    xmlWriter.WriteElementString("DerivationInvID", invID.ToString());
                    xmlWriter.WriteElementString("DerivationNotInvID", nonInvID.ToString());
                    xmlWriter.WriteElementString("CreatedBy", "User");
                    xmlWriter.WriteElementString("CreatedDate", DateTime.Today.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
                    xmlWriter.WriteElementString("UpdatedBy", "User");
                    xmlWriter.WriteElementString("UpdatedDate", DateTime.Today.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));

                    //End event
                    xmlWriter.WriteEndElement();

                }
                //End events
                xmlWriter.WriteEndElement();
            }
        }

            catch (Exception ex)
            {
                Debug.WriteLine("WriteEventFile failed: " + ex.Message);
            }

            xmlWriter.Flush();
            // End of File
            xmlWriter.WriteEndElement();
            xmlWriter.Dispose();
            //---------------------- Write evt file finished ---------------------

            stopWatch.Stop();

            // wait label
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}h{1:00}m{2:00}.{3:00}s",ts.Hours, ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
            labelWait.Text = "DONE. Processing time: " + elapsedTime;

        }


        public List<LabEvent> readAllEvents(string filename)
        {
            string s = File.ReadAllText(filename);
            string[] words = s.Split(new Char[] { ' ', '\n', '\\' });
            var eventsList = new List<LabEvent>();
            for (int i = 1; i < words.GetLength(0) - 11; i++)
            {
                if (words[i].Contains("[tps1]"))
                {
                    eventsList.Add(new LabEvent
                    {
                        startTime = words[i + 1],
                        endTime = words[i + 5],
                        Type = words[i + 11],
                    });

                }

            }

            return eventsList;
        }

        public class LabEvent
        {
            public string startTime { get; set; }
            public string endTime { get; set; }
            public string Type { get; set; }

        }

        public static bool IsFileReady(String sFilename)
        {
             
            // file is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(sFilename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (inputStream.Length > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return false;
            }
        }

        private void buttonExit_Click(object sender, EventArgs e)
        {
            // delete all (.mrk) files in temp
            DirectoryInfo directory = new DirectoryInfo(@"C:\System98\Programs\HFOLAB_LTSI\tmp\");
            foreach (FileInfo file in directory.GetFiles()) file.Delete();
            // exit application
            Application.Exit();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {   
            // play a sound when closing :p
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(@"C:\Windows\Media\Windows Logoff Sound.wav");
            player.Play();
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);
        }

    }
}
